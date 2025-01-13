using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    /// <summary>
    /// Manages API rate limiting policies
    /// </summary>
    [ApiController]
    [Route("api/rate-limits")]
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public class RateLimitPolicyController : ControllerBase
    {
        private readonly IRateLimitPolicyManager _policyManager;
        private readonly ILogger<RateLimitPolicyController> _logger;

        public RateLimitPolicyController(
            IRateLimitPolicyManager policyManager,
            ILogger<RateLimitPolicyController> logger)
        {
            _policyManager = policyManager;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all configured rate limit policies
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/rate-limits
        ///     
        /// </remarks>
        /// <returns>A list of all rate limit policies</returns>
        /// <response code="200">Returns the list of policies</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RateLimitPolicyConfig>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<RateLimitPolicyConfig>>> GetAllPolicies()
        {
            var policies = await _policyManager.GetAllPoliciesAsync();
            return Ok(policies);
        }

        /// <summary>
        /// Retrieves a specific rate limit policy by its path pattern
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/rate-limits/api%2Fusers%2F%2A%2A
        ///     
        /// The pattern should be URL encoded. Examples of valid patterns:
        /// * /api/users/** - Matches all paths under /api/users
        /// * /api/products/* - Matches immediate children of /api/products
        /// * /api/orders/{id} - Matches exact pattern with parameter
        /// </remarks>
        /// <param name="pattern">The URL-encoded path pattern</param>
        /// <returns>The matching rate limit policy</returns>
        /// <response code="200">Returns the requested policy</response>
        /// <response code="404">If the policy doesn't exist</response>
        [HttpGet("{pattern}")]
        [ProducesResponseType(typeof(RateLimitPolicyConfig), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RateLimitPolicyConfig>> GetPolicy(
            string pattern)
        {
            var policy = await _policyManager.GetPolicyAsync(
                WebUtility.UrlDecode(pattern));
            
            if (policy == null)
                return NotFound();

            return Ok(policy);
        }

        /// <summary>
        /// Creates a new rate limit policy
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/rate-limits
        ///     {
        ///         "pathPattern": "/api/users/**",
        ///         "requestsPerMinute": 100,
        ///         "burstLimit": 10,
        ///         "excludedPaths": [
        ///             "/api/users/health"
        ///         ],
        ///         "clientSpecificLimits": {
        ///             "trusted-client": 200
        ///         },
        ///         "penaltyConfig": {
        ///             "violationThreshold": 3,
        ///             "penaltyDuration": "00:05:00",
        ///             "penaltyMultiplier": 2.0,
        ///             "maxPenaltyMultiplier": 8
        ///         },
        ///         "enabled": true,
        ///         "expiresAt": "2024-12-31T23:59:59Z",
        ///         "requiredScopes": [
        ///             "read:users"
        ///         ]
        ///     }
        ///     
        /// </remarks>
        /// <param name="policy">The rate limit policy configuration</param>
        /// <returns>The created policy</returns>
        /// <response code="201">Returns the created policy</response>
        /// <response code="400">If the policy configuration is invalid</response>
        [HttpPost]
        [ProducesResponseType(typeof(RateLimitPolicyConfig), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RateLimitPolicyConfig>> CreatePolicy(
            RateLimitPolicyConfig policy)
        {
            var isValid = await _policyManager.ValidatePolicyAsync(policy);
            if (!isValid)
                return BadRequest("Invalid policy configuration");

            await _policyManager.AddOrUpdatePolicyAsync(policy);
            
            _logger.LogInformation(
                "Rate limit policy created for pattern: {Pattern}",
                policy.PathPattern);

            return CreatedAtAction(
                nameof(GetPolicy),
                new { pattern = WebUtility.UrlEncode(policy.PathPattern) },
                policy);
        }

        /// <summary>
        /// Updates an existing rate limit policy
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     PUT /api/rate-limits/api%2Fusers%2F%2A%2A
        ///     {
        ///         "pathPattern": "/api/users/**",
        ///         "requestsPerMinute": 150,
        ///         "burstLimit": 15
        ///     }
        ///     
        /// The path pattern in the URL must match the pattern in the policy body.
        /// </remarks>
        /// <param name="pattern">The URL-encoded path pattern</param>
        /// <param name="policy">The updated policy configuration</param>
        /// <returns>No content on success</returns>
        /// <response code="204">If the policy was successfully updated</response>
        /// <response code="400">If the policy configuration is invalid</response>
        /// <response code="404">If the policy doesn't exist</response>
        [HttpPut("{pattern}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UpdatePolicy(
            string pattern,
            RateLimitPolicyConfig policy)
        {
            pattern = WebUtility.UrlDecode(pattern);
            if (pattern != policy.PathPattern)
                return BadRequest("Path pattern mismatch");

            var existing = await _policyManager.GetPolicyAsync(pattern);
            if (existing == null)
                return NotFound();

            var isValid = await _policyManager.ValidatePolicyAsync(policy);
            if (!isValid)
                return BadRequest("Invalid policy configuration");

            await _policyManager.AddOrUpdatePolicyAsync(policy);
            
            _logger.LogInformation(
                "Rate limit policy updated for pattern: {Pattern}",
                pattern);

            return NoContent();
        }

        /// <summary>
        /// Deletes a rate limit policy
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// 
        ///     DELETE /api/rate-limits/api%2Fusers%2F%2A%2A
        ///     
        /// </remarks>
        /// <param name="pattern">The URL-encoded path pattern</param>
        /// <returns>No content on success</returns>
        /// <response code="204">If the policy was successfully deleted</response>
        /// <response code="404">If the policy doesn't exist</response>
        [HttpDelete("{pattern}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeletePolicy(string pattern)
        {
            pattern = WebUtility.UrlDecode(pattern);
            var existing = await _policyManager.GetPolicyAsync(pattern);
            if (existing == null)
                return NotFound();

            await _policyManager.RemovePolicyAsync(pattern);
            
            _logger.LogInformation(
                "Rate limit policy deleted for pattern: {Pattern}",
                pattern);

            return NoContent();
        }
    }
} 