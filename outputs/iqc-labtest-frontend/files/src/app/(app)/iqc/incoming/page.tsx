import { Suspense } from "react";
import { SkeletonScreen } from "@/shared/ui";
import { IncomingWorkspaceScreen } from "@/modules/iqc/incoming/presentation/screens/IncomingWorkspaceScreen";
export default function IncomingPage() {
  return (
    <Suspense fallback={<SkeletonScreen />}>
      <IncomingWorkspaceScreen />
    </Suspense>
  );
}
