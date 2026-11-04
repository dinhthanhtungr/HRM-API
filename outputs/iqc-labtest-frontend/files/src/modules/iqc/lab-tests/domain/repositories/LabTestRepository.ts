import type {
  LabImportPreview,
  LabImportResult,
  LabTest,
  LabWrite,
  MaterialTestGrid,
} from "../dtos/LabTest";
export interface LabTestRepository {
  grid(pageNumber: number, signal?: AbortSignal): Promise<MaterialTestGrid>;
  detail(id: string, signal?: AbortSignal): Promise<LabTest>;
  create(data: LabWrite): Promise<LabTest>;
  patch(id: string, data: LabWrite): Promise<LabTest>;
  delete(id: string): Promise<boolean>;
  preview(
    file: File,
    mapping: Record<string, string>,
  ): Promise<LabImportPreview>;
  import(
    batchId: string,
    rows: { rowNumber: number; data: LabWrite }[],
  ): Promise<LabImportResult>;
}
