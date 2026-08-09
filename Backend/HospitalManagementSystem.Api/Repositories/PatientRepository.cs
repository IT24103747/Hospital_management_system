using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly ApplicationDbContext _context;

        public PatientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Patient>> GetAllAsync(string? search, int page, int pageSize)
        {
            var query = _context.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLowerInvariant();
                query = query.Where(p => MatchesSearch(p, normalizedSearch));
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? search)
        {
            var query = _context.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLowerInvariant();
                query = query.Where(p => MatchesSearch(p, normalizedSearch));
            }

            return await query.CountAsync();
        }

        public async Task<Patient?> GetByIdAsync(int id) =>
            await _context.Patients.FindAsync(id);

        public async Task<Patient?> GetByEmailAsync(string email) =>
            await _context.Patients.FirstOrDefaultAsync(p => p.Email == email);

        public async Task<Patient> CreateAsync(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task<Patient> UpdateAsync(Patient patient)
        {
            patient.UpdatedAt = DateTime.UtcNow;
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task DeleteAsync(Patient patient)
        {
            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByEmailAsync(string email, int? excludeId = null) =>
            await _context.Patients.AnyAsync(p => p.Email == email && p.PatientId != excludeId);

        public async Task<bool> ExistsByNICAsync(string nic, int? excludeId = null) =>
            await _context.Patients.AnyAsync(p => p.NIC == nic && p.PatientId != excludeId);

        private static bool MatchesSearch(Patient patient, string search)
        {
            return
                (patient.FirstName != null && patient.FirstName.ToLower().Contains(search)) ||
                (patient.LastName != null && patient.LastName.ToLower().Contains(search)) ||
                (patient.Email != null && patient.Email.ToLower().Contains(search)) ||
                (patient.NIC != null && patient.NIC.ToLower().Contains(search)) ||
                (patient.PhoneNumber != null && patient.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
    }
}
