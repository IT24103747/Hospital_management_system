using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // Seed Admin if not present
            if (!context.Users.Any(u => u.Email == "admin@medicore.lk"))
            {
                var adminUser = new User
                {
                    FullName = "System Admin",
                    Email = "admin@medicore.lk",
                    PasswordHash = HashPassword("Admin1234"),
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(adminUser);
                context.SaveChanges();
            }
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + "_MediCoreSalt_2026");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            return HashPassword(password) == storedHash;
        }
    }
}
