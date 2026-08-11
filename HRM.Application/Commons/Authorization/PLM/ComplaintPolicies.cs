namespace HRM.Application.Commons.Authorization.PLM;

public static class ComplaintPolicies
{
    public const string Create = "Complaint.Create";
    public const string Investigate = "Complaint.Investigate";
    public const string ActionUpdate = "Complaint.Action.Update";
    public const string Verify = "Complaint.Verify";
    public const string InitialApprove = "Complaint.InitialApprove";
    public const string FinalApprove = "Complaint.FinalApprove";
    public const string ViewPdf = "Complaint.ViewPdf";
}
