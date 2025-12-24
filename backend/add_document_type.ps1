# PowerShell script to add DocumentType column to PostgreSQL
# Run this if the automatic column addition doesn't work

$connectionString = "Host=localhost;Port=5432;Database=invoiceexpensesystem;Username=postgres;Password=asafmarom"

Write-Host "Adding DocumentType column to Expenses table..."

# You can run this SQL manually in pgAdmin or psql:
Write-Host @"
Run this SQL in pgAdmin or psql:

ALTER TABLE "Expenses" ADD COLUMN IF NOT EXISTS "DocumentType" text DEFAULT 'Receipt';

UPDATE "Expenses" 
SET "DocumentType" = CASE 
    WHEN "IsReceipt" = true THEN 'Receipt'
    ELSE 'TaxInvoice'
END
WHERE "DocumentType" IS NULL;
"@

