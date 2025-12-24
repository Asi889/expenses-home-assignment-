-- Create Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Email" VARCHAR(255) NOT NULL UNIQUE,
    "PasswordHash" TEXT NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create index on Email
CREATE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");

-- Create Expenses table
CREATE TABLE IF NOT EXISTS "Expenses" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "BusinessName" VARCHAR(255) NOT NULL,
    "TransactionDate" TIMESTAMP NOT NULL,
    "AmountBeforeVat" DECIMAL(18,2) NOT NULL,
    "AmountAfterVat" DECIMAL(18,2) NOT NULL,
    "InvoiceNumber" VARCHAR(255),
    "Category" INTEGER NOT NULL DEFAULT 5,
    "ServiceProvided" TEXT,
    "TaxId" VARCHAR(255),
    "IsReceipt" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_Expenses_Users_UserId" FOREIGN KEY ("UserId") 
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Create index on UserId
CREATE INDEX IF NOT EXISTS "IX_Expenses_UserId" ON "Expenses" ("UserId");

