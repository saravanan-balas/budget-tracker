using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Common.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string firstName)
    {
        var frontendUrl = _configuration["AppSettings:FrontendUrl"];
        var resetUrl = $"{frontendUrl}/auth/reset-password?token={resetToken}";

        var subject = "Reset your BudgetVu password";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <title>Reset your password</title>
  <style>
    body {{ font-family: Arial, sans-serif; background-color: #f3f4f6; margin: 0; padding: 0; }}
    .wrapper {{ max-width: 560px; margin: 40px auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }}
    .header {{ background: linear-gradient(135deg, #2563eb, #7c3aed); padding: 32px 24px; text-align: center; }}
    .header h1 {{ color: #ffffff; margin: 0; font-size: 22px; font-weight: 700; letter-spacing: -0.3px; }}
    .header p {{ color: #bfdbfe; margin: 6px 0 0; font-size: 14px; }}
    .body {{ padding: 32px 24px; color: #374151; }}
    .body h2 {{ margin: 0 0 12px; font-size: 18px; color: #111827; }}
    .body p {{ margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #6b7280; }}
    .btn {{ display: inline-block; background-color: #2563eb; color: #ffffff; padding: 13px 28px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 15px; margin: 8px 0 20px; }}
    .fallback {{ background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 6px; padding: 10px 14px; font-size: 13px; color: #6b7280; word-break: break-all; }}
    .footer {{ text-align: center; padding: 20px 24px; font-size: 12px; color: #9ca3af; border-top: 1px solid #f3f4f6; }}
  </style>
</head>
<body>
  <div class='wrapper'>
    <div class='header'>
      <h1>BudgetVu</h1>
      <p>Password Reset Request</p>
    </div>
    <div class='body'>
      <h2>Hi {firstName},</h2>
      <p>We received a request to reset the password for your BudgetVu account. Click the button below to choose a new password.</p>
      <a href='{resetUrl}' class='btn'>Reset My Password</a>
      <p>This link expires in 1 hour. If you did not request a password reset, you can safely ignore this email.</p>
      <p style='font-size:13px; color:#9ca3af;'>If the button above does not work, paste this link into your browser:</p>
      <div class='fallback'>{resetUrl}</div>
    </div>
    <div class='footer'>
      <p>BudgetVu. This is an automated message, please do not reply.</p>
    </div>
  </div>
</body>
</html>";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            var connectionString = _configuration["AzureCommunication:ConnectionString"];
            var senderAddress = _configuration["AzureCommunication:SenderAddress"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(senderAddress))
            {
                _logger.LogWarning("Azure Communication Services not configured. Would send email to {Email}: {Subject}", to, subject);
                return true;
            }

            var emailClient = new EmailClient(connectionString);

            var emailContent = new EmailContent(subject);
            if (isHtml)
                emailContent.Html = body;
            else
                emailContent.PlainText = body;

            var message = new EmailMessage(
                senderAddress: senderAddress,
                content: emailContent,
                recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(to) })
            );

            var operation = await emailClient.SendAsync(WaitUntil.Completed, message);

            if (operation.HasValue && operation.Value.Status == EmailSendStatus.Succeeded)
            {
                _logger.LogInformation("Email sent via Azure Communication Services to {Email}", to);
                return true;
            }

            _logger.LogError("Azure email send failed with status: {Status}", operation.Value?.Status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }
}
