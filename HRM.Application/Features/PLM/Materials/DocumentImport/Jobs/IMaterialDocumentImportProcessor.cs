namespace HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;

public interface IMaterialDocumentImportProcessor
{
    Task ProcessAsync(
        MaterialDocumentImportJobWorkItem job,
        CancellationToken cancellationToken);
}
