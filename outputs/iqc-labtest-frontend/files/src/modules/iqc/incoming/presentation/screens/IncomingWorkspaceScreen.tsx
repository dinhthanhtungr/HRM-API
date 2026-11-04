"use client";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useI18n } from "@/core/i18n";
import { PageTabs } from "@/shared/ui";
import { IncomingListScreen } from "./IncomingListScreen";
import { LabTestGridScreen } from "../../../lab-tests/presentation/screens/LabTestGridScreen";

export function IncomingWorkspaceScreen() {
  const { t } = useI18n();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const active =
    params.get("tab") === "mechanical-tests" ? "mechanical-tests" : "incoming";
  return (
    <>
      <PageTabs
        activeKey={active}
        items={[
          { key: "incoming", label: t("iqc.incoming.title") },
          { key: "mechanical-tests", label: t("iqc.labTests.title") },
        ]}
        onChange={(key) => {
          const next = new URLSearchParams(params.toString());
          if (key === "mechanical-tests") next.set("tab", key);
          else next.delete("tab");
          router.push(`${pathname}${next.size ? `?${next}` : ""}`, {
            scroll: false,
          });
        }}
      />
      {active === "incoming" ? <IncomingListScreen /> : <LabTestGridScreen />}
    </>
  );
}
