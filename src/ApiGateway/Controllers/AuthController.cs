using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IIMSAuthenticationService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IIMSAuthenticationService authService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] AuthRequest request)
        {
            try
            {
                var result = await _authService.AuthenticateAsync(request);
                if (!result.IsSuccessful)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = result.ErrorMessage
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred during authentication"
                });
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                if (!result.IsSuccessful)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        Message = result.ErrorMessage
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred during token refresh"
                });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                await _authService.RevokeTokenAsync(request.RefreshToken);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                return StatusCode(500, new ErrorResponse
                {
                    Message = "An error occurred during logout"
                });
            }
        }
    }

    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }
} 