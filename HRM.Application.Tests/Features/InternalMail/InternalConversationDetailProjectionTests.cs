using System.Text.Json;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationDetailProjectionTests
{
    [Fact]
    public void PageLastMessageProjection_TranslatesWithTenantAndMembershipScope()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sql = db.InternalConversationParticipants
            .Where(p => p.EmployeeId == employeeId && p.IsActive && p.Conversation.CompanyId == companyId)
            .OrderByDescending(p => p.Conversation.Messages.Max(m => (DateTime?)m.SentAt))
            .Take(20)
            .Select(p => new
            {
                Participant = p,
                LastMessage = p.Conversation.Messages.Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId)
                    .Select(m => new { m.InternalMessageId, m.Body, m.SenderEmployeeId, SenderName = m.SenderEmployee.FullName })
                    .FirstOrDefault()
            })
            .Select(row => new
            {
                row.Participant.InternalConversationId,
                MessageId = row.LastMessage == null ? null : (Guid?)row.LastMessage.InternalMessageId,
                Body = row.LastMessage == null ? null : row.LastMessage.Body,
                Sender = row.LastMessage == null ? null : row.LastMessage.SenderName
            }).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.Contains("EmployeeId", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void Detail_ReportsRemainingUnreadInActualSerializedContract()
    {
        var employee = Guid.NewGuid();
        var conversation = new InternalConversation { CreatedByNavigation = new Employee() };
        InternalMessage Add(bool read = false, bool deleted = false, Guid? sender = null, Guid? recipient = null)
        {
            var message = new InternalMessage { SenderEmployeeId = sender ?? Guid.NewGuid(), IsDeleted = deleted };
            message.ReadStates.Add(new InternalMessageReadState { EmployeeId = recipient ?? employee, IsRead = read });
            conversation.Messages.Add(message);
            return message;
        }
        var unread = Add();
        Add(read: true);
        Add(deleted: true);
        Add(sender: employee);
        Add(recipient: Guid.NewGuid());
        var project = InternalConversationDetailProjection.ForEmployee(employee).Compile();
        Assert.Equal(1, project(conversation).UnreadCount);
        unread.ReadStates.Single().IsRead = true;
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(project(conversation),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(0, payload.RootElement.GetProperty("unreadCount").GetInt32());
        Add(); // A new message arriving after acknowledgement must survive reconciliation.
        Assert.Equal(1, project(conversation).UnreadCount);
    }

    [Fact]
    public void DetailProjection_TranslatesToPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = db.InternalConversations.Select(InternalConversationDetailProjection.ForEmployee(Guid.NewGuid())).ToQueryString();
        Assert.Contains("count(*)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsDeleted", sql);
    }
}
