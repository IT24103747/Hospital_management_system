using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IAppointmentService
    {
        Task<PagedResult<AppointmentDto>> GetAllAppointmentsAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize);
        Task<AppointmentDto?> GetAppointmentByIdAsync(int id);
        Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto);
        Task<AppointmentDto?> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto);
        Task<bool> DeleteAppointmentAsync(int id);
        Task<AppointmentDto?> UpdateStatusAsync(int id, string status);
        Task<AppointmentDto?> CancelAppointmentAsync(int id, string reason);
        Task<AppointmentDto?> RescheduleAppointmentAsync(int id, int doctorTimeSlotId);
        Task<IEnumerable<DoctorLookupDto>> GetDoctorsAsync();
        Task<IEnumerable<DoctorTimeSlotDto>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable);
        Task<DoctorTimeSlotDto> CreateSlotAsync(CreateDoctorTimeSlotDto dto);
        Task<DoctorTimeSlotDto?> UpdateSlotAsync(int id, UpdateDoctorTimeSlotDto dto);
        Task<DoctorTimeSlotDto?> CancelSlotAsync(int id, string? reason);
    }
}
