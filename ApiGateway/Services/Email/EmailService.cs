using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpClient _smtpClient;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _smtpClient = new SmtpClient
        {
            Host = _configuration["Email:SmtpHost"],
            Port = int.Parse(_configuration["Email:SmtpPort"]),
            EnableSsl = true,
            Credentials = new NetworkCredential(
                _configuration["Email:Username"],
                _configuration["Email:Password"])
        };
    }

    public async Task SendEmailConfirmationAsync(string email, string name, string confirmationLink)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_configuration["Email:FromAddress"], _configuration["Email:FromName"]),
            Subject = "Confirm your email",
            IsBodyHtml = true,
            Body = GenerateEmailConfirmationTemplate(name, confirmationLink)
        };
        message.To.Add(email);

        try
        {
            await _smtpClient.SendMailAsync(message);
            _logger.LogInformation("Confirmation email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending confirmation email to {Email}", email);
            throw;
        }
    }

    private string GenerateEmailConfirmationTemplate(string name, string confirmationLink)
    {
        return $@"
            <h2>Welcome {name}!</h2>
            <p>Thank you for registering. Please confirm your email by clicking the link below:</p>
            <p><a href='{confirmationLink}'>Confirm Email</a></p>
            <p>If you didn't register for an account, please ignore this email.</p>";
    }
} 