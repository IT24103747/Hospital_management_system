using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public interface IPatientRepository
    {
        Task<IEnumerable<Patient>> GetAllAsync(string? search, int page, int pageSize);
        Task<int> GetTotalCountAsync(string? search);
        Task<Patient?> GetByIdAsync(int id);
        Task<Patient?> GetByEmailAsync(string email);
        Task<Patient> CreateAsync(Patient patient);
        Task<Patient> UpdateAsync(Patient patient);
        Task DeleteAsync(Patient patient);
        Task<bool> ExistsByEmailAsync(string email, int? excludeId = null);
        Task<bool> ExistsByNICAsync(string nic, int? excludeId = null);
    }
}
