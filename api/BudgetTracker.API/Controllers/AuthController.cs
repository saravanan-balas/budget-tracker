using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Data;
using BudgetTracker.API.Services;

namespace BudgetTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto createUserDto)
    {
        try
        {
            var result = await _authService.RegisterAsync(createUserDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            var result = await _authService.LoginAsync(loginDto);
            if (result == null)
            {
                return Unauthorized(new { error = "Invalid email or password" });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "An error occurred during login" });
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);
            if (result == null)
            {
                return Unauthorized(new { error = "Invalid refresh token" });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { error = "An error occurred while refreshing token" });
        }
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto googleAuthDto)
    {
        try
        {
            var result = await _authService.AuthenticateWithGoogleAsync(googleAuthDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google authentication");
            return StatusCode(500, new { error = "An error occurred during Google authentication" });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { error = "Invalid user" });
        }

        await _authService.LogoutAsync(Guid.Parse(userId));
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("make-admin")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> MakeAdmin([FromBody] MakeAdminDto makeAdminDto)
    {
        try
        {
            // SECURITY: This endpoint is only available in Development environment
            // In Production, this should be removed or secured with proper authentication
            var environment = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment())
            {
                _logger.LogWarning("MakeAdmin endpoint called in non-development environment");
                return StatusCode(403, new { error = "This endpoint is only available in development environment" });
            }
            
            var context = HttpContext.RequestServices.GetRequiredService<BudgetTracker.Common.Data.BudgetTrackerDbContext>();
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Email == makeAdminDto.Email);
            
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }
            
            user.IsAdmin = true;
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            
            _logger.LogWarning("User {Email} (ID: {UserId}) was granted admin access via MakeAdmin endpoint", user.Email, user.Id);
            
            return Ok(new { message = $"User {user.Email} is now an admin. Please log out and log back in for the change to take effect." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making user admin");
            return StatusCode(500, new { error = "An error occurred while granting admin access" });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);
            if (result)
            {
                return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
            }
            return BadRequest(new { error = "Failed to process forgot password request" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request");
            return StatusCode(500, new { error = "An error occurred while processing your request" });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(resetPasswordDto);
            if (result)
            {
                return Ok(new { message = "Password has been reset successfully" });
            }
            return BadRequest(new { error = "Invalid or expired reset token" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(500, new { error = "An error occurred while resetting your password" });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        try
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { error = "Invalid user" });
            }

            var result = await _authService.ChangePasswordAsync(Guid.Parse(userId), changePasswordDto);
            if (result)
            {
                return Ok(new { message = "Password has been changed successfully" });
            }
            return BadRequest(new { error = "Current password is incorrect" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { error = "An error occurred while changing your password" });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "Invalid user" });
            }

            var user = await _authService.GetCurrentUserAsync(Guid.Parse(userId));
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { error = "An error occurred while getting user information" });
        }
    }
}

public class RefreshTokenDto
{
    public string RefreshToken { get; set; } = string.Empty;
}