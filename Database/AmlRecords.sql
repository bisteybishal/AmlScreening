IF OBJECT_ID(N'dbo.AmlRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AmlRecords
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AmlRecords PRIMARY KEY,
        RecordType    NVARCHAR(50)  NOT NULL,
        FullName      NVARCHAR(200) NOT NULL,
        DateOfBirth   DATE          NULL,
        Country       NVARCHAR(100) NOT NULL,
        Nationality   NVARCHAR(100) NOT NULL CONSTRAINT DF_AmlRecords_Nationality DEFAULT N'',
        ReferenceNo   NVARCHAR(100) NOT NULL,
        SourceName    NVARCHAR(150) NOT NULL,
        Remarks       NVARCHAR(1000) NOT NULL CONSTRAINT DF_AmlRecords_Remarks DEFAULT N'',
        IsActive      BIT           NOT NULL CONSTRAINT DF_AmlRecords_IsActive DEFAULT 1,
        CreatedDate   DATETIME2(0)  NOT NULL CONSTRAINT DF_AmlRecords_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy     INT           NULL,
        UpdatedDate   DATETIME2(0)  NULL,
        UpdatedBy     INT           NULL,
        CONSTRAINT UQ_AmlRecords_ReferenceNo UNIQUE (ReferenceNo)
    );

    CREATE INDEX IX_AmlRecords_FullName ON dbo.AmlRecords (FullName);
    CREATE INDEX IX_AmlRecords_RecordType_IsActive ON dbo.AmlRecords (RecordType, IsActive);
END;
