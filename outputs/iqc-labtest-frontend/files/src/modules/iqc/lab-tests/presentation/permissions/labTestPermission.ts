import type { MaterialTestRow } from "../../domain/dtos/LabTest";
export function labTestActions(canManage: boolean, row?: MaterialTestRow) {
  return {
    canCreate: canManage,
    canImport: canManage,
    canEdit: canManage && (!row || row.canEdit),
    canDelete: canManage && row?.source === 2 && row.canDelete,
    disabledReasonKey: "iqc.labTests.manageRequired",
  };
}
