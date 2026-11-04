import type { PagedResult } from "../../../evaluation-standards/domain/dtos/EvaluationStandard";
import type { Configuration } from "../../../evaluations/domain/dtos/Evaluation";

export type LabConfiguration = Omit<
  Configuration,
  "showOnReport" | "showOnCoa"
>;
export type LabResult = {
  id: string;
  evaluationStandardId: string | null;
  criterionCode: string;
  criterionName: string;
  criterionNameEn: string | null;
  configuration: LabConfiguration;
  value: string | null;
  observedText: string | null;
  attachmentCollectionId: string | null;
};
export type LabMetadata = {
  sampleName: string;
  testDate: string;
  supplierCustomerName: string | null;
  batchNumber: string | null;
  itemCode: string | null;
  resin: string | null;
  application: string | null;
  preparedByName: string | null;
  note: string | null;
  weightKg: number | null;
  importedPhotoName: string | null;
};
export type LabTest = LabMetadata & {
  id: string;
  code: string;
  evaluationTemplateId: string | null;
  results: LabResult[];
};
export type MaterialTestCell = {
  key: string;
  resultId: string;
  evaluationStandardId: string | null;
  value: string | null;
  observedText: string | null;
  configuration: LabConfiguration;
  attachmentCollectionId: string | null;
};
export type MaterialTestRow = LabMetadata & {
  source: 1 | 2;
  id: string;
  voucherDetailId: string | null;
  materialCode: string | null;
  canEdit: boolean;
  canDelete: boolean;
  cells: MaterialTestCell[];
};
export type MaterialTestColumn = {
  key: string;
  code: string;
  name: string;
  nameEn: string | null;
  unit: string | null;
  dataType: number;
  sortOrder: number;
};
export type MaterialTestGrid = {
  page: PagedResult<MaterialTestRow>;
  columns: MaterialTestColumn[];
};
export type LabResultInput = {
  evaluationStandardId: string;
  value?: string | null;
  observedText?: string | null;
  clearValue?: boolean;
  clearObservedText?: boolean;
};
export type LabWrite = Partial<LabMetadata> & {
  evaluationTemplateId?: string;
  clearFields?: string[];
  results?: LabResultInput[];
};
export type LabImportRow = {
  rowNumber: number;
  data: LabWrite;
  errors: string[];
  warnings: string[];
};
export type LabImportPreview = {
  importBatchId: string;
  totalDataRows: number;
  ignoredRows: number;
  headers: string[];
  warnings: string[];
  rows: LabImportRow[];
  columnStandards: Record<string, string>;
};
export type LabImportResult = {
  created: number;
  skipped: number;
  items: {
    rowNumber: number;
    id: string;
    code: string;
    alreadyImported: boolean;
  }[];
};
