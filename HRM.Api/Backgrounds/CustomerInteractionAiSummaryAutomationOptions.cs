namespace HRM.Api.Backgrounds;

public sealed class CustomerInteractionAiSummaryAutomationOptions
{
    public bool Enabled { get; set; }
    public int PollMinutes { get; set; } = 60;
    public int ScanCustomerLimit { get; set; } = 500;
    public int MaxAiRequestsPerRun { get; set; } = 10;
    public int MaxCustomersPerAiRequest { get; set; } = 5;
    public int MonthlyRunStartDay { get; set; } = 1;
    public int MonthlyRunEndDay { get; set; } = 3;
    public int YearlyRunStartDay { get; set; } = 1;
    public int YearlyRunEndDay { get; set; } = 3;
    public int LifetimeRunStartDay { get; set; } = 4;
    public int LifetimeRunEndDay { get; set; } = 5;
}
