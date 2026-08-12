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
        public DbSet<DoctorTimeSlot> DoctorTimeSlots => Set<DoctorTimeSlot>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
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

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Doctors_RegistrationStatus",
                    "\"RegistrationStatus\" IN ('Pending', 'Approved', 'Declined')"));
                entity.HasKey(d => d.DoctorId);
                entity.Property(d => d.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.LastName).IsRequired().HasMaxLength(100);
                entity.Property(d => d.NIC).IsRequired().HasMaxLength(12);
                entity.Property(d => d.Specialization).IsRequired().HasMaxLength(100);
                entity.Property(d => d.SlmcLicenseNumber).IsRequired().HasMaxLength(50);
                entity.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(d => d.RegistrationStatus).IsRequired().HasMaxLength(20);
                entity.Property(d => d.DeclineReason).HasMaxLength(500);
                entity.HasIndex(d => d.UserId).IsUnique();
                entity.HasIndex(d => d.NIC).IsUnique();
                entity.HasIndex(d => d.SlmcLicenseNumber).IsUnique();
                entity.HasIndex(d => d.RegistrationStatus);
                entity.HasOne(d => d.User)
                    .WithOne(u => u.DoctorProfile)
                    .HasForeignKey<Doctor>(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany()
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
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
                entity.Property(p => p.EmergencyContactName).IsRequired(false).HasMaxLength(100);
                entity.Property(p => p.EmergencyContactPhone).IsRequired(false).HasMaxLength(20);
                entity.Property(p => p.ProfileImageUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<DoctorTimeSlot>(entity =>
            {
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_DoctorTimeSlots_Capacity_Positive", "\"Capacity\" > 0");
                    t.HasCheckConstraint("CK_DoctorTimeSlots_TimeRange", "\"EndAt\" > \"StartAt\"");
                });
                entity.HasKey(s => s.DoctorTimeSlotId);
                entity.Property(s => s.DoctorName).IsRequired().HasMaxLength(150);
                entity.Property(s => s.Specialty).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Capacity).IsRequired();
                entity.Property(s => s.IsActive).IsRequired();
                entity.HasIndex(s => new { s.DoctorName, s.StartAt, s.EndAt });
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.ToTable(t =>
                    t.HasCheckConstraint(
                        "CK_Appointments_Status",
                        "\"Status\" IN ('Requested', 'Confirmed', 'Completed', 'Cancelled', 'No-show')"));
                entity.HasKey(a => a.AppointmentId);
                entity.Property(a => a.AppointmentNumber).IsRequired();
                entity.Property(a => a.EstimatedStartAt).IsRequired();
                entity.Property(a => a.PatientName).IsRequired().HasMaxLength(150);
                entity.Property(a => a.PatientPhone).IsRequired().HasMaxLength(30);
                entity.Property(a => a.PatientEmail).HasMaxLength(200);
                entity.Property(a => a.AppointmentType).IsRequired().HasMaxLength(80);
                entity.Property(a => a.Reason).IsRequired().HasMaxLength(500);
                entity.Property(a => a.Status).IsRequired().HasMaxLength(30);
                entity.Property(a => a.CancellationReason).HasMaxLength(500);
                entity.Property(a => a.Notes).HasMaxLength(500);
                entity.HasIndex(a => a.Status);
                entity.HasIndex(a => a.CreatedAt);
                entity.HasIndex(a => new { a.DoctorTimeSlotId, a.AppointmentNumber })
                    .IsUnique()
                    .HasFilter("\"Status\" <> 'Cancelled'");
                entity.HasIndex(a => a.EstimatedStartAt);
                entity.HasOne(a => a.DoctorTimeSlot)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(a => a.DoctorTimeSlotId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Patient)
                    .WithMany()
                    .HasForeignKey(a => a.PatientId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
