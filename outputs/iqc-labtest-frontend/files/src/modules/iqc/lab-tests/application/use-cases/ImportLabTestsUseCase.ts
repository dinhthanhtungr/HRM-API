import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";
import type { LabWrite } from "../../domain/dtos/LabTest";

export class ImportLabTestsUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(batchId: string, rows: { rowNumber: number; data: LabWrite }[]) {
    return this.repository.import(batchId, rows);
  }
}
