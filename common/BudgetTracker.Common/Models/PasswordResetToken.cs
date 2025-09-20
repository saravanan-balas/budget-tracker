using System;

namespace BudgetTracker.Common.Models;

public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    
    // Navigation property
    public User User { get; set; } = null!;
}
