import { apiFetchBinary } from "@/core/api/httpClient";
import { toQueryString } from "@/core/api/queryString";
import { parseIncomingResponse } from "../../../incoming/infrastructure/api/IncomingApiRepository";
import type { LabTestRepository } from "../../domain/repositories/LabTestRepository";
import type {
  LabImportPreview,
  LabImportResult,
  LabTest,
  LabWrite,
  MaterialTestGrid,
} from "../../domain/dtos/LabTest";

const base = "/api/v1/dev-and-qa/lab-tests";
async function request<T>(
  path: string,
  options: Parameters<typeof apiFetchBinary>[1] = {},
): Promise<T> {
  const response = await apiFetchBinary(path, options);
  const result = parseIncomingResponse(
    new TextDecoder().decode(response.content),
  );
  if (result.success !== true)
    throw new Error(result.message || "Request failed.");
  return result.data as T;
}
export class LabTestApiRepository implements LabTestRepository {
  grid(pageNumber: number, signal?: AbortSignal) {
    return request<MaterialTestGrid>(
      `${base}/grid${toQueryString({ pageNumber, pageSize: 30 })}`,
      { signal },
    );
  }
  detail(id: string, signal?: AbortSignal) {
    return request<LabTest>(`${base}/${encodeURIComponent(id)}`, { signal });
  }
  create(data: LabWrite) {
    return request<LabTest>(base, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }
  patch(id: string, data: LabWrite) {
    return request<LabTest>(`${base}/${encodeURIComponent(id)}`, {
      method: "PATCH",
      body: JSON.stringify(data),
    });
  }
  delete(id: string) {
    return request<boolean>(`${base}/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
  }
  preview(file: File, mapping: Record<string, string>) {
    const form = new FormData();
    form.append("file", file);
    form.append("columnStandards", JSON.stringify(mapping));
    return request<LabImportPreview>(`${base}/import/preview`, {
      method: "POST",
      body: form,
    });
  }
  import(importBatchId: string, rows: { rowNumber: number; data: LabWrite }[]) {
    return request<LabImportResult>(`${base}/import`, {
      method: "POST",
      body: JSON.stringify({ importBatchId, rows }),
    });
  }
}
