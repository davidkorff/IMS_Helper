using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Linq;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountService> _logger;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountService> logger,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<(bool success, string[] errors)> RegisterAsync(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            TimeZone = request.TimeZone,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("User created a new account with password");

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = GenerateEmailConfirmationLink(user.Email, code);
            
            await _emailService.SendEmailConfirmationAsync(
                user.Email,
                user.FirstName,
                callbackUrl);

            return (true, Array.Empty<string>());
        }

        return (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool success, string[] errors)> LoginAsync(LoginRequest request)
    {
        var result = await _signInManager.PasswordSignInAsync(
            request.Email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            
            _logger.LogInformation("User logged in");
            return (true, Array.Empty<string>());
        }

        if (result.RequiresTwoFactor)
        {
            return (false, new[] { "Requires two-factor authentication" });
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out");
            return (false, new[] { "Account is locked out" });
        }

        return (false, new[] { "Invalid login attempt" });
    }

    private string GenerateEmailConfirmationLink(string email, string code)
    {
        // Implementation for generating confirmation link
        return $"{_configuration["AppUrl"]}/confirm-email?email={email}&code={code}";
    }
} 