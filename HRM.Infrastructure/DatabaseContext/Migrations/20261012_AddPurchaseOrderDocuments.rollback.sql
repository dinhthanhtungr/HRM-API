-- Review and back up PO documents before rollback; this removes the new table.
BEGIN;
DROP TABLE "Orders"."PurchaseOrderDocuments";
COMMIT;
