using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/quotes")]
    [Authorize(Policy = "RequireApiKey")]
    public class QuotesController : ControllerBase
    {
        [HttpPost]
        [Authorize(Policy = "RequireQuotePermission")]
        public async Task<IActionResult> CreateQuote([FromBody] CreateQuoteRequest request)
        {
            var apiKeyId = User.Claims.FirstOrDefault(c => c.Type == "ApiKeyId")?.Value;
            var environment = User.Claims.FirstOrDefault(c => c.Type == "Environment")?.Value;
            
            // Your implementation here
        }

        [HttpGet]
        [Authorize(Policy = "RequireProductionKey")]
        public async Task<IActionResult> ListQuotes()
        {
            // Only accessible with production API keys
        }
    }
} 