using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Doctor> Doctors => Set<Doctor>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired().HasMaxLength(50);
            });

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

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(d => d.DoctorId);
                entity.Property(d => d.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.LastName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Email).IsRequired().HasMaxLength(200);
                entity.HasIndex(d => d.Email).IsUnique();
                entity.Property(d => d.NIC).IsRequired().HasMaxLength(20);
                entity.HasIndex(d => d.NIC).IsUnique();
                entity.Property(d => d.SLMCLicenseNumber).IsRequired().HasMaxLength(50);
                entity.HasIndex(d => d.SLMCLicenseNumber).IsUnique();
                entity.Property(d => d.Specialization).IsRequired().HasMaxLength(150);
                entity.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(d => d.Status).IsRequired().HasMaxLength(20);

                entity.HasOne(d => d.User)
                      .WithOne(u => u.DoctorProfile)
                      .HasForeignKey<Doctor>(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
