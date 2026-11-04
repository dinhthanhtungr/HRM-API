-- Prepared for review only; not executed. Apply before enabling PO documents.
BEGIN;
CREATE TABLE "Orders"."PurchaseOrderDocuments" (
 "PurchaseOrderDocumentId" uuid PRIMARY KEY,
 "PurchaseOrderId" uuid NOT NULL REFERENCES "Orders"."PurchaseOrders"("PurchaseOrderId") ON DELETE RESTRICT,
 "DocumentType" integer NOT NULL CHECK ("DocumentType" BETWEEN 0 AND 12),
 "AttachmentCollectionId" uuid REFERENCES "Attachment"."AttachmentCollection"("AttachmentCollectionID") ON DELETE RESTRICT,
 "DocumentCode" text, "DocumentName" text, "Note" text,
 "IsReceived" boolean NOT NULL, "ReceivedDate" timestamp without time zone,
 "VerifiedBy" uuid REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 "VerifiedDate" timestamp without time zone,
 "CreatedDate" timestamp without time zone NOT NULL,
 "CreatedBy" uuid NOT NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 "UpdatedDate" timestamp without time zone,
 "UpdatedBy" uuid REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 "IsActive" boolean NOT NULL
);
CREATE INDEX "IX_PurchaseOrderDocuments_PurchaseOrderId" ON "Orders"."PurchaseOrderDocuments"("PurchaseOrderId");
CREATE INDEX "IX_PurchaseOrderDocuments_AttachmentCollectionId" ON "Orders"."PurchaseOrderDocuments"("AttachmentCollectionId");
CREATE INDEX "IX_PurchaseOrderDocuments_CreatedBy" ON "Orders"."PurchaseOrderDocuments"("CreatedBy");
CREATE INDEX "IX_PurchaseOrderDocuments_UpdatedBy" ON "Orders"."PurchaseOrderDocuments"("UpdatedBy");
CREATE INDEX "IX_PurchaseOrderDocuments_VerifiedBy" ON "Orders"."PurchaseOrderDocuments"("VerifiedBy");
COMMIT;
