import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";
import type { LabWrite } from "../../domain/dtos/LabTest";

export class CreateLabTestUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(data: LabWrite) {
    return this.repository.create(data);
  }
}
