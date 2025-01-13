using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

public class IMSClientTests
{
    private readonly IMSClient _client;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<ILogger<IMSClient>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public IMSClientTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _loggerMock = new Mock<ILogger<IMSClient>>();
        _configMock = new Mock<IConfiguration>();
        
        _configMock.Setup(x => x["IMS:BaseUrl"])
            .Returns("https://test-ims.com/");

        _client = new IMSClient(
            _httpClientMock.Object,
            _loggerMock.Object,
            _configMock.Object
        );
    }

    [Fact]
    public async Task GetAuthToken_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var expectedToken = "test-token";
        var soapResponse = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                <soap:Body>
                    <LoginResponse>
                        <Token>{expectedToken}</Token>
                    </LoginResponse>
                </soap:Body>
            </soap:Envelope>";

        _httpClientMock.Setup(x => x.PostAsync(
            It.IsAny<string>(),
            It.IsAny<HttpContent>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                Content = new StringContent(soapResponse)
            });

        // Act
        var token = await _client.GetAuthToken("TEST", "test@test.com", "password");

        // Assert
        Assert.Equal(expectedToken, token);
    }

    [Fact]
    public async Task GetAuthToken_InvalidCredentials_ThrowsException()
    {
        // Arrange
        _httpClientMock.Setup(x => x.PostAsync(
            It.IsAny<string>(),
            It.IsAny<HttpContent>()))
            .ThrowsAsync(new HttpRequestException());

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.GetAuthToken("TEST", "test@test.com", "password"));
    }
} 