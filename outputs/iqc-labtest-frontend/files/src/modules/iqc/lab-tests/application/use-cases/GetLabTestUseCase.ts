import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";

export class GetLabTestUseCase {
  constructor(private readonly repository: LabTestRepository) {}

  execute(id: string, signal?: AbortSignal) {
    return this.repository.detail(id, signal);
  }
}
