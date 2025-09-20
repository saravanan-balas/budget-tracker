using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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
        var resetUrl = $"{_configuration["AppSettings:FrontendUrl"]}/auth/reset-password?token={resetToken}";
        
        var subject = "Reset Your Password - Budget Tracker";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Password Reset</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #3b82f6; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8fafc; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; background-color: #3b82f6; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Password Reset Request</h1>
        </div>
        <div class='content'>
            <h2>Hello {firstName}!</h2>
            <p>We received a request to reset your password for your Budget Tracker account.</p>
            <p>Click the button below to reset your password:</p>
            <a href='{resetUrl}' class='button'>Reset My Password</a>
            <p>This link will expire in 1 hour for security reasons.</p>
            <p>If you didn't request this password reset, please ignore this email. Your password will remain unchanged.</p>
            <p>If the button doesn't work, you can copy and paste this link into your browser:</p>
            <p style='word-break: break-all; background-color: #e5e7eb; padding: 10px; border-radius: 4px;'>{resetUrl}</p>
        </div>
        <div class='footer'>
            <p>Best regards,<br>The Budget Tracker Team</p>
            <p><small>This is an automated message. Please do not reply to this email.</small></p>
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
            // Check if SendGrid is configured
            var sendGridApiKey = _configuration["Email:SendGridApiKey"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Budget Tracker";

            // If SendGrid is not configured, fall back to SMTP or log for development
            if (string.IsNullOrEmpty(sendGridApiKey))
            {
                return await SendEmailViaSmtpAsync(to, subject, body, isHtml);
            }

            // Send via SendGrid
            return await SendEmailViaSendGridAsync(to, subject, body, fromEmail, fromName, isHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }

    private async Task<bool> SendEmailViaSendGridAsync(string to, string subject, string body, string fromEmail, string fromName, bool isHtml)
    {
        try
        {
            var sendGridApiKey = _configuration["Email:SendGridApiKey"];
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {sendGridApiKey}");

            var emailData = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = to } }
                    }
                },
                from = new { email = fromEmail, name = fromName },
                subject = subject,
                content = new[]
                {
                    new
                    {
                        type = isHtml ? "text/html" : "text/plain",
                        value = body
                    }
                }
            };

            var json = JsonSerializer.Serialize(emailData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.sendgrid.com/v3/mail/send", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully via SendGrid to {Email}", to);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SendGrid API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SendGrid to {Email}", to);
            return false;
        }
    }

    private async Task<bool> SendEmailViaSmtpAsync(string to, string subject, string body, bool isHtml)
    {
        try
        {
            // Check if SMTP is configured
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Budget Tracker";

            // If email is not configured, log the email content for development
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
            {
                _logger.LogInformation("Email not configured. Would send email to {To} with subject '{Subject}':\n{Body}", 
                    to, subject, body);
                return true; // Return true for development
            }

            using var client = new SmtpClient(smtpHost, smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail ?? "noreply@budgettracker.com", fromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = isHtml;

            await client.SendMailAsync(message);
            
            _logger.LogInformation("Email sent successfully via SMTP to {Email}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP to {Email}", to);
            return false;
        }
    }
}
