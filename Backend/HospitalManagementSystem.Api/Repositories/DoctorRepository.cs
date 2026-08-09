using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly ApplicationDbContext _context;

        public DoctorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Doctor>> GetAllAsync(string? status = null, string? search = null)
        {
            var query = _context.Doctors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(d =>
                    d.FirstName.ToLower().Contains(s) ||
                    d.LastName.ToLower().Contains(s) ||
                    d.Email.ToLower().Contains(s) ||
                    d.Specialization.ToLower().Contains(s) ||
                    d.SLMCLicenseNumber.ToLower().Contains(s) ||
                    d.NIC.ToLower().Contains(s));
            }

            return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        }

        public async Task<Doctor?> GetByIdAsync(int id)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.DoctorId == id);
        }

        public async Task<Doctor?> GetByUserIdAsync(int userId)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
        }

        public async Task<Doctor?> GetByEmailAsync(string email)
        {
            return await _context.Doctors.FirstOrDefaultAsync(d => d.Email.ToLower() == email.ToLower());
        }

        public async Task<Doctor> CreateAsync(Doctor doctor)
        {
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();
            return doctor;
        }

        public async Task<Doctor> UpdateAsync(Doctor doctor)
        {
            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync();
            return doctor;
        }

        public async Task<bool> ExistsByEmailAsync(string email, int? excludeDoctorId = null)
        {
            return await _context.Doctors.AnyAsync(d => d.Email.ToLower() == email.ToLower() && (!excludeDoctorId.HasValue || d.DoctorId != excludeDoctorId.Value));
        }

        public async Task<bool> ExistsByNICAsync(string nic, int? excludeDoctorId = null)
        {
            return await _context.Doctors.AnyAsync(d => d.NIC.ToLower() == nic.ToLower() && (!excludeDoctorId.HasValue || d.DoctorId != excludeDoctorId.Value));
        }

        public async Task<bool> ExistsBySLMCAsync(string slmc, int? excludeDoctorId = null)
        {
            return await _context.Doctors.AnyAsync(d => d.SLMCLicenseNumber.ToLower() == slmc.ToLower() && (!excludeDoctorId.HasValue || d.DoctorId != excludeDoctorId.Value));
        }
    }
}
