/* READ ONLY. Run on the exact database used by BOTH portal and API before deployment.
   Save results and a tested backup. Never paste connection strings/passwords into reports.
   Read committed, no NOLOCK: an inconsistent ownership inventory is not acceptable. */
SET NOCOUNT ON;
SET LOCK_TIMEOUT 10000;

SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName,
       SERVERPROPERTY('ProductVersion') AS SqlVersion;

SELECT wanted.TableName, OBJECT_ID(N'dbo.' + wanted.TableName,N'U') AS ObjectId
FROM (VALUES (N'Users'),(N'Clients'),(N'AmlRecords'),(N'Customers'),
             (N'AmlTransactions'),(N'CustomerKycResults'),(N'AmlSources'),
             (N'ApiApplications'),(N'Permissions'),(N'UserPermissions')) wanted(TableName);

SELECT t.name AS TableName,c.name AS ColumnName,ty.name AS SqlType,
       c.max_length,c.is_nullable,c.is_identity
FROM sys.tables t
JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.name IN (N'Users',N'Clients',N'AmlRecords',N'Customers',N'AmlTransactions',
                 N'CustomerKycResults',N'AmlSources',N'ApiApplications')
ORDER BY t.name,c.column_id;

SELECT i.name AS IndexName,t.name AS TableName,i.is_unique,i.is_primary_key,
       i.is_unique_constraint,i.filter_definition,c.name AS ColumnName,ic.key_ordinal
FROM sys.indexes i
JOIN sys.tables t ON t.object_id=i.object_id
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE t.name IN(N'AmlRecords',N'Customers',N'AmlTransactions')
ORDER BY t.name,i.name,ic.key_ordinal;

SELECT fk.name AS ForeignKeyName,OBJECT_NAME(fk.parent_object_id) AS ReferencingTable,
       OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id IN(OBJECT_ID(N'dbo.Customers'),OBJECT_ID(N'dbo.AmlRecords'));

SELECT u.UserId,u.Username,u.ClientId,u.IsActive,r.RoleName
FROM dbo.Users u
LEFT JOIN dbo.UserRoles ur ON ur.UserId=u.UserId
LEFT JOIN dbo.Roles r ON r.RoleId=ur.RoleId
ORDER BY u.UserId; -- Deliberately never select PasswordHash/session/credential fields.

SELECT ClientId,OwnerUserId,IsShared,SourceName,COUNT_BIG(*) AS Records
FROM dbo.AmlRecords
GROUP BY ClientId,OwnerUserId,IsShared,SourceName;
SELECT SourceName,ReferenceNo,COUNT_BIG(*) AS SharedDuplicates
FROM dbo.AmlRecords WHERE IsShared=1
GROUP BY SourceName,ReferenceNo HAVING COUNT_BIG(*)>1;
SELECT COUNT_BIG(*) AS CustomersNeedingOwnershipReview FROM dbo.Customers;
SELECT COUNT_BIG(*) AS TransactionsNeedingOwnershipReview FROM dbo.AmlTransactions;

SELECT OBJECT_NAME(object_id) AS ProcedureName,definition
FROM sys.sql_modules
WHERE object_id IN(OBJECT_ID(N'dbo.spa_AML_Record_BulkInsert'),
                   OBJECT_ID(N'dbo.spa_CustomerScreening_Check'));

/* If this times out, stop. Resolve the blocking session with your DBA; do not kill
   sessions or use NOLOCK to make a deployment appear safe. */
