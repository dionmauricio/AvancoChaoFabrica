CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Customers" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "TaxId" text,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Customers" PRIMARY KEY ("Id")
);

CREATE TABLE "ImportBatches" (
    "Id" uuid NOT NULL,
    "FileName" text NOT NULL,
    "ImportedAt" timestamp with time zone NOT NULL,
    "ImportedBy" text,
    "Status" text NOT NULL,
    CONSTRAINT "PK_ImportBatches" PRIMARY KEY ("Id")
);

CREATE TABLE "Suppliers" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "TaxId" text,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
);

CREATE TABLE "Projects" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text,
    "CustomerId" uuid NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Projects" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Projects_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ImportedRows" (
    "Id" uuid NOT NULL,
    "ImportBatchId" uuid NOT NULL,
    "SheetName" text NOT NULL,
    "RowNumber" integer NOT NULL,
    "SourceDataDate" text,
    "SourceRevision" text,
    "RawDataJson" text NOT NULL,
    "Status" text NOT NULL,
    "ValidationMessage" text,
    CONSTRAINT "PK_ImportedRows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ImportedRows_ImportBatches_ImportBatchId" FOREIGN KEY ("ImportBatchId") REFERENCES "ImportBatches" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductionOrders" (
    "Id" uuid NOT NULL,
    "Number" text NOT NULL,
    "Kind" integer NOT NULL,
    "ProjectId" uuid NOT NULL,
    "SupplierId" uuid,
    "Revision" text,
    "DataDate" date,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ProductionOrders" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProductionOrders_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProductionOrders_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "LaborAllocations" (
    "Id" uuid NOT NULL,
    "ProductionOrderId" uuid NOT NULL,
    "PersonOrTeam" text NOT NULL,
    "Role" text,
    "WorkDate" date NOT NULL,
    "Hours" numeric(8,2) NOT NULL,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_LaborAllocations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_LaborAllocations_ProductionOrders_ProductionOrderId" FOREIGN KEY ("ProductionOrderId") REFERENCES "ProductionOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "OrderStageWeights" (
    "Id" uuid NOT NULL,
    "ProductionOrderId" uuid NOT NULL,
    "StageCode" text NOT NULL,
    "StageName" text NOT NULL,
    "Sequence" integer NOT NULL,
    "WeightPercent" numeric(7,4) NOT NULL,
    CONSTRAINT "PK_OrderStageWeights" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrderStageWeights_ProductionOrders_ProductionOrderId" FOREIGN KEY ("ProductionOrderId") REFERENCES "ProductionOrders" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductionItems" (
    "Id" uuid NOT NULL,
    "ProductionOrderId" uuid NOT NULL,
    "SupplierId" uuid,
    "Drawing" text,
    "Package" text,
    "Tag" text,
    "ReferenceTag" text,
    "Mark" text,
    "Description" text,
    "Quantity" numeric(18,3) NOT NULL,
    "Unit" text,
    "UnitWeightKg" numeric(18,3),
    "TotalWeightKg" numeric(18,3),
    "ContractualDate" date,
    "RescheduledDate" date,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_ProductionItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProductionItems_ProductionOrders_ProductionOrderId" FOREIGN KEY ("ProductionOrderId") REFERENCES "ProductionOrders" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProductionItems_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ItemStageProgress" (
    "Id" uuid NOT NULL,
    "ProductionItemId" uuid NOT NULL,
    "StageCode" text NOT NULL,
    "CompletionPercent" numeric(7,4) NOT NULL,
    "CompletedWeightKg" numeric(18,3),
    "ReportDate" date NOT NULL,
    "Notes" text,
    "RecordedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ItemStageProgress" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ItemStageProgress_ProductionItems_ProductionItemId" FOREIGN KEY ("ProductionItemId") REFERENCES "ProductionItems" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ShipmentLines" (
    "Id" uuid NOT NULL,
    "ProductionItemId" uuid NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "WeightKg" numeric(18,3),
    "DocumentNumber" text,
    "DocumentRevision" text,
    "ManifestNumber" text,
    "ShipmentDate" date,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ShipmentLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ShipmentLines_ProductionItems_ProductionItemId" FOREIGN KEY ("ProductionItemId") REFERENCES "ProductionItems" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Customers_Name" ON "Customers" ("Name");

CREATE INDEX "IX_ImportedRows_ImportBatchId" ON "ImportedRows" ("ImportBatchId");

CREATE INDEX "IX_ItemStageProgress_ProductionItemId_StageCode_ReportDate" ON "ItemStageProgress" ("ProductionItemId", "StageCode", "ReportDate");

CREATE INDEX "IX_LaborAllocations_ProductionOrderId" ON "LaborAllocations" ("ProductionOrderId");

CREATE UNIQUE INDEX "IX_OrderStageWeights_ProductionOrderId_StageCode" ON "OrderStageWeights" ("ProductionOrderId", "StageCode");

CREATE INDEX "IX_ProductionItems_ProductionOrderId" ON "ProductionItems" ("ProductionOrderId");

CREATE INDEX "IX_ProductionItems_SupplierId" ON "ProductionItems" ("SupplierId");

CREATE INDEX "IX_ProductionOrders_ProjectId" ON "ProductionOrders" ("ProjectId");

CREATE INDEX "IX_ProductionOrders_SupplierId" ON "ProductionOrders" ("SupplierId");

CREATE INDEX "IX_Projects_CustomerId" ON "Projects" ("CustomerId");

CREATE INDEX "IX_ShipmentLines_ProductionItemId" ON "ShipmentLines" ("ProductionItemId");

CREATE INDEX "IX_Suppliers_Name" ON "Suppliers" ("Name");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261022203815_InitialCreate', '10.0.13');

COMMIT;

