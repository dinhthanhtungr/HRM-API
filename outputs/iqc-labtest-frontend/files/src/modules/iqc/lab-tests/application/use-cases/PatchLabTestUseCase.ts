import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";
import type { LabWrite } from "../../domain/dtos/LabTest";

export class PatchLabTestUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(id: string, data: LabWrite) {
    return this.repository.patch(id, data);
  }
}
