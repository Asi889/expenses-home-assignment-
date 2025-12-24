ALTER TABLE "Expenses" ADD COLUMN IF NOT EXISTS "DocumentType" text DEFAULT 'Receipt';
UPDATE "Expenses" SET "DocumentType" = CASE WHEN "IsReceipt" = true THEN 'Receipt' ELSE 'TaxInvoice' END WHERE "DocumentType" IS NULL;
