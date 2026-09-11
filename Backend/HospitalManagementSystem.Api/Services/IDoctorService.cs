using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface IDoctorService
{
    Task RegisterAsync(RegisterDoctorDto dto);
    Task<IEnumerable<DoctorDto>> GetRegistrationsAsync(string? status);
    Task<DoctorDto?> GetByIdAsync(int doctorId);
    Task<DoctorDto?> ReviewAsync(int doctorId, int adminUserId, bool approve, string? declineReason);
    Task<DoctorDto?> GetByUserIdAsync(int userId);
    Task<DoctorDto?> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto);
    Task<IReadOnlyList<DoctorSearchResultDto>> SearchApprovedDoctorsAsync(string? query, int limit = 20);
    Task<DoctorPublicProfileDto?> GetApprovedDoctorProfileAsync(int doctorId);
}
