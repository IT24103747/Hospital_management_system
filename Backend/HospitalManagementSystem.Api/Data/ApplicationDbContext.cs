using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Patient> Patients => Set<Patient>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.PatientId);
                entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(p => p.Email).IsUnique();
                entity.Property(p => p.NIC).IsRequired().HasMaxLength(20);
                entity.HasIndex(p => p.NIC).IsUnique();
                entity.Property(p => p.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(p => p.Gender).IsRequired().HasMaxLength(20);
                entity.Property(p => p.BloodGroup).HasMaxLength(10);
                entity.Property(p => p.Address).HasMaxLength(500);
                entity.Property(p => p.EmergencyContactName).HasMaxLength(100);
                entity.Property(p => p.EmergencyContactPhone).HasMaxLength(20);
                entity.Property(p => p.ProfileImageUrl).HasMaxLength(500);
            });
        }
    }
}
