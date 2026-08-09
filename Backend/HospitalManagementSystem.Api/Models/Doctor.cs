using System;
using System.Text.Json.Serialization;

namespace HospitalManagementSystem.Api.Models
{
    public class Doctor
    {
        public int DoctorId { get; set; }
        
        public int UserId { get; set; }
        
        [JsonIgnore]
        public User? User { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string SLMCLicenseNumber { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        
        // Status options: "Pending", "Approved", "Declined"
        public string Status { get; set; } = "Pending";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ActionedAt { get; set; }
    }
}
