using AmlScreening.Domain.Entities;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Infrastructure.Data
{
    public sealed class AmlScreeningDbContext : DbContext
    {
        private readonly ICurrentUserContext _currentUser;

        public AmlScreeningDbContext(
            DbContextOptions<AmlScreeningDbContext> options,
            ICurrentUserContext currentUser) : base(options)
        {
            _currentUser = currentUser;
        }
        public int CurrentClientId => _currentUser.ClientId;
        public bool CanAccessAllClients =>
            _currentUser.HasPermission(AppPermissions.PlatformCrossClientAccess);
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<AmlRecord> AmlRecords { get; set; }
        public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<AmlTransaction> Transactions => Set<AmlTransaction>();
        public DbSet<ScreeningMatch> ScreeningMatches => Set<ScreeningMatch>();
        public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
        public DbSet<AmlDecision> AmlDecisions => Set<AmlDecision>();
        public DbSet<ComplianceAlert> ComplianceAlerts => Set<ComplianceAlert>();
        public DbSet<ComplianceCase> ComplianceCases => Set<ComplianceCase>();

        public DbSet<RiskFactor> RiskFactors => Set<RiskFactor>();
        public DbSet<RiskFactorRule> RiskFactorRules => Set<RiskFactorRule>();
        public DbSet<RiskBand> RiskBands => Set<RiskBand>();
        public DbSet<RiskModel> RiskModels => Set<RiskModel>();
        public DbSet<RiskAssessmentFactor> RiskAssessmentFactors => Set<RiskAssessmentFactor>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AmlRecord>(entity =>
            {
                entity.ToTable("AmlRecords", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.SourceName, x.ReferenceNo }).IsUnique();
                entity.Property(x => x.IsShared).HasDefaultValue(false);
                entity.Property(x => x.Country)
                    .HasMaxLength(100)
                    .IsRequired(false);
                entity.Property(x => x.Nationality)
                    .HasMaxLength(100)
                    .IsRequired(false);
                entity.Property(x => x.Remarks)
                    .IsRequired(false);
                entity.Property(x => x.DeleteFg)
                    .HasMaxLength(1)
                    .IsFixedLength()
                    .HasDefaultValue("N");

                entity.Property(x => x.DeleteBy)
                    .HasMaxLength(50);

                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });
            modelBuilder.Entity<ComplianceRule>(entity =>
            {
                entity.ToTable("ComplianceRules", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.RuleCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.RuleName)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.RuleType)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.Currency)
                    .HasMaxLength(3);

                entity.Property(x => x.ThresholdAmount)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Action)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.ApprovalStatus)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.ApprovedBy).HasMaxLength(100);

                entity.Property(x => x.DeleteFg)
                    .HasMaxLength(1)
                    .IsFixedLength()
                    .HasDefaultValue("N");

                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.CustomerReference }).IsUnique();
                entity.Property(x => x.CustomerReference).HasMaxLength(100).IsRequired();
                entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Country).HasMaxLength(100);
                entity.Property(x => x.Nationality).HasMaxLength(100);
                entity.Property(x => x.Occupation).HasMaxLength(150);
                entity.Property(x => x.SourceOfFunds).HasMaxLength(150);
                entity.Property(x => x.RiskLevel).HasMaxLength(20);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<AmlTransaction>(entity =>
            {
                entity.ToTable("AmlTransactions", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.TransactionReference }).IsUnique();
                entity.Property(x => x.TransactionReference).HasMaxLength(100).IsRequired();
                entity.Property(x => x.SenderName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.ReceiverName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Amount).HasPrecision(18, 2);
                entity.Property(x => x.Currency).HasMaxLength(3);
                entity.Property(x => x.OriginCountry).HasMaxLength(100);
                entity.Property(x => x.DestinationCountry).HasMaxLength(100);
                entity.Property(x => x.Channel).HasMaxLength(50);
                entity.Property(x => x.Status).HasMaxLength(30);
                entity.Property(x => x.CorrelationId).HasMaxLength(100);
                entity.Property(x => x.Decision).HasMaxLength(20);
                entity.Property(x => x.RiskLevel).HasMaxLength(20);
                entity.Property(x => x.ScreeningStatus).HasMaxLength(30);
                entity.Property(x => x.DecisionReasons).HasMaxLength(2000);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
                entity.Property(x => x.PurposeCode)
    .HasMaxLength(50);

                entity.Property(x => x.RegisteredReceiverCount);

                entity.Property(x => x.RelationshipCode)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<ScreeningMatch>(entity =>
            {
                entity.ToTable("ScreeningMatches", "dbo");
                entity.Property(x => x.SubjectName).HasMaxLength(200);
                entity.Property(x => x.MatchedName).HasMaxLength(200);
                entity.Property(x => x.MatchScore).HasPrecision(5, 2);
                entity.Property(x => x.MatchStatus).HasMaxLength(30);
                entity.Property(x => x.SourceName).HasMaxLength(150);
                entity.Property(x => x.ListVersion).HasMaxLength(50);
                entity.Property(x => x.ComparedFields).HasMaxLength(500);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<RiskAssessment>(entity =>
            {
                entity.ToTable("RiskAssessments", "dbo");
                entity.HasIndex(x => x.TransactionId).IsUnique();
                entity.Property(x => x.RiskLevel).HasMaxLength(20);
                entity.Property(x => x.ModelVersion).HasMaxLength(50);
                entity.Property(x => x.Factors).HasMaxLength(2000);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<AmlDecision>(entity =>
            {
                entity.ToTable("AmlDecisions", "dbo");
                entity.HasIndex(x => x.TransactionId).IsUnique();
                entity.Property(x => x.CorrelationId).HasMaxLength(100);
                entity.Property(x => x.Decision).HasMaxLength(20);
                entity.Property(x => x.Reasons).HasMaxLength(2000);
                entity.Property(x => x.RuleVersions).HasMaxLength(1000);
                entity.Property(x => x.ModelVersion).HasMaxLength(50);
                entity.Property(x => x.ListVersions).HasMaxLength(500);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<ComplianceAlert>(entity =>
            {
                entity.ToTable("ComplianceAlerts", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.AlertReference }).IsUnique();
                entity.Property(x => x.AlertReference).HasMaxLength(100);
                entity.Property(x => x.Severity).HasMaxLength(20);
                entity.Property(x => x.Reason).HasMaxLength(2000);
                entity.Property(x => x.Status).HasMaxLength(30);
                entity.Property(x => x.Resolution).HasMaxLength(2000);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<ComplianceCase>(entity =>
            {
                entity.ToTable("ComplianceCases", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.CaseReference }).IsUnique();
                entity.Property(x => x.CaseReference).HasMaxLength(100);
                entity.Property(x => x.Status).HasMaxLength(30);
                entity.Property(x => x.Disposition).HasMaxLength(100);
                entity.Property(x => x.Notes).HasMaxLength(4000);
                entity.Property(x => x.DeleteFg).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
                entity.Property(x => x.RowVersion).IsRowVersion();
                entity.HasQueryFilter("SoftDeleteFilter", x => x.DeleteFg == "N");
                entity.HasQueryFilter("ClientFilter", x => CanAccessAllClients || x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("Clients", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ClientCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
                entity.HasIndex(x => x.ClientCode).IsUnique();
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs", "dbo");
                entity.HasKey(x => x.AuditLogId);
                entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
                entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
                entity.Property(x => x.EntityId).HasMaxLength(100);
                entity.Property(x => x.Details).HasMaxLength(2000);
            });
            modelBuilder.Entity<RiskFactor>(entity =>
            {
                entity.ToTable("RiskFactors", "dbo");

                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.ClientId,
                    x.FactorCode
                }).IsUnique().HasFilter("[DeleteFg] = 'N'");

                entity.Property(x => x.FactorCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.FactorName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.ScoreCategory)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.ValueType)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.WeightPercent)
                    .HasPrecision(5, 2);

                ConfigureCommonFields(entity);

                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x => x.DeleteFg == "N");

                entity.HasQueryFilter(
                    "ClientFilter",
                    x => CanAccessAllClients ||
                         x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<RiskFactorRule>(entity =>
            {
                entity.ToTable("RiskFactorRules", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ValueCode)
                    .HasMaxLength(100);

                entity.Property(x => x.ValueName)
                    .HasMaxLength(200);

                entity.Property(x => x.MinimumValue)
                    .HasPrecision(18, 2);

                entity.Property(x => x.MaximumValue)
                    .HasPrecision(18, 2);

                entity.Property(x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(x => x.ApprovedBy)
                    .HasMaxLength(100);

                entity.HasOne<RiskFactor>()
                    .WithMany()
                    .HasForeignKey(x => x.RiskFactorId)
                    .OnDelete(DeleteBehavior.Restrict);

                ConfigureCommonFields(entity);

                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x => x.DeleteFg == "N");

                entity.HasQueryFilter(
                    "ClientFilter",
                    x => CanAccessAllClients ||
                         x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<RiskBand>(entity =>
            {
                entity.ToTable("RiskBands", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.RiskLevel)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.Decision)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                ConfigureCommonFields(entity);

                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x => x.DeleteFg == "N");

                entity.HasQueryFilter(
                    "ClientFilter",
                    x => CanAccessAllClients ||
                         x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<RiskModel>(entity =>
            {
                entity.ToTable("RiskModels", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.ModelCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.CustomerWeightPercent)
                    .HasPrecision(5, 2);

                entity.Property(x => x.TransactionWeightPercent)
                    .HasPrecision(5, 2);

                entity.Property(x => x.ScreeningWeightPercent)
                    .HasPrecision(5, 2);

                entity.Property(x => x.ApprovalStatus)
                    .HasMaxLength(30)
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.ClientId,
                    x.ModelCode,
                    x.ModelVersion
                }).IsUnique().HasFilter("[DeleteFg] = 'N'");

                ConfigureCommonFields(entity);

                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x => x.DeleteFg == "N");

                entity.HasQueryFilter(
                    "ClientFilter",
                    x => CanAccessAllClients ||
                         x.ClientId == CurrentClientId);
            });

            modelBuilder.Entity<RiskAssessmentFactor>(entity =>
            {
                entity.ToTable("RiskAssessmentFactors", "dbo");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.FactorCode)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.InputValue)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.WeightPercent)
                    .HasPrecision(5, 2);

                entity.Property(x => x.WeightedScore)
                    .HasPrecision(8, 2);

                entity.Property(x => x.Explanation)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.HasOne<RiskAssessment>()
                    .WithMany()
                    .HasForeignKey(x => x.RiskAssessmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                ConfigureCommonFields(entity);

                entity.HasQueryFilter(
                    "SoftDeleteFilter",
                    x => x.DeleteFg == "N");

                entity.HasQueryFilter(
                    "ClientFilter",
                    x => CanAccessAllClients ||
                         x.ClientId == CurrentClientId);
            });
        }
        private static void ConfigureCommonFields<TEntity>(
    Microsoft.EntityFrameworkCore.Metadata.Builders
        .EntityTypeBuilder<TEntity> entity)
    where TEntity : AmlScreening.Domain.Common.BaseEntity
        {
            entity.Property(x => x.DeleteFg)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasDefaultValue("N");

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100);

            entity.Property(x => x.UpdatedBy)
                .HasMaxLength(100);

            entity.Property(x => x.DeleteBy)
                .HasMaxLength(100);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();
        }
    }
}
