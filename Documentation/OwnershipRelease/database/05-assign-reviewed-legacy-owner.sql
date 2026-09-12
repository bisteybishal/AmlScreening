/* OPTIONAL, only for one legacy customer with an UNKNOWN owner. Review first.
   This is not a transfer tool for already-owned records. Execute as a deployment DBA. */
SET XACT_ABORT ON;
DECLARE @Reviewed BIT=0,@AdministratorUserId BIGINT=0,@CustomerId INT=0,@OwnerUserId BIGINT=0;
IF @Reviewed<>1 OR dbo.fn_IsSystemAdmin(@AdministratorUserId)<>1
 THROW 51500,'Review the customer ownership and supply the acting system administrator.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF NOT EXISTS(SELECT 1 FROM dbo.Customers c WITH(UPDLOCK,HOLDLOCK)
   JOIN dbo.Users u ON u.UserId=@OwnerUserId AND u.ClientId=c.ClientId AND u.IsActive=1
   WHERE c.Id=@CustomerId AND c.OwnerUserId IS NULL)
   THROW 51501,'Customer is already owned, missing, or not in this owner account client.',1;
 IF EXISTS(SELECT 1 FROM dbo.AmlTransactions WHERE CustomerId=@CustomerId AND OwnerUserId IS NOT NULL)
   OR EXISTS(SELECT 1 FROM dbo.CustomerKycResults WHERE CustomerId=@CustomerId AND OwnerUserId IS NOT NULL)
   THROW 51502,'Linked records already have owners. Reconcile manually; do not overwrite them.',1;
 UPDATE dbo.Customers SET OwnerUserId=@OwnerUserId WHERE Id=@CustomerId AND OwnerUserId IS NULL;
 UPDATE dbo.AmlTransactions SET OwnerUserId=@OwnerUserId WHERE CustomerId=@CustomerId AND OwnerUserId IS NULL;
 UPDATE dbo.CustomerKycResults SET OwnerUserId=@OwnerUserId WHERE CustomerId=@CustomerId AND OwnerUserId IS NULL;
 INSERT dbo.OwnershipConfigurationAudit(ChangedByUserId,ChangeType,TargetUserId,Details)
 VALUES(@AdministratorUserId,N'Assign legacy owner',@OwnerUserId,CONCAT(N'Reviewed customer ID ',@CustomerId,N' and linked history.'));
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
