namespace HRM.Domain.Enums.InternalMailEnums;

public static class InternalMailAreas
{
    public const string General = "general";
    public const string Technical = "technical";
    public const string Pricing = "pricing";
    public const string Custom = "custom";
    public static bool IsKnown(string? area) => area is General or Technical or Pricing or Custom;
}
