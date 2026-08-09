using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IDoctorService
    {
        Task<DoctorDto> RegisterDoctorAsync(DoctorRegisterDto dto);
        Task<IEnumerable<DoctorDto>> GetAllDoctorsAsync(string? status = null, string? search = null);
        Task<DoctorDto?> GetDoctorByIdAsync(int doctorId);
        Task<DoctorDto?> GetDoctorByUserIdAsync(int userId);
        Task<DoctorDto> ActionDoctorRequestAsync(int doctorId, string status);
        Task<DoctorDto> UpdateDoctorProfileAsync(int doctorId, UpdateDoctorProfileDto dto);
    }
}
