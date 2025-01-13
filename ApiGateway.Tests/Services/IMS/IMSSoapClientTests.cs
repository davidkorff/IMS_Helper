using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Xunit;
using ApiGateway.Services.IMS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class IMSSoapClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<IMSSoapClient>> _mockLogger;
    private readonly IMSSettings _settings;
    private readonly IMSSoapClient _client;

    public IMSSoapClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<IMSSoapClient>>();
        _settings = new IMSSettings
        {
            BaseUrl = "http://ims-test.com",
            SoapEndpoint = "/soap"
        };

        _client = new IMSSoapClient(
            _httpClient,
            _mockLogger.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var username = "testuser";
        var password = "testpass";
        var expectedToken = "test-token-123";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($@"
                    <soap:Envelope>
                        <soap:Body>
                            <AuthenticationResponse>
                                <token>{expectedToken}</token>
                            </AuthenticationResponse>
                        </soap:Body>
                    </soap:Envelope>")
            });

        // Act
        var token = await _client.AuthenticateAsync(username, password);

        // Assert
        Assert.Equal(expectedToken, token);
        
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString() == 
                        $"{_settings.BaseUrl}{_settings.SoapEndpoint}"),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidCredentials_ThrowsException()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent(
                    "<soap:Fault><faultstring>Invalid credentials</faultstring></soap:Fault>")
            });

        // Act & Assert
        await Assert.ThrowsAsync<IMSAuthenticationException>(() =>
            _client.AuthenticateAsync("wrong", "wrong"));
    }
} 