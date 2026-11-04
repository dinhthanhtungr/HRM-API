import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";

export class DeleteLabTestUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(id: string) {
    return this.repository.delete(id);
  }
}
