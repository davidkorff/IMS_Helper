using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Shared.Integration.IMS
{
    [Collection("IMS Integration Tests")]
    public class IMSClientIntegrationTests : IAsyncLifetime
    {
        private readonly IMSClient _client;
        private readonly ILogger<IMSClient> _logger;
        private readonly IMSSettings _settings;
        private string _authToken;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;

        public IMSClientIntegrationTests()
        {
            _config = IMSTestConfiguration.Load();
            
            _settings = new IMSSettings
            {
                BaseUrl = _config.BaseUrl,
                ProgramCode = _config.ProgramCode,
                ClientId = _config.ClientId,
                RetryPolicy = _config.RetryPolicy
            };

            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            }).CreateLogger<IMSClient>();

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_settings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _client = new IMSClient(
                httpClient,
                _logger,
                Options.Create(_settings));

            _cleanup = new IMSTestCleanup(_client, _logger);
        }

        public async Task InitializeAsync()
        {
            // Login before each test
            _authToken = await _client.LoginAsync(
                _settings.ProgramCode,
                Environment.GetEnvironmentVariable("IMS_TEST_EMAIL"),
                Environment.GetEnvironmentVariable("IMS_TEST_PASSWORD"));

            Assert.NotNull(_authToken);
        }

        public async Task DisposeAsync()
        {
            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task GetValidCompanyLines_ReturnsExpectedStructure()
        {
            // Act
            var response = await _client.GetValidCompanyLines(_settings.ProgramCode);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.CompanyLines);

            var companyLine = response.CompanyLines.First();
            Assert.NotNull(companyLine.CompanyLineGUID);
            Assert.NotNull(companyLine.LocationName);
            Assert.NotNull(companyLine.LineName);
            Assert.NotEmpty(companyLine.BillTypes);
        }

        [Fact]
        public async Task CreateSubmission_ValidData_ReturnsSubmissionGuid()
        {
            // Arrange
            var companyLines = await _client.GetValidCompanyLines(_settings.ProgramCode);
            var companyLine = companyLines.CompanyLines.First();
            var office = companyLine.Offices.First();
            var underwriter = companyLine.Users.First();

            var request = new SubmissionRequest
            {
                InsuredGuid = await CreateTestInsured(),
                ProducerContactGuid = await GetTestProducer(),
                UnderwriterGuid = underwriter.UserGUID
            };

            // Act
            var submissionGuid = await _client.CreateSubmission(request);

            // Assert
            Assert.NotNull(submissionGuid);
            Assert.Matches("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", submissionGuid);
        }

        [Fact]
        public async Task CreateQuote_ValidSubmission_ReturnsQuoteGuid()
        {
            // Arrange
            var companyLines = await _client.GetValidCompanyLines(_settings.ProgramCode);
            var companyLine = companyLines.CompanyLines.First();
            var office = companyLine.Offices.First();
            var underwriter = companyLine.Users.First();

            var submissionGuid = await _client.CreateSubmission(new SubmissionRequest
            {
                InsuredGuid = await CreateTestInsured(),
                ProducerContactGuid = await GetTestProducer(),
                UnderwriterGuid = underwriter.UserGUID
            });

            var request = new QuoteRequest
            {
                SubmissionGuid = submissionGuid,
                QuotingLocationGuid = office.OfficeGuid,
                IssuingLocationGuid = office.OfficeGuid,
                CompanyLocationGuid = companyLine.CompanyLocationGUID,
                LineGuid = companyLine.LineGUID,
                StateId = companyLine.StateID,
                ProducerContactGuid = await GetTestProducer(),
                QuoteStatusId = 1, // Submitted
                EffectiveDate = DateTime.UtcNow.AddDays(1),
                ExpirationDate = DateTime.UtcNow.AddDays(366),
                BillingTypeId = companyLine.BillTypes.First().BillingTypeID,
                UnderwriterGuid = underwriter.UserGUID,
                PolicyTypeId = 1, // New
                CostCenterId = 1
            };

            // Act
            var quoteGuid = await _client.CreateQuote(request);

            // Assert
            Assert.NotNull(quoteGuid);
            Assert.Matches("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", quoteGuid);
        }

        [Fact]
        public async Task CreateDocument_ValidQuote_ReturnsDocumentId()
        {
            // Arrange
            var quoteGuid = await CreateTestQuote();

            // Act
            var documentId = await _client.CreateQuoteDocument(quoteGuid);

            // Assert
            Assert.NotNull(documentId);
            
            // Verify we can retrieve the document
            var content = await _client.GetDocument(documentId);
            Assert.NotNull(content);
            Assert.True(content.Length > 0);
        }

        private async Task<string> CreateTestInsured()
        {
            var request = new AddInsuredRequest
            {
                Name = $"Test Insured {DateTime.UtcNow:yyyyMMddHHmmss}",
                Address1 = "123 Test St",
                City = "Test City",
                State = "TX",
                Zip = "12345",
                Phone = "1234567890",
                Email = "test@example.com"
            };

            var insuredGuid = await _client.AddInsured(request);
            _cleanup.TrackInsured(insuredGuid);
            return insuredGuid;
        }

        private async Task<string> GetTestProducer()
        {
            // In a real test environment, you'd either:
            // 1. Have a known test producer GUID
            // 2. Create a test producer
            // 3. Query for an existing producer
            return Environment.GetEnvironmentVariable("IMS_TEST_PRODUCER_GUID");
        }

        private async Task<string> CreateTestQuote()
        {
            var companyLines = await _client.GetValidCompanyLines(_settings.ProgramCode);
            var companyLine = companyLines.CompanyLines.First();
            var office = companyLine.Offices.First();
            var underwriter = companyLine.Users.First();

            var submissionGuid = await _client.CreateSubmission(new SubmissionRequest
            {
                InsuredGuid = await CreateTestInsured(),
                ProducerContactGuid = await GetTestProducer(),
                UnderwriterGuid = underwriter.UserGUID
            });

            var request = new QuoteRequest
            {
                SubmissionGuid = submissionGuid,
                QuotingLocationGuid = office.OfficeGuid,
                IssuingLocationGuid = office.OfficeGuid,
                CompanyLocationGuid = companyLine.CompanyLocationGUID,
                LineGuid = companyLine.LineGUID,
                StateId = companyLine.StateID,
                ProducerContactGuid = await GetTestProducer(),
                QuoteStatusId = 1,
                EffectiveDate = DateTime.UtcNow.AddDays(1),
                ExpirationDate = DateTime.UtcNow.AddDays(366),
                BillingTypeId = companyLine.BillTypes.First().BillingTypeID,
                UnderwriterGuid = underwriter.UserGUID,
                PolicyTypeId = 1,
                CostCenterId = 1
            };

            var quoteGuid = await _client.CreateQuote(request);
            _cleanup.TrackQuote(quoteGuid);
            return quoteGuid;
        }
    }
} 