using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] OccupyingStatuses = ["Requested", "Confirmed", "Completed", "No-show"];

        public AppointmentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Appointment>> GetAllAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize)
        {
            var query = BuildAppointmentQuery(search, status, doctorName, date);
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLower() switch
            {
                "patient" => descending ? query.OrderByDescending(a => a.PatientName) : query.OrderBy(a => a.PatientName),
                "doctor" => descending ? query.OrderByDescending(a => a.DoctorTimeSlot!.DoctorName) : query.OrderBy(a => a.DoctorTimeSlot!.DoctorName),
                "status" => descending ? query.OrderByDescending(a => a.Status) : query.OrderBy(a => a.Status),
                "created" => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
                _ => descending
                    ? query.OrderByDescending(a => a.EstimatedStartAt).ThenByDescending(a => a.AppointmentNumber)
                    : query.OrderBy(a => a.EstimatedStartAt).ThenBy(a => a.AppointmentNumber)
            };

            return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? search, string? status, string? doctorName, DateTime? date) =>
            await BuildAppointmentQuery(search, status, doctorName, date).CountAsync();

        public async Task<Appointment?> GetByIdAsync(int id) =>
            await _context.Appointments
                .Include(a => a.DoctorTimeSlot)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

        public async Task<Appointment> CreateAsync(Appointment appointment)
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return (await GetByIdAsync(appointment.AppointmentId))!;
        }

        public async Task<Appointment> UpdateAsync(Appointment appointment)
        {
            appointment.UpdatedAt = DateTime.UtcNow;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            return (await GetByIdAsync(appointment.AppointmentId))!;
        }

        public async Task DeleteAsync(Appointment appointment)
        {
            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetActiveBookingCountAsync(int doctorTimeSlotId, int? excludeAppointmentId = null) =>
            await _context.Appointments.CountAsync(a =>
                a.DoctorTimeSlotId == doctorTimeSlotId &&
                OccupyingStatuses.Contains(a.Status) &&
                (!excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value));

        public async Task<IEnumerable<int>> GetBookedAppointmentNumbersAsync(int doctorTimeSlotId, int? excludeAppointmentId = null) =>
            await _context.Appointments
                .Where(a =>
                    a.DoctorTimeSlotId == doctorTimeSlotId &&
                    OccupyingStatuses.Contains(a.Status) &&
                    (!excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value))
                .Select(a => a.AppointmentNumber)
                .ToListAsync();

        public async Task<DoctorTimeSlot?> GetSlotByIdAsync(int id) =>
            await _context.DoctorTimeSlots
                .Include(s => s.Appointments)
                .FirstOrDefaultAsync(s => s.DoctorTimeSlotId == id);

        public async Task<IEnumerable<DoctorTimeSlot>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable)
        {
            var query = _context.DoctorTimeSlots.Include(s => s.Appointments).AsQueryable();

            if (!string.IsNullOrWhiteSpace(doctorName))
            {
                var term = doctorName.ToLower();
                query = query.Where(s => s.DoctorName.ToLower().Contains(term));
            }

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(s => s.StartAt >= start && s.StartAt < end);
            }

            if (onlyAvailable)
            {
                query = query.Where(s => s.IsActive && s.Appointments.Count(a => OccupyingStatuses.Contains(a.Status)) < s.Capacity);
            }

            return await query.OrderBy(s => s.StartAt).ToListAsync();
        }

        public async Task<DoctorTimeSlot> CreateSlotAsync(DoctorTimeSlot slot)
        {
            _context.DoctorTimeSlots.Add(slot);
            await _context.SaveChangesAsync();
            return slot;
        }

        public async Task<bool> SlotOverlapsAsync(string doctorName, DateTime startAt, DateTime endAt, int? excludeSlotId = null) =>
            await _context.DoctorTimeSlots.AnyAsync(s =>
                s.DoctorName.ToLower() == doctorName.ToLower() &&
                s.StartAt < endAt &&
                startAt < s.EndAt &&
                (!excludeSlotId.HasValue || s.DoctorTimeSlotId != excludeSlotId.Value));

        private IQueryable<Appointment> BuildAppointmentQuery(string? search, string? status, string? doctorName, DateTime? date)
        {
            var query = _context.Appointments
                .Include(a => a.DoctorTimeSlot)
                .Include(a => a.Patient)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(a =>
                    a.PatientName.ToLower().Contains(term) ||
                    a.PatientPhone.Contains(term) ||
                    (a.PatientEmail != null && a.PatientEmail.ToLower().Contains(term)) ||
                    a.DoctorTimeSlot!.DoctorName.ToLower().Contains(term) ||
                    a.Reason.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(doctorName))
            {
                var term = doctorName.ToLower();
                query = query.Where(a => a.DoctorTimeSlot!.DoctorName.ToLower().Contains(term));
            }

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(a => a.DoctorTimeSlot!.StartAt >= start && a.DoctorTimeSlot.StartAt < end);
            }

            return query;
        }
    }
}
