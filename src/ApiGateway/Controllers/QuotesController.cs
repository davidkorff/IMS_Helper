using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;
using FluentValidation;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuotesController : ControllerBase
    {
        private readonly IIMSClient _imsClient;
        private readonly ILogger<QuotesController> _logger;
        private readonly IValidator<CreateQuoteRequest> _validator;

        public QuotesController(
            IIMSClient imsClient,
            ILogger<QuotesController> logger,
            IValidator<CreateQuoteRequest> validator)
        {
            _imsClient = imsClient;
            _logger = logger;
            _validator = validator;
        }

        [HttpPost]
        [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<QuoteResponse>> CreateQuote(CreateQuoteRequest request)
        {
            var validationResult = await _validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors.Select(e => 
                    new ValidationError { Field = e.PropertyName, Message = e.ErrorMessage }));
            }

            var quote = await _imsClient.CreateQuote(request);
            return Ok(quote);
        }

        [HttpGet("{quoteId}")]
        public async Task<ActionResult<QuoteResponse>> GetQuote(string quoteId)
        {
            var quote = await _imsClient.GetQuote(quoteId);
            return Ok(quote);
        }

        [HttpPost("{quoteId}/bind")]
        public async Task<ActionResult<PolicyResponse>> BindQuote(string quoteId)
        {
            var policy = await _imsClient.BindQuote(quoteId);
            return Ok(policy);
        }
    }
} 