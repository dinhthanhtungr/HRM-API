using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<Address> Addresses { get; set; } = default!;

        public virtual DbSet<Contact> Contacts { get; set; } = default!;

        public virtual DbSet<Customer> Customers { get; set; } = default!;
        public virtual DbSet<CustomerClaim> CustomerClaims { get; set; } = default!;
        public virtual DbSet<CustomerNote> CustomerNotes { get; set; } = default!;

        public virtual DbSet<CustomerAssignment> CustomerAssignments { get; set; } = default!;

        public virtual DbSet<CustomerTransferLog> CustomerTransferLogs { get; set; } = default!;

        public virtual DbSet<DetailCustomerTransfer> DetailCustomerTransfers { get; set; } = default!;
    }
}
