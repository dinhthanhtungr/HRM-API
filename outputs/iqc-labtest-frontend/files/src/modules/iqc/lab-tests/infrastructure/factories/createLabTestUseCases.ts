import { LabTestApiRepository } from "../api/LabTestApiRepository";
import { GetMaterialTestGridUseCase } from "../../application/use-cases/GetMaterialTestGridUseCase";
import { GetLabTestUseCase } from "../../application/use-cases/GetLabTestUseCase";
import { CreateLabTestUseCase } from "../../application/use-cases/CreateLabTestUseCase";
import { PatchLabTestUseCase } from "../../application/use-cases/PatchLabTestUseCase";
import { DeleteLabTestUseCase } from "../../application/use-cases/DeleteLabTestUseCase";
import { PreviewLabTestImportUseCase } from "../../application/use-cases/PreviewLabTestImportUseCase";
import { ImportLabTestsUseCase } from "../../application/use-cases/ImportLabTestsUseCase";

export const createGetMaterialTestGridUseCase = () =>
  new GetMaterialTestGridUseCase(new LabTestApiRepository());
export const createGetLabTestUseCase = () =>
  new GetLabTestUseCase(new LabTestApiRepository());
export const createCreateLabTestUseCase = () =>
  new CreateLabTestUseCase(new LabTestApiRepository());
export const createPatchLabTestUseCase = () =>
  new PatchLabTestUseCase(new LabTestApiRepository());
export const createDeleteLabTestUseCase = () =>
  new DeleteLabTestUseCase(new LabTestApiRepository());
export const createPreviewLabTestImportUseCase = () =>
  new PreviewLabTestImportUseCase(new LabTestApiRepository());
export const createImportLabTestsUseCase = () =>
  new ImportLabTestsUseCase(new LabTestApiRepository());
