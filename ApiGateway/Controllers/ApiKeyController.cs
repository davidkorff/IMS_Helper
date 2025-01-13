using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiGateway.Models.ApiKey;
using ApiGateway.Services;
using ApiGateway.Models;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/keys")]
    [Authorize(Roles = "Admin")]
    public class ApiKeyController : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<ApiKeyController> _logger;

        public ApiKeyController(
            IApiKeyService apiKeyService,
            ILogger<ApiKeyController> logger)
        {
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request)
        {
            try
            {
                var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(accountId))
                {
                    return BadRequest(new ErrorResponse { Message = "Invalid account" });
                }

                var options = new ApiKeyOptions
                {
                    Name = request.Name,
                    Environment = request.Environment,
                    Permissions = request.Permissions,
                    RateLimit = request.RateLimit,
                    ExpiresAt = request.ExpiresAt
                };

                var apiKey = await _apiKeyService.GenerateApiKeyAsync(accountId, options);

                var response = new ApiKeyResponse
                {
                    Id = apiKey.Id,
                    Key = apiKey.PlaintextKey, // Only returned once during creation
                    Name = apiKey.Name,
                    Environment = apiKey.Environment,
                    Permissions = apiKey.Permissions,
                    RateLimit = apiKey.RateLimit,
                    ExpiresAt = apiKey.ExpiresAt,
                    CreatedAt = apiKey.CreatedAt
                };

                return CreatedAtAction(nameof(GetApiKey), new { id = apiKey.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating API key");
                return StatusCode(500, new ErrorResponse { Message = "Error creating API key" });
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetApiKey(string id)
        {
            try
            {
                var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var apiKey = await _apiKeyService.GetApiKeyAsync(accountId, id);

                if (apiKey == null)
                {
                    return NotFound(new ErrorResponse { Message = "API key not found" });
                }

                var response = new ApiKeyResponse
                {
                    Id = apiKey.Id,
                    Name = apiKey.Name,
                    Environment = apiKey.Environment,
                    Permissions = apiKey.Permissions,
                    RateLimit = apiKey.RateLimit,
                    ExpiresAt = apiKey.ExpiresAt,
                    CreatedAt = apiKey.CreatedAt,
                    Status = apiKey.Status
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API key");
                return StatusCode(500, new ErrorResponse { Message = "Error retrieving API key" });
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ApiKeyResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListApiKeys([FromQuery] ApiKeyListRequest request)
        {
            try
            {
                var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var apiKeys = await _apiKeyService.ListApiKeysAsync(accountId, request);

                var response = apiKeys.Select(k => new ApiKeyResponse
                {
                    Id = k.Id,
                    Name = k.Name,
                    Environment = k.Environment,
                    Permissions = k.Permissions,
                    RateLimit = k.RateLimit,
                    ExpiresAt = k.ExpiresAt,
                    CreatedAt = k.CreatedAt,
                    Status = k.Status
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing API keys");
                return StatusCode(500, new ErrorResponse { Message = "Error listing API keys" });
            }
        }

        [HttpPost("{id}/revoke")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeApiKey(string id)
        {
            try
            {
                var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await _apiKeyService.RevokeApiKeyAsync(accountId, id);
                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "API key not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key");
                return StatusCode(500, new ErrorResponse { Message = "Error revoking API key" });
            }
        }
    }
} 