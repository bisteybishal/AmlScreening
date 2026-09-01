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
            _currentUser.HasPermission(AppPermissions.AmlRecordsViewAll);
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AmlRecord>(entity =>
            {
                entity.ToTable("AmlRecords", "dbo");
                entity.HasIndex(x => new { x.ClientId, x.ReferenceNo }).IsUnique();
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
        }
    }
}
