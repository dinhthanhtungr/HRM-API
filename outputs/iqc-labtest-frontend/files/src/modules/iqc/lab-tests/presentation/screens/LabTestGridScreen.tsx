"use client";
import { useState } from "react";
import { useAppRouter } from "@/shared/hooks/navigation/useAppRouter";
import { useAuthorization } from "@/core/auth/authorization";
import { useI18n } from "@/core/i18n";
import { useUrlObjectQueryState } from "@/shared/hooks/pageShells/useUrlQueryState";
import {
  AppButton,
  AppDialog,
  AppDialogContent,
  AppDialogTitle,
  AppDialogFooter,
  AppNotification,
  ListPageShell,
  PageBreadcrumb,
  PaginationBar,
} from "@/shared/ui";
import { buildIncomingPermission } from "../../../incoming/presentation/permissions/incomingPermission";
import { MaterialTestTable } from "../components/MaterialTestTable";
import { LabTestEditor } from "../components/LabTestEditor";
import { LabTestImportDialog } from "../components/LabTestImportDialog";
import {
  useLabTestDetail,
  useLabTestGrid,
  useLabTestMutations,
} from "../hooks/useLabTests";
import { labTestActions } from "../permissions/labTestPermission";
import type { MaterialTestRow } from "../../domain/dtos/LabTest";
import styles from "../components/LabTests.module.css";

const defaults = { testPage: "" };
export function LabTestGridScreen() {
  const { t } = useI18n();
  const router = useAppRouter();
  const auth = useAuthorization();
  const permission = buildIncomingPermission(auth.roles, auth.isReady);
  const actions = labTestActions(permission.canManage);
  const [raw, setRaw] = useUrlObjectQueryState(defaults);
  const pageNumber =
    /^\d+$/.test(raw.testPage) &&
    Number(raw.testPage) >= 1 &&
    Number(raw.testPage) <= 71582788
      ? Number(raw.testPage)
      : 1;
  const grid = useLabTestGrid(pageNumber, permission.canView);
  const [editor, setEditor] = useState<{
    id: string | null;
    canEdit: boolean;
  } | null>(null);
  const [deleting, setDeleting] = useState<MaterialTestRow | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const detail = useLabTestDetail(
    editor?.id ?? null,
    Boolean(editor) && permission.canView,
  );
  const mutations = useLabTestMutations();

  return (
    <ListPageShell
      title={t("iqc.labTests.title")}
      breadcrumb={<PageBreadcrumb />}
      description={t("iqc.labTests.description")}
      error={grid.error?.message}
      actions={
        <div className={styles.actions}>
          <AppButton
            variant="outline"
            disabled={grid.isFetching}
            onClick={() => void grid.refetch()}
          >
            {t("iqc.labTests.refresh")}
          </AppButton>
          <AppButton
            variant="outline"
            disabled={!actions.canImport}
            disabledReason={t(actions.disabledReasonKey)}
            onClick={() => {
              if (actions.canImport) setImportOpen(true);
            }}
          >
            {t("iqc.labTests.import")}
          </AppButton>
          <AppButton
            disabled={!actions.canCreate}
            disabledReason={t(actions.disabledReasonKey)}
            onClick={() => {
              if (actions.canCreate) {
                mutations.save.reset();
                setEditor({ id: null, canEdit: true });
              }
            }}
          >
            {t("iqc.labTests.create")}
          </AppButton>
        </div>
      }
      pagination={
        <PaginationBar
          pageNumber={pageNumber}
          totalPages={grid.data?.page.totalPages ?? 1}
          hasPreviousPage={pageNumber > 1}
          hasNextPage={grid.data?.page.hasNextPage ?? false}
          onPageChange={(page) =>
            setRaw({ testPage: String(page) }, { replace: false })
          }
        />
      }
    >
      {auth.isReady && !permission.canView ? (
        <AppNotification tone="danger" message={t("iqc.incoming.forbidden")} />
      ) : (
        <MaterialTestTable
          data={grid.data}
          loading={!auth.isReady || grid.isFetching}
          canManage={permission.canManage}
          onOpen={(row) => {
            if (row.source === 1 && row.voucherDetailId)
              router.push(`/iqc/incoming/${row.voucherDetailId}`);
            else {
              mutations.save.reset();
              setEditor({
                id: row.id,
                canEdit: labTestActions(permission.canManage, row).canEdit,
              });
            }
          }}
          onDelete={(row) => {
            if (labTestActions(permission.canManage, row).canDelete) {
              mutations.deletion.reset();
              setDeleting(row);
            }
          }}
        />
      )}
      {editor && editor.id && detail.isLoading && (
        <p role="status">{t("iqc.labTests.loading")}</p>
      )}
      {editor && detail.error && (
        <AppNotification tone="danger" message={detail.error.message} />
      )}
      {editor && (!editor.id || detail.data) && (
        <LabTestEditor
          key={editor.id ?? "new"}
          detail={editor.id ? detail.data : undefined}
          canEdit={editor.canEdit && permission.canManage}
          pending={mutations.save.isPending}
          error={mutations.save.error?.message}
          onClose={() => setEditor(null)}
          onSave={async (data) => {
            if (!editor.canEdit || !permission.canManage) return;
            await mutations.save.mutateAsync({ id: editor.id, data });
            setEditor(null);
          }}
        />
      )}
      {deleting && (
        <AppDialog
          open
          onOpenChange={(open) => {
            if (!open && !mutations.deletion.isPending) setDeleting(null);
          }}
        >
          <AppDialogContent>
            <AppDialogTitle>{t("iqc.labTests.delete")}</AppDialogTitle>
            <p>
              {t("iqc.labTests.deleteConfirm", {
                code: deleting.materialCode ?? deleting.sampleName,
              })}
            </p>
            {mutations.deletion.error && (
              <AppNotification
                tone="danger"
                message={mutations.deletion.error.message}
              />
            )}
            <AppDialogFooter>
              <AppButton
                variant="outline"
                disabled={mutations.deletion.isPending}
                onClick={() => setDeleting(null)}
              >
                {t("iqc.labTests.cancel")}
              </AppButton>
              <AppButton
                variant="destructive"
                disabled={mutations.deletion.isPending}
                onClick={() => {
                  if (
                    !labTestActions(permission.canManage, deleting).canDelete ||
                    mutations.deletion.isPending
                  )
                    return;
                  void mutations.deletion
                    .mutateAsync(deleting.id)
                    .then(() => setDeleting(null))
                    .catch(() => {});
                }}
              >
                {t(
                  mutations.deletion.isPending
                    ? "iqc.labTests.saving"
                    : "iqc.labTests.delete",
                )}
              </AppButton>
            </AppDialogFooter>
          </AppDialogContent>
        </AppDialog>
      )}
      {importOpen && (
        <LabTestImportDialog
          canImport={actions.canImport}
          onClose={() => setImportOpen(false)}
        />
      )}
    </ListPageShell>
  );
}
