namespace BudgetTracker.Common.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string firstName);
    Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
}
