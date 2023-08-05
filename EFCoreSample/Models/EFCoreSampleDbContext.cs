using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EFCoreSample.Models
{
    public partial class EFCoreSampleDbContext : DbContext
    {
        public EFCoreSampleDbContext()
        {
        }

        public EFCoreSampleDbContext(DbContextOptions<EFCoreSampleDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<TestModel> TestModels { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer("Server=(LocalDb)\\MSSQLLocalDB;Database=EFCoreSample;Trusted_Connection=True;TrustServerCertificate=True");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestModel>(entity =>
            {
                entity.ToTable("TestModel");

                entity.Property(e => e.CreateDate).HasColumnType("datetime");

                entity.Property(e => e.D).HasColumnType("decimal(18, 8)");

                entity.Property(e => e.S)
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
