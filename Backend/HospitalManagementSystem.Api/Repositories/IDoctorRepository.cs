using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public interface IDoctorRepository
    {
        Task<IEnumerable<Doctor>> GetAllAsync(string? status = null, string? search = null);
        Task<Doctor?> GetByIdAsync(int id);
        Task<Doctor?> GetByUserIdAsync(int userId);
        Task<Doctor?> GetByEmailAsync(string email);
        Task<Doctor> CreateAsync(Doctor doctor);
        Task<Doctor> UpdateAsync(Doctor doctor);
        Task<bool> ExistsByEmailAsync(string email, int? excludeDoctorId = null);
        Task<bool> ExistsByNICAsync(string nic, int? excludeDoctorId = null);
        Task<bool> ExistsBySLMCAsync(string slmc, int? excludeDoctorId = null);
    }
}
