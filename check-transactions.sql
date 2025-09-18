CREATE EXTENSION IF NOT EXISTS vector;


CREATE TABLE "BankTemplates" (
    "Id" uuid NOT NULL,
    "BankName" character varying(255) NOT NULL,
    "Country" character varying(100) NOT NULL,
    "FileFormat" character varying(50) NOT NULL,
    "TemplatePattern" text NOT NULL,
    "SuccessCount" integer NOT NULL,
    "FailureCount" integer NOT NULL,
    "ConfidenceScore" double precision NOT NULL,
    "LastUsed" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BankTemplates" PRIMARY KEY ("Id")
);


CREATE TABLE "ImportParsingCache" (
    "Id" uuid NOT NULL,
    "FileHash" character varying(64) NOT NULL,
    "BankName" character varying(255) NOT NULL,
    "ParsedStructure" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ImportParsingCache" PRIMARY KEY ("Id")
);


CREATE TABLE "Merchants" (
    "Id" uuid NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Category" character varying(100),
    "Website" character varying(500),
    "LogoUrl" character varying(500),
    "Aliases" text[] NOT NULL,
    "EnrichedData" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Merchants" PRIMARY KEY ("Id")
);


CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(256) NOT NULL,
    "FirstName" character varying(100) NOT NULL,
    "LastName" character varying(100) NOT NULL,
    "PasswordHash" text,
    "GoogleId" text,
    "Currency" character varying(3) NOT NULL,
    "Country" character varying(2) NOT NULL,
    "TimeZone" character varying(50) NOT NULL,
    "SubscriptionTier" integer NOT NULL,
    "SubscriptionExpiresAt" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);


CREATE TABLE "Accounts" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Type" integer NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Institution" character varying(200),
    "AccountNumber" character varying(50),
    "Balance" numeric(18,2) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Accounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AuditEvents" (
    "Id" uuid NOT NULL,
    "UserId" uuid,
    "EventType" character varying(100) NOT NULL,
    "EntityType" character varying(100) NOT NULL,
    "EntityId" uuid,
    "OldValues" text,
    "NewValues" text,
    "IpAddress" character varying(45),
    "UserAgent" character varying(500),
    "AdditionalData" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AuditEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AuditEvents_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
);


CREATE TABLE "Categories" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ParentCategoryId" uuid,
    "Name" character varying(100) NOT NULL,
    "Icon" character varying(50),
    "Color" character varying(7),
    "Type" integer NOT NULL,
    "BudgetAmount" numeric(18,2),
    "BudgetPeriod" integer,
    "IsSystem" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Categories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Categories_Categories_ParentCategoryId" FOREIGN KEY ("ParentCategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Categories_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Goals" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(500),
    "Type" integer NOT NULL,
    "TargetAmount" numeric(18,2) NOT NULL,
    "CurrentAmount" numeric(18,2) NOT NULL,
    "TargetDate" timestamp with time zone NOT NULL,
    "CategoryScope" text,
    "AccountScope" text,
    "Status" integer NOT NULL,
    "Icon" character varying(50),
    "Color" character varying(7),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Goals" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Goals_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ImportedFiles" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "FileName" character varying(500) NOT NULL,
    "FileType" character varying(50) NOT NULL,
    "FileSize" bigint NOT NULL,
    "BlobUrl" character varying(1000),
    "Status" integer NOT NULL,
    "TotalRows" integer NOT NULL,
    "ProcessedRows" integer NOT NULL,
    "ImportedTransactions" integer NOT NULL,
    "DuplicateTransactions" integer NOT NULL,
    "FailedRows" integer NOT NULL,
    "ErrorDetails" text,
    "BankTemplate" text,
    "DetectedBankName" character varying(255),
    "DetectedCountry" character varying(100),
    "DetectedFormat" character varying(50),
    "TemplateVersion" character varying(50),
    "ParsingMetadata" text,
    "AICost" numeric(10,4),
    "IsProcessedSynchronously" boolean NOT NULL,
    "ProcessingStartedAt" timestamp with time zone,
    "ProcessingCompletedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ImportedFiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ImportedFiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "RecurringSeries" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "MerchantId" uuid,
    "Name" character varying(200) NOT NULL,
    "RecurrenceType" integer NOT NULL,
    "RecurrenceInterval" integer NOT NULL,
    "RecurrenceDays" text,
    "ExpectedAmount" numeric(18,2) NOT NULL,
    "AmountTolerance" numeric(5,2) NOT NULL,
    "NextExpectedDate" timestamp with time zone,
    "LastOccurrence" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "SubscriptionType" integer,
    "SubscriptionEndDate" timestamp with time zone,
    "Metadata" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RecurringSeries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RecurringSeries_Merchants_MerchantId" FOREIGN KEY ("MerchantId") REFERENCES "Merchants" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_RecurringSeries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Rules" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(500),
    "Type" integer NOT NULL,
    "Conditions" text NOT NULL,
    "Actions" text NOT NULL,
    "Priority" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "TimesApplied" integer NOT NULL,
    "LastApplied" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CategoryId" uuid,
    CONSTRAINT "PK_Rules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Rules_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id"),
    CONSTRAINT "FK_Rules_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Transactions" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "TransactionDate" timestamp with time zone NOT NULL,
    "PostedDate" timestamp with time zone NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Type" integer NOT NULL,
    "OriginalMerchant" character varying(500) NOT NULL,
    "NormalizedMerchant" character varying(500),
    "MerchantId" uuid,
    "CategoryId" uuid,
    "Description" character varying(1000),
    "Notes" character varying(2000),
    "IsPending" boolean NOT NULL,
    "IsRecurring" boolean NOT NULL,
    "RecurringSeriesId" uuid,
    "IsTransfer" boolean NOT NULL,
    "TransferPairId" uuid,
    "IsSplit" boolean NOT NULL,
    "ParentTransactionId" uuid,
    "Tags" character varying(500),
    "ImportHash" character varying(64),
    "ImportedFileId" uuid,
    "Metadata" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Transactions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Transactions_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Transactions_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Transactions_ImportedFiles_ImportedFileId" FOREIGN KEY ("ImportedFileId") REFERENCES "ImportedFiles" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Transactions_Merchants_MerchantId" FOREIGN KEY ("MerchantId") REFERENCES "Merchants" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Transactions_RecurringSeries_RecurringSeriesId" FOREIGN KEY ("RecurringSeriesId") REFERENCES "RecurringSeries" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Transactions_Transactions_ParentTransactionId" FOREIGN KEY ("ParentTransactionId") REFERENCES "Transactions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Transactions_Transactions_TransferPairId" FOREIGN KEY ("TransferPairId") REFERENCES "Transactions" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Transactions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_Accounts_UserId" ON "Accounts" ("UserId");


