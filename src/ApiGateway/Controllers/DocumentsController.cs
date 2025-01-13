using Microsoft.AspNetCore.Mvc;
using ApiGateway.Services.Documents;
using ApiGateway.Models;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IIMSClient _imsClient;
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IIMSClient imsClient,
            IDocumentService documentService,
            ILogger<DocumentsController> logger)
        {
            _imsClient = imsClient;
            _documentService = documentService;
            _logger = logger;
        }

        [HttpGet("quotes/{quoteId}")]
        public async Task<ActionResult<List<DocumentResponse>>> GetQuoteDocuments(string quoteId)
        {
            var documents = await _documentService.GetQuoteDocuments(quoteId);
            return Ok(documents);
        }

        [HttpGet("policies/{policyId}")]
        public async Task<ActionResult<List<DocumentResponse>>> GetPolicyDocuments(string policyId)
        {
            var documents = await _documentService.GetPolicyDocuments(policyId);
            return Ok(documents);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<DocumentResponse>> UploadDocument(IFormFile file, [FromForm] DocumentMetadata metadata)
        {
            var document = await _documentService.UploadDocument(file, metadata);
            return Ok(document);
        }

        [HttpGet("{documentId}/download")]
        public async Task<IActionResult> DownloadDocument(string documentId)
        {
            var document = await _documentService.GetDocument(documentId);
            return File(document.Content, document.ContentType, document.FileName);
        }
    }
} 