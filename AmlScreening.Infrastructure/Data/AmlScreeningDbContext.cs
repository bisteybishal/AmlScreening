using AmlScreening.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Infrastructure.Data
{
    public sealed class AmlScreeningDbContext : DbContext
    {
        public AmlScreeningDbContext(DbContextOptions<AmlScreeningDbContext> options) : base(options)
        {
        }
        public DbSet<AmlRecord> AmlRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AmlRecord>(entity =>
            {
                entity.Property(x => x.DeleteFg)
                    .HasMaxLength(1)
                    .IsFixedLength()
                    .HasDefaultValue("N");

                entity.Property(x => x.DeleteBy)
                    .HasMaxLength(50);

                entity.HasQueryFilter(x => x.DeleteFg == "N");
            });
        }
    }
}
