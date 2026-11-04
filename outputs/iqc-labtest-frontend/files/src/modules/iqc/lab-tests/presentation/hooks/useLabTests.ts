"use client";
import { useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as factories from "../../infrastructure/factories/createLabTestUseCases";
import type { LabWrite } from "../../domain/dtos/LabTest";
import { createDetailEvaluationStandardUseCase } from "../../../evaluation-standards/infrastructure/factories/createEvaluationStandardUseCases";

const root = ["iqc-lab-tests"] as const;
export function useLabTestGrid(page: number, enabled: boolean) {
  const action = useMemo(
    () => factories.createGetMaterialTestGridUseCase(),
    [],
  );
  return useQuery({
    queryKey: [...root, "grid", page],
    queryFn: ({ signal }) => action.execute(page, signal),
    enabled,
  });
}
export function useLabTestDetail(id: string | null, enabled: boolean) {
  const action = useMemo(() => factories.createGetLabTestUseCase(), []);
  return useQuery({
    queryKey: [...root, "detail", id],
    queryFn: ({ signal }) => action.execute(id!, signal),
    enabled: enabled && Boolean(id),
  });
}
export function useLabTestMutations() {
  const client = useQueryClient();
  const create = useMemo(() => factories.createCreateLabTestUseCase(), []);
  const patch = useMemo(() => factories.createPatchLabTestUseCase(), []);
  const remove = useMemo(() => factories.createDeleteLabTestUseCase(), []);
  const invalidate = () => client.invalidateQueries({ queryKey: root });
  const save = useMutation({
    mutationFn: ({ id, data }: { id: string | null; data: LabWrite }) =>
      id ? patch.execute(id, data) : create.execute(data),
    onSuccess: invalidate,
  });
  const deletion = useMutation({
    mutationFn: (id: string) => remove.execute(id),
    onSuccess: invalidate,
  });
  return { save, deletion };
}
export function useLabImport() {
  const client = useQueryClient();
  const previewAction = useMemo(
    () => factories.createPreviewLabTestImportUseCase(),
    [],
  );
  const importAction = useMemo(
    () => factories.createImportLabTestsUseCase(),
    [],
  );
  const preview = useMutation({
    mutationFn: ({
      file,
      mapping,
    }: {
      file: File;
      mapping: Record<string, string>;
    }) => previewAction.execute(file, mapping),
  });
  const commit = useMutation({
    mutationFn: ({
      batchId,
      rows,
    }: {
      batchId: string;
      rows: { rowNumber: number; data: LabWrite }[];
    }) => importAction.execute(batchId, rows),
    onSuccess: () => client.invalidateQueries({ queryKey: root }),
  });
  return { preview, commit };
}
export function useLabImportStandards(ids: string[], enabled: boolean) {
  const action = useMemo(() => createDetailEvaluationStandardUseCase(), []);
  const keys = Array.from(new Set(ids)).sort();
  return useQuery({
    queryKey: [...root, "import-standards", keys],
    queryFn: ({ signal }) =>
      Promise.all(keys.map((id) => action.execute(id, signal))),
    enabled,
  });
}
