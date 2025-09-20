using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(CreateUserDto createUserDto);
    Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(Guid userId);
    Task<AuthResponseDto> AuthenticateWithGoogleAsync(GoogleAuthDto googleAuthDto);
    Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
    Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);
}