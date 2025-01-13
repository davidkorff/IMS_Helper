using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using IMS.Gateway.Api.Models.Account;
using IMS.Gateway.Api.Services.IMSConnection;
using IMS.Gateway.Api.Services.ApiKey;
using IMS.Gateway.Api.Exceptions;
using IMS.Gateway.Api.Models.IMSConnection;
using IMS.Gateway.Api.Models.ApiKey;
using IMS.Gateway.Api.Models.Profile;

namespace IMS.Gateway.Api.Services.Account
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIMSConnectionService _imsConnectionService;
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            UserManager<ApplicationUser> userManager,
            IIMSConnectionService imsConnectionService,
            IApiKeyService apiKeyService,
            ILogger<ProfileService> logger)
        {
            _userManager = userManager;
            _imsConnectionService = imsConnectionService;
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        public async Task<ProfileResponse> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var imsConnections = await _imsConnectionService.GetConnectionsAsync(userId);
            var apiKeys = await _apiKeyService.GetApiKeysAsync(userId);

            return new ProfileResponse
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                CompanyName = user.CompanyName,
                TimeZone = user.TimeZone,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                TwoFactorEnabled = user.TwoFactorEnabled,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IMSConnections = imsConnections,
                ApiKeys = apiKeys
            };
        }

        public async Task<ProfileResponse> UpdateProfileAsync(string userId, UpdateProfileRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.CompanyName = request.CompanyName;
            user.TimeZone = request.TimeZone;
            user.PhoneNumber = request.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new ValidationException(result.Errors.First().Description);
            }

            return await GetProfileAsync(userId);
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                throw new ValidationException(result.Errors.First().Description);
            }

            _logger.LogInformation("User {UserId} changed their password successfully", userId);
        }

        public async Task<Enable2FAResponse> Enable2FAAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            return new Enable2FAResponse
            {
                SharedKey = unformattedKey,
                QrCodeUri = GenerateQrCodeUri(user.Email, unformattedKey)
            };
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            const string authenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
            return string.Format(
                authenticatorUriFormat,
                Uri.EscapeDataString("IMS Gateway"),
                Uri.EscapeDataString(email),
                unformattedKey);
        }
    }
}