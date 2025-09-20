using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using AutoMapper;
using Google.Apis.Auth;

namespace BudgetTracker.API.Services;

public class AuthService : IAuthService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    public AuthService(
        BudgetTrackerDbContext context, 
        IConfiguration configuration,
        IMapper mapper,
        ILogger<AuthService> logger,
        IEmailService emailService)
    {
        _context = context;
        _configuration = configuration;
        _mapper = mapper;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterAsync(CreateUserDto createUserDto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == createUserDto.Email);

        if (existingUser != null)
        {
            throw new ArgumentException("User with this email already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = createUserDto.Email,
            FirstName = createUserDto.FirstName,
            LastName = createUserDto.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(createUserDto.Password),
            Currency = createUserDto.Currency,
            Country = createUserDto.Country,
            TimeZone = createUserDto.TimeZone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return null;
        }

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("Refresh token logic to be implemented");
    }

    public async Task LogoutAsync(Guid userId)
    {
        await Task.CompletedTask;
        _logger.LogInformation("User {UserId} logged out", userId);
    }

    public async Task<AuthResponseDto> AuthenticateWithGoogleAsync(GoogleAuthDto googleAuthDto)
    {
        try
        {
            // Verify the Google ID token
            var payload = await GoogleJsonWebSignature.ValidateAsync(googleAuthDto.IdToken);
            
            if (payload == null)
            {
                throw new ArgumentException("Invalid Google ID token");
            }

            // Check if user already exists by Google ID or email
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.GoogleId == payload.Subject || u.Email == payload.Email);

            User user;

            if (existingUser != null)
            {
                // Update existing user with Google ID if not already set
                if (string.IsNullOrEmpty(existingUser.GoogleId))
                {
                    existingUser.GoogleId = payload.Subject;
                    existingUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                user = existingUser;
            }
            else
            {
                // Create new user from Google profile
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = payload.Email,
                    FirstName = payload.GivenName ?? "",
                    LastName = payload.FamilyName ?? "",
                    GoogleId = payload.Subject,
                    Currency = "USD",
                    Country = "US",
                    TimeZone = "UTC",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                User = _mapper.Map<UserDto>(user)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Google");
            throw new ArgumentException("Failed to authenticate with Google");
        }
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("UserId", user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim("SubscriptionTier", user.SubscriptionTier.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == forgotPasswordDto.Email);

            if (user == null)
            {
                // Don't reveal if user exists or not for security
                return true;
            }

            // Generate a secure token
            var token = GenerateSecureToken();
            var expiresAt = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

            // Invalidate any existing tokens for this user
            var existingTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed)
                .ToListAsync();

            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = DateTime.UtcNow;
            }

            // Create new reset token
            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = token,
                Email = user.Email,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // Send email with reset link
            var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, token, user.FirstName);
            
            if (emailSent)
            {
                _logger.LogInformation("Password reset email sent successfully to {Email}", user.Email);
            }
            else
            {
                _logger.LogWarning("Failed to send password reset email to {Email}, but token was generated: {Token}", user.Email, token);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", forgotPasswordDto.Email);
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        try
        {
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == resetPasswordDto.Token && 
                                        t.Email == resetPasswordDto.Email && 
                                        !t.IsUsed && 
                                        t.ExpiresAt > DateTime.UtcNow);

            if (resetToken == null)
            {
                return false;
            }

            // Update user password
            resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.Password);
            resetToken.User.UpdatedAt = DateTime.UtcNow;

            // Mark token as used
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successfully for user {UserId}", resetToken.UserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for token {Token}", resetPasswordDto.Token);
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false;
            }

            // Verify current password
            if (string.IsNullOrEmpty(user.PasswordHash) || 
                !BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash))
            {
                return false;
            }

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return false;
        }
    }

    private string GenerateSecureToken()
    {
        var randomNumber = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}