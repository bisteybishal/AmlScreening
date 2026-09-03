/*
    Manual full-backup template for AMLScreening.

    Before execution:
      1. Confirm the database name.
      2. Change @BackupDirectory to an approved SQL Server backup directory.
      3. Confirm the SQL Server service account has write permission.

    This is COPY_ONLY so an ad-hoc deployment backup does not disturb the
    scheduled differential backup chain.
*/

USE master;
GO

DECLARE @DatabaseName SYSNAME = N'AMLScreening';
DECLARE @BackupDirectory NVARCHAR(260) = N'D:\SqlBackups\AMLScreening\';
DECLARE @Timestamp CHAR(15) =
    CONVERT(CHAR(8), GETDATE(), 112) + '_' +
    REPLACE(CONVERT(CHAR(8), GETDATE(), 108), ':', '');
DECLARE @BackupFile NVARCHAR(4000) =
    @BackupDirectory + @DatabaseName + N'_FULL_' + @Timestamp + N'.bak';
DECLARE @Sql NVARCHAR(MAX);

IF DB_ID(@DatabaseName) IS NULL
    THROW 51000, 'The requested database does not exist.', 1;

SET @Sql = N'BACKUP DATABASE ' + QUOTENAME(@DatabaseName) +
    N' TO DISK = @File WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 10;';

EXEC sys.sp_executesql
    @Sql,
    N'@File NVARCHAR(4000)',
    @File = @BackupFile;

RESTORE VERIFYONLY
FROM DISK = @BackupFile
WITH CHECKSUM;

SELECT @DatabaseName AS DatabaseName,
       @BackupFile AS BackupFile,
       SYSDATETIMEOFFSET() AS VerifiedAt;
GO
