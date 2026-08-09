using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public interface IAppointmentRepository
    {
        Task<IEnumerable<Appointment>> GetAllAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize);
        Task<int> GetTotalCountAsync(string? search, string? status, string? doctorName, DateTime? date);
        Task<Appointment?> GetByIdAsync(int id);
        Task<Appointment> CreateAsync(Appointment appointment);
        Task<Appointment> UpdateAsync(Appointment appointment);
        Task DeleteAsync(Appointment appointment);
        Task<int> GetActiveBookingCountAsync(int doctorTimeSlotId, int? excludeAppointmentId = null);
        Task<IEnumerable<int>> GetBookedAppointmentNumbersAsync(int doctorTimeSlotId, int? excludeAppointmentId = null);
        Task<DoctorTimeSlot?> GetSlotByIdAsync(int id);
        Task<IEnumerable<DoctorTimeSlot>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable);
        Task<DoctorTimeSlot> CreateSlotAsync(DoctorTimeSlot slot);
        Task<DoctorTimeSlot> UpdateSlotAsync(DoctorTimeSlot slot);
        Task<bool> SlotOverlapsAsync(string doctorName, DateTime startAt, DateTime endAt, int? excludeSlotId = null);
    }
}