CREATE INDEX "IX_AuditEvents_UserId_CreatedAt" ON "AuditEvents" ("UserId", "CreatedAt");


CREATE INDEX "IX_BankTemplates_Lookup" ON "BankTemplates" ("BankName", "Country", "FileFormat");


CREATE INDEX "IX_Categories_ParentCategoryId" ON "Categories" ("ParentCategoryId");


CREATE INDEX "IX_Categories_UserId_Name" ON "Categories" ("UserId", "Name");


CREATE INDEX "IX_Goals_UserId" ON "Goals" ("UserId");


CREATE INDEX "IX_ImportedFiles_UserId" ON "ImportedFiles" ("UserId");


CREATE UNIQUE INDEX "IX_ImportParsingCache_FileHash" ON "ImportParsingCache" ("FileHash");


CREATE INDEX "IX_Merchants_DisplayName" ON "Merchants" ("DisplayName");


CREATE INDEX "IX_RecurringSeries_MerchantId" ON "RecurringSeries" ("MerchantId");


CREATE INDEX "IX_RecurringSeries_UserId" ON "RecurringSeries" ("UserId");


CREATE INDEX "IX_Rules_CategoryId" ON "Rules" ("CategoryId");


CREATE INDEX "IX_Rules_UserId_Priority" ON "Rules" ("UserId", "Priority");


CREATE INDEX "IX_Transactions_AccountId" ON "Transactions" ("AccountId");


CREATE INDEX "IX_Transactions_CategoryId" ON "Transactions" ("CategoryId");


CREATE INDEX "IX_Transactions_ImportedFileId" ON "Transactions" ("ImportedFileId");


CREATE INDEX "IX_Transactions_ImportHash" ON "Transactions" ("ImportHash");


CREATE INDEX "IX_Transactions_MerchantId" ON "Transactions" ("MerchantId");


CREATE INDEX "IX_Transactions_ParentTransactionId" ON "Transactions" ("ParentTransactionId");


CREATE INDEX "IX_Transactions_RecurringSeriesId" ON "Transactions" ("RecurringSeriesId");


CREATE UNIQUE INDEX "IX_Transactions_TransferPairId" ON "Transactions" ("TransferPairId");


CREATE INDEX "IX_Transactions_UserId_TransactionDate" ON "Transactions" ("UserId", "TransactionDate");


CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");


