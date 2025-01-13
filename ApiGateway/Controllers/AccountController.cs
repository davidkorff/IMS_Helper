using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiGateway.Models;
using ApiGateway.Services;
using ApiGateway.Services.Auth;
using ApiGateway.Services.Email;
using ApiGateway.Services.Jwt;
using ApiGateway.Services.Account;
using ApiGateway.Models.Requests;
using ApiGateway.Models.Responses;
using ApiGateway.Models.Errors;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAccountService accountService,
            IJwtService jwtService,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _jwtService = jwtService;
            _logger = logger;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var (success, errors) = await _accountService.RegisterAsync(request);
            
            if (!success)
            {
                return BadRequest(new ErrorResponse { Errors = errors });
            }

            return CreatedAtAction(nameof(GetAccount), new { email = request.Email }, 
                new SuccessResponse { Message = "Registration successful. Please check your email to confirm your account." });
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (success, errors) = await _accountService.LoginAsync(request);
            
            if (!success)
            {
                return BadRequest(new ErrorResponse { Errors = errors });
            }

            var token = await _jwtService.GenerateTokenAsync(request.Email);
            
            return Ok(new LoginResponse 
            { 
                Token = token,
                ExpiresIn = 3600 // 1 hour
            });
        }

        [Authorize]
        [HttpGet]
        [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAccount()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var account = await _accountService.GetAccountAsync(email);
            
            return Ok(account);
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout()
        {
            await _accountService.LogoutAsync();
            return NoContent();
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string code)
        {
            var (success, errors) = await _accountService.ConfirmEmailAsync(email, code);
            
            if (!success)
            {
                return BadRequest(new ErrorResponse { Errors = errors });
            }

            return Ok(new SuccessResponse { Message = "Email confirmed successfully." });
        }
    }
} 