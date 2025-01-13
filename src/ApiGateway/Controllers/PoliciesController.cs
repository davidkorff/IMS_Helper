using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoliciesController : ControllerBase
    {
        private readonly IIMSClient _imsClient;
        private readonly IPolicyService _policyService;
        private readonly ILogger<PoliciesController> _logger;

        public PoliciesController(
            IIMSClient imsClient,
            IPolicyService policyService,
            ILogger<PoliciesController> logger)
        {
            _imsClient = imsClient;
            _policyService = policyService;
            _logger = logger;
        }

        [HttpGet("{policyId}")]
        public async Task<ActionResult<PolicyResponse>> GetPolicy(string policyId)
        {
            var policy = await _policyService.GetPolicy(policyId);
            return Ok(policy);
        }

        [HttpPost("{policyId}/endorse")]
        public async Task<ActionResult<EndorsementResponse>> CreateEndorsement(
            string policyId, 
            EndorsementRequest request)
        {
            var endorsement = await _policyService.CreateEndorsement(policyId, request);
            return Ok(endorsement);
        }

        [HttpPost("{policyId}/cancel")]
        public async Task<ActionResult<CancellationResponse>> CancelPolicy(
            string policyId, 
            CancellationRequest request)
        {
            var cancellation = await _policyService.CancelPolicy(policyId, request);
            return Ok(cancellation);
        }

        [HttpPost("{policyId}/reinstate")]
        public async Task<ActionResult<PolicyResponse>> ReinstatePolicy(
            string policyId, 
            ReinstatementRequest request)
        {
            var policy = await _policyService.ReinstatePolicy(policyId, request);
            return Ok(policy);
        }

        [HttpGet("{policyId}/history")]
        public async Task<ActionResult<List<PolicyHistoryEntry>>> GetPolicyHistory(string policyId)
        {
            var history = await _policyService.GetPolicyHistory(policyId);
            return Ok(history);
        }
    }
} 