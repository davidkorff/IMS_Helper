using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiGateway.Models.Account;
using ApiGateway.Services.Account;
using ApiGateway.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            IProfileService profileService,
            ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var profile = await _profileService.GetProfileAsync(userId);
            return Ok(profile);
        }

        [HttpPut]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            try
            {
                var profile = await _profileService.UpdateProfileAsync(userId, request);
                return Ok(profile);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new ErrorResponse { Errors = new[] { ex.Message } });
            }
        }

        [HttpPost("change-password")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            try
            {
                await _profileService.ChangePasswordAsync(userId, request);
                return NoContent();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new ErrorResponse { Errors = new[] { ex.Message } });
            }
        }

        [HttpPost("phone/verify")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _profileService.VerifyPhoneNumberAsync(userId, request.Code);
            return NoContent();
        }

        [HttpPost("2fa/enable")]
        [ProducesResponseType(typeof(Enable2FAResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Enable2FA()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var response = await _profileService.Enable2FAAsync(userId);
            return Ok(response);
        }

        [HttpPost("2fa/verify")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Verify2FA([FromBody] Verify2FARequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _profileService.Verify2FAAsync(userId, request.Code);
            return NoContent();
        }
    }
} 