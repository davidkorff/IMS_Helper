using Microsoft.AspNetCore.Mvc;
using ApiGateway.Models.Claims;
using ApiGateway.Services;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClaimsController : ControllerBase
    {
        private readonly IClaimsService _claimsService;
        private readonly ILogger<ClaimsController> _logger;

        public ClaimsController(
            IClaimsService claimsService,
            ILogger<ClaimsController> logger)
        {
            _claimsService = claimsService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ClaimResponse>> FileNewClaim(CreateClaimRequest request)
        {
            var claim = await _claimsService.CreateClaim(request);
            return Ok(claim);
        }

        [HttpGet("{claimId}")]
        public async Task<ActionResult<ClaimResponse>> GetClaim(string claimId)
        {
            var claim = await _claimsService.GetClaim(claimId);
            return Ok(claim);
        }

        [HttpGet("policy/{policyId}")]
        public async Task<ActionResult<List<ClaimResponse>>> GetPolicyClaims(string policyId)
        {
            var claims = await _claimsService.GetPolicyClaims(policyId);
            return Ok(claims);
        }

        [HttpPost("{claimId}/documents")]
        public async Task<ActionResult<DocumentResponse>> AddClaimDocument(
            string claimId,
            IFormFile file,
            [FromForm] ClaimDocumentMetadata metadata)
        {
            var document = await _claimsService.AddClaimDocument(claimId, file, metadata);
            return Ok(document);
        }

        [HttpPatch("{claimId}/status")]
        public async Task<ActionResult<ClaimResponse>> UpdateClaimStatus(
            string claimId,
            UpdateClaimStatusRequest request)
        {
            var claim = await _claimsService.UpdateClaimStatus(claimId, request);
            return Ok(claim);
        }
    }
} 