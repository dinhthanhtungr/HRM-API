namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Enable only after the operator has applied and verified the group schema, backfill and legacy-write bridge.</summary>
// GROUP-STORAGE-TRANSITION: switch — remove only after canonical writers/readers replace every legacy path; see TRANSITION_CLEANUP.md.
public sealed class InternalMailGroupStorageOptions
{
    public const string SectionName = "InternalMail:GroupStorage";
    public bool Enabled { get; set; }
}
