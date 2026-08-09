using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
    }
}
