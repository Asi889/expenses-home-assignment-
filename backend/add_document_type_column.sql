-- Add DocumentType column to Expenses table
ALTER TABLE "Expenses" 
ADD COLUMN "DocumentType" text DEFAULT 'Receipt';

-- Update existing records to have a default value
UPDATE "Expenses" 
SET "DocumentType" = CASE 
    WHEN "IsReceipt" = true THEN 'Receipt'
    ELSE 'TaxInvoice'
END;

