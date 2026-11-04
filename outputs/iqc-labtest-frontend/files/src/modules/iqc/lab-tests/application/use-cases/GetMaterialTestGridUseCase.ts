import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";

export class GetMaterialTestGridUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(page: number, signal?: AbortSignal) {
    return this.repository.grid(page, signal);
  }
}
