using Microsoft.AspNetCore.Mvc;
using System.Net;

public class BaseApiController : ControllerBase
{
    protected IActionResult HandleValidationResponse(ValidationResult validationResult)
    {
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Message = "Validation failed",
                Errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
            });
        }

        return Ok();
    }
}

public class IMSController : BaseApiController
{
    private readonly IIMSService _imsService;
    private readonly IValidator<QuoteRequest> _quoteValidator;

    public IMSController(IIMSService imsService, IValidator<QuoteRequest> quoteValidator)
    {
        _imsService = imsService;
        _quoteValidator = quoteValidator;
    }

    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote([FromBody] QuoteRequest request)
    {
        var validationResult = await _quoteValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return HandleValidationResponse(validationResult);
        }

        try
        {
            var result = await _imsService.CreateQuoteAsync(request);
            return Ok(result);
        }
        catch (IMSException ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, 
                new ErrorResponse { Message = ex.Message });
        }
    }
} 