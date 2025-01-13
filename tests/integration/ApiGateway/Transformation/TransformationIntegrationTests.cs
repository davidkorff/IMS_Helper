using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using ApiGateway;

public class TransformationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public TransformationIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task JsonTransformation_WorksEndToEnd()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestContent = new
        {
            oldName = "test",
            removeMe = "value",
            nested = new { oldKey = "value" }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestContent),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/transform-test", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var transformed = JsonDocument.Parse(responseContent);
        
        Assert.True(transformed.RootElement.TryGetProperty(
            "newName", 
            out var _));
        Assert.False(transformed.RootElement.TryGetProperty(
            "removeMe", 
            out var _));
    }

    [Fact]
    public async Task XmlTransformation_WorksEndToEnd()
    {
        // Arrange
        var client = _factory.CreateClient();
        var requestContent = @"
            <root>
                <oldName>test</oldName>
                <removeMe>value</removeMe>
                <nested>
                    <oldKey>value</oldKey>
                </nested>
            </root>";

        var content = new StringContent(
            requestContent,
            Encoding.UTF8,
            "application/xml");

        // Act
        var response = await client.PostAsync("/api/transform-test", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var doc = new XmlDocument();
        doc.LoadXml(responseContent);

        Assert.NotNull(doc.SelectSingleNode("//newName"));
        Assert.Null(doc.SelectSingleNode("//removeMe"));
    }

    [Fact]
    public async Task LargeContent_HandledCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();
        var largeObject = new { data = new string('x', 1000000) };
        var content = new StringContent(
            JsonSerializer.Serialize(largeObject),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/transform-test", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
} 