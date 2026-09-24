using AmlScreening.Application.Interface;
using AmlScreening.Domain.Common;
using AmlScreening.Domain.Entities;
using AmlScreening.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmlScreening.Infrastructure.Data;

public sealed class AmlScreeningDbContext
    : DbContext
{
    private readonly ICurrentUserContext
        _currentUser;


    public AmlScreeningDbContext(
        DbContextOptions<AmlScreeningDbContext> options,
        ICurrentUserContext currentUser)
        : base(options)
    {
        _currentUser =
            currentUser;
    }


    // =========================================================
    // CURRENT PARTNER
    // =========================================================

    public int CurrentPartnerId =>
        _currentUser.IsAuthenticated
        &&
        _currentUser.UserId > 0
        &&
        _currentUser.PartnerId > 0

            ? _currentUser.PartnerId

            : -1;

    public bool CurrentUserIsAdmin =>
    _currentUser.IsAuthenticated &&
    _currentUser.UserId > 0 &&
    _currentUser.IsAdmin;
    // =========================================================
    // AML
    // =========================================================

    public DbSet<AmlRecord> AmlRecords =>
        Set<AmlRecord>();


    // =========================================================
    // COMPLIANCE / WORKFLOW
    // =========================================================

    public DbSet<ComplianceRule> ComplianceRules =>
        Set<ComplianceRule>();


    public DbSet<Customer> Customers =>
        Set<Customer>();


    public DbSet<AmlTransaction> Transactions =>
        Set<AmlTransaction>();


    public DbSet<ScreeningMatch> ScreeningMatches =>
        Set<ScreeningMatch>();


    public DbSet<RiskAssessment> RiskAssessments =>
        Set<RiskAssessment>();


    public DbSet<AmlDecision> AmlDecisions =>
        Set<AmlDecision>();


    public DbSet<ComplianceAlert> ComplianceAlerts =>
        Set<ComplianceAlert>();


    public DbSet<ComplianceCase> ComplianceCases =>
        Set<ComplianceCase>();


    // =========================================================
    // RISK SCORING
    // =========================================================

    public DbSet<RiskFactor> RiskFactors =>
        Set<RiskFactor>();


    public DbSet<RiskFactorRule> RiskFactorRules =>
        Set<RiskFactorRule>();


    public DbSet<RiskBand> RiskBands =>
        Set<RiskBand>();


    public DbSet<RiskModel> RiskModels =>
        Set<RiskModel>();


    public DbSet<RiskAssessmentFactor>
        RiskAssessmentFactors =>
            Set<RiskAssessmentFactor>();


    // =========================================================
    // AUDIT
    // =========================================================

    public DbSet<AuditLog> AuditLogs =>
        Set<AuditLog>();


    // =========================================================
    // MODEL
    // =========================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder);


        ConfigureAmlRecord(
            modelBuilder);


        ConfigureComplianceRule(
            modelBuilder);


        ConfigureWorkflow(
            modelBuilder);


        ConfigureRiskScoring(
            modelBuilder);


        ConfigureAuditLog(
            modelBuilder);
    }


    // =========================================================
    // AML RECORD
    // =========================================================
    private void ConfigureAmlRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AmlRecord>(entity =>
        {
            entity.ToTable("AmlRecords", "dbo");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.PartnerId)
                .IsRequired();

            entity.Property(x => x.BranchId)
                .IsRequired();

            entity.Property(x => x.RecordType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.FullName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Country)
                .HasMaxLength(300);

            entity.Property(x => x.Nationality)
                .HasMaxLength(100);

            entity.Property(x => x.ReferenceNo)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.SourceName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.NormalizedName)
                .HasMaxLength(250);

            entity.Property(x => x.NamePrefix)
                .HasMaxLength(10);

            entity.Property(x => x.PhotoContentType)
                .HasMaxLength(100);

            entity.Property(x => x.IsGlobal)
                .IsRequired()
                .HasDefaultValue(false);

            ConfigureCommonFields(entity);

            // A global source reference is shared across partners.
            entity.HasIndex(x => new
            {
                x.SourceName,
                x.ReferenceNo
            })
                .IsUnique()
                .HasDatabaseName("UX_AmlRecords_Global_Source_Reference")
                .HasFilter("[IsGlobal] = 1 AND [DeleteFg] = 'N'");

            // Private references are unique within their owning partner.
            entity.HasIndex(x => new
            {
                x.PartnerId,
                x.SourceName,
                x.ReferenceNo
            })
                .IsUnique()
                .HasDatabaseName("UX_AmlRecords_Partner_Source_Reference")
                .HasFilter("[IsGlobal] = 0 AND [DeleteFg] = 'N'");

            entity.HasIndex(x => new
            {
                x.PartnerId,
                x.CreatedDate
            })
                .HasDatabaseName("IX_AmlRecords_Partner_CreatedDate");

            entity.HasIndex(x => new
            {
                x.PartnerId,
                x.NormalizedName
            })
                .HasDatabaseName("IX_AmlRecords_Partner_NormalizedName");

            entity.HasIndex(x => new
            {
                x.SourceName,
                x.NormalizedName
            })
                .HasDatabaseName("IX_AmlRecords_Source_NormalizedName");

            entity.HasQueryFilter(
                "SoftDeleteFilter",
                x => x.DeleteFg == "N");

            // PartnerId and BranchId are never used as global flags.
            // All branches/users within a partner share private records.
            entity.HasQueryFilter(
                "PartnerFilter",
                x => CurrentPartnerId > 0 &&
                     (
                      CurrentUserIsAdmin ||
                         x.IsGlobal ||
                         x.PartnerId == CurrentPartnerId
                     ));
        });
    }


    // =========================================================
    // COMPLIANCE RULE
    // =========================================================

    private void ConfigureComplianceRule(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceRule>(
            entity =>
            {
                entity.ToTable(
                    "ComplianceRules",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.RuleCode)
                    .HasMaxLength(50)
                    .IsRequired();


                entity.Property(
                        x => x.RuleName)
                    .HasMaxLength(150)
                    .IsRequired();


                entity.Property(
                        x => x.RuleType)
                    .HasMaxLength(50)
                    .IsRequired();


                entity.Property(
                        x => x.Currency)
                    .HasMaxLength(3);


                entity.Property(
                        x => x.ThresholdAmount)
                    .HasPrecision(18, 2);


                entity.Property(
                        x => x.Action)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovalStatus)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovedBy)
                    .HasMaxLength(100);


                /*
                 * One RuleCode can have
                 * multiple versions.
                 */

                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.RuleCode,
                            x.RuleVersion
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_ComplianceRules_Partner_Rule_Version")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });
    }


    // =========================================================
    // CUSTOMER / TRANSACTION / SCREENING
    // =========================================================

    private void ConfigureWorkflow(
        ModelBuilder modelBuilder)
    {
        // -----------------------------------------------------
        // CUSTOMER
        // -----------------------------------------------------

        modelBuilder.Entity<Customer>(
            entity =>
            {
                entity.ToTable(
                    "Customers",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.CustomerReference
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_Customers_Partner_CustomerReference")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                entity.Property(
                        x => x.CustomerReference)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.FullName)
                    .HasMaxLength(200)
                    .IsRequired();


                entity.Property(
                        x => x.Country)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Nationality)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Occupation)
                    .HasMaxLength(150);


                entity.Property(
                        x => x.SourceOfFunds)
                    .HasMaxLength(150);


                entity.Property(
                        x => x.RiskLevel)
                    .HasMaxLength(20);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // AML TRANSACTION
        // -----------------------------------------------------

        modelBuilder.Entity<AmlTransaction>(
            entity =>
            {
                entity.ToTable(
                    "AmlTransactions",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.TransactionReference
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_AmlTransactions_Partner_Reference")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                entity.Property(
                        x => x.TransactionReference)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.SenderName)
                    .HasMaxLength(200)
                    .IsRequired();


                entity.Property(
                        x => x.ReceiverName)
                    .HasMaxLength(200)
                    .IsRequired();


                entity.Property(
                        x => x.Amount)
                    .HasPrecision(18, 2);


                entity.Property(
                        x => x.Currency)
                    .HasMaxLength(3);


                entity.Property(
                        x => x.OriginCountry)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.DestinationCountry)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Channel)
                    .HasMaxLength(50);


                entity.Property(
                        x => x.Status)
                    .HasMaxLength(30);


                entity.Property(
                        x => x.CorrelationId)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Decision)
                    .HasMaxLength(20);


                entity.Property(
                        x => x.RiskLevel)
                    .HasMaxLength(20);


                entity.Property(
                        x => x.ScreeningStatus)
                    .HasMaxLength(30);


                entity.Property(
                        x => x.DecisionReasons)
                    .HasMaxLength(2000);


                entity.Property(
                        x => x.PurposeCode)
                    .HasMaxLength(50);


                entity.Property(
                        x => x.RelationshipCode)
                    .HasMaxLength(50);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // SCREENING MATCH
        // -----------------------------------------------------

        modelBuilder.Entity<ScreeningMatch>(
            entity =>
            {
                entity.ToTable(
                    "ScreeningMatches",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.SubjectName)
                    .HasMaxLength(200);


                entity.Property(
                        x => x.MatchedName)
                    .HasMaxLength(200);


                entity.Property(
                        x => x.MatchScore)
                    .HasPrecision(5, 2);


                entity.Property(
                        x => x.MatchStatus)
                    .HasMaxLength(30);


                entity.Property(
                        x => x.SourceName)
                    .HasMaxLength(150);


                entity.Property(
                        x => x.ListVersion)
                    .HasMaxLength(50);


                entity.Property(
                        x => x.ComparedFields)
                    .HasMaxLength(500);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // RISK ASSESSMENT
        // -----------------------------------------------------

        modelBuilder.Entity<RiskAssessment>(
            entity =>
            {
                entity.ToTable(
                    "RiskAssessments",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => x.TransactionId)
                    .IsUnique();


                entity.Property(
                        x => x.RiskLevel)
                    .HasMaxLength(20);


                entity.Property(
                        x => x.ModelVersion)
                    .HasMaxLength(50);


                entity.Property(
                        x => x.Factors)
                    .HasMaxLength(2000);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // AML DECISION
        // -----------------------------------------------------

        modelBuilder.Entity<AmlDecision>(
            entity =>
            {
                entity.ToTable(
                    "AmlDecisions",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => x.TransactionId)
                    .IsUnique();


                entity.Property(
                        x => x.CorrelationId)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Decision)
                    .HasMaxLength(20);


                entity.Property(
                        x => x.Reasons)
                    .HasMaxLength(2000);


                entity.Property(
                        x => x.RuleVersions)
                    .HasMaxLength(1000);


                entity.Property(
                        x => x.ModelVersion)
                    .HasMaxLength(50);


                entity.Property(
                        x => x.ListVersions)
                    .HasMaxLength(500);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // COMPLIANCE ALERT
        // -----------------------------------------------------

        modelBuilder.Entity<ComplianceAlert>(
            entity =>
            {
                entity.ToTable(
                    "ComplianceAlerts",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.AlertReference
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_ComplianceAlerts_Partner_Reference")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                entity.Property(
                        x => x.AlertReference)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Severity)
                    .HasMaxLength(20);


                entity.Property(
                        x => x.Reason)
                    .HasMaxLength(2000);


                entity.Property(
                        x => x.Status)
                    .HasMaxLength(30);


                entity.Property(
                        x => x.Resolution)
                    .HasMaxLength(2000);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // COMPLIANCE CASE
        // -----------------------------------------------------

        modelBuilder.Entity<ComplianceCase>(
            entity =>
            {
                entity.ToTable(
                    "ComplianceCases",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.CaseReference
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_ComplianceCases_Partner_Reference")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                entity.Property(
                        x => x.CaseReference)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Status)
                    .HasMaxLength(30);


                entity.Property(
                        x => x.Disposition)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Notes)
                    .HasMaxLength(4000);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });
    }


    // =========================================================
    // RISK SCORING
    // =========================================================

    private void ConfigureRiskScoring(
        ModelBuilder modelBuilder)
    {
        // -----------------------------------------------------
        // RISK FACTOR
        // -----------------------------------------------------

        modelBuilder.Entity<RiskFactor>(
            entity =>
            {
                entity.ToTable(
                    "RiskFactors",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.FactorCode
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_RiskFactors_Partner_FactorCode")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                entity.Property(
                        x => x.FactorCode)
                    .HasMaxLength(50)
                    .IsRequired();


                entity.Property(
                        x => x.FactorName)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.ScoreCategory)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.ValueType)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.WeightPercent)
                    .HasPrecision(5, 2);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // RISK FACTOR RULE
        // -----------------------------------------------------

        modelBuilder.Entity<RiskFactorRule>(
            entity =>
            {
                entity.ToTable(
                    "RiskFactorRules",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.ValueCode)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.ValueName)
                    .HasMaxLength(200);


                entity.Property(
                        x => x.MinimumValue)
                    .HasPrecision(18, 2);


                entity.Property(
                        x => x.MaximumValue)
                    .HasPrecision(18, 2);


                entity.Property(
                        x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovedBy)
                    .HasMaxLength(100);


                entity.HasOne<RiskFactor>()
                    .WithMany()
                    .HasForeignKey(
                        x => x.RiskFactorId)
                    .OnDelete(
                        DeleteBehavior.Restrict);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.RiskFactorId,
                            x.RuleVersion
                        })

                    .HasDatabaseName(
                        "IX_RiskFactorRules_Partner_Factor_Version");


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // RISK BAND
        // -----------------------------------------------------

        modelBuilder.Entity<RiskBand>(
            entity =>
            {
                entity.ToTable(
                    "RiskBands",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.RiskLevel)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.Decision)
                    .HasMaxLength(20)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovedBy)
                    .HasMaxLength(100);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.BandVersion,
                            x.RiskLevel
                        })

                    .HasDatabaseName(
                        "IX_RiskBands_Partner_Version_Level");


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // RISK MODEL
        // -----------------------------------------------------

        modelBuilder.Entity<RiskModel>(
            entity =>
            {
                entity.ToTable(
                    "RiskModels",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.ModelCode)
                    .HasMaxLength(50)
                    .IsRequired();


                entity.Property(
                        x => x.CustomerWeightPercent)
                    .HasPrecision(5, 2);


                entity.Property(
                        x => x.TransactionWeightPercent)
                    .HasPrecision(5, 2);


                entity.Property(
                        x => x.ScreeningWeightPercent)
                    .HasPrecision(5, 2);


                entity.Property(
                        x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();


                entity.Property(
                        x => x.ApprovedBy)
                    .HasMaxLength(100);


                entity.HasIndex(
                        x => new
                        {
                            x.PartnerId,
                            x.ModelCode,
                            x.ModelVersion
                        })

                    .IsUnique()

                    .HasDatabaseName(
                        "UX_RiskModels_Partner_Code_Version")

                    .HasFilter(
                        "[DeleteFg] = 'N'");


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });


        // -----------------------------------------------------
        // RISK ASSESSMENT FACTOR
        // -----------------------------------------------------

        modelBuilder.Entity<RiskAssessmentFactor>(
            entity =>
            {
                entity.ToTable(
                    "RiskAssessmentFactors",
                    "dbo");


                entity.HasKey(
                    x => x.Id);


                entity.Property(
                        x => x.FactorCode)
                    .HasMaxLength(50)
                    .IsRequired();


                entity.Property(
                        x => x.InputValue)
                    .HasMaxLength(500)
                    .IsRequired();


                entity.Property(
                        x => x.WeightPercent)
                    .HasPrecision(5, 2);


                entity.Property(
                        x => x.WeightedScore)
                    .HasPrecision(8, 2);


                entity.Property(
                        x => x.Explanation)
                    .HasMaxLength(1000)
                    .IsRequired();


                entity.HasOne<RiskAssessment>()
                    .WithMany()
                    .HasForeignKey(
                        x => x.RiskAssessmentId)
                    .OnDelete(
                        DeleteBehavior.Restrict);


                ConfigureCommonFields(
                    entity);


                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x =>
                        x.DeleteFg == "N");


                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });
    }


    // =========================================================
    // AUDIT LOG
    // =========================================================

    private void ConfigureAuditLog(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(
            entity =>
            {
                entity.ToTable(
                    "AuditLogs",
                    "dbo");


                entity.HasKey(
                    x => x.AuditLogId);


                entity.Property(
                        x => x.Username)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.Action)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.EntityType)
                    .HasMaxLength(100)
                    .IsRequired();


                entity.Property(
                        x => x.EntityId)
                    .HasMaxLength(100);


                entity.Property(
                        x => x.Details)
                    .HasMaxLength(2000);


                /*
                 * AuditLog must now have PartnerId.
                 *
                 * Do NOT keep ClientId.
                 */

                entity.HasQueryFilter(
                    "PartnerFilter",
                    x =>
                        CurrentPartnerId > 0

                        &&

                        x.PartnerId ==
                            CurrentPartnerId);
            });
    }


    // =========================================================
    // BASE ENTITY CONFIGURATION
    // =========================================================

    private static void ConfigureCommonFields<TEntity>(
        EntityTypeBuilder<TEntity> entity)

        where TEntity : BaseEntity
    {
        entity.Property(
                x => x.DeleteFg)
            .HasMaxLength(1)
            .IsFixedLength()
            .HasDefaultValue("N");


        entity.Property(
                x => x.CreatedBy)
            .HasMaxLength(100);


        entity.Property(
                x => x.UpdatedBy)
            .HasMaxLength(100);


        entity.Property(
                x => x.DeleteBy)
            .HasMaxLength(100);


        entity.Property(
                x => x.RowVersion)
            .IsRowVersion();
    }
}