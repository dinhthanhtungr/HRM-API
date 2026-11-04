import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";

export class PreviewLabTestImportUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(file: File, mapping: Record<string, string>) {
    return this.repository.preview(file, mapping);
  }
}
