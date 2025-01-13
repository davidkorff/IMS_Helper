using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ApiGateway.Transformation.Validation;

public class SchemaValidationTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<SchemaValidator>> _loggerMock;
    private readonly SchemaValidator _jsonValidator;
    private readonly XsdValidator _xmlValidator;

    public SchemaValidationTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<SchemaValidator>>();
        
        _jsonValidator = new SchemaValidator(
            _loggerMock.Object,
            _cacheMock.Object);
            
        _xmlValidator = new XsdValidator(
            _loggerMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task JsonValidation_ValidContent_Succeeds()
    {
        // Arrange
        var schema = @"{
            'type': 'object',
            'properties': {
                'name': { 'type': 'string' }
            }
        }";

        var content = new MemoryStream(
            Encoding.UTF8.GetBytes("{ 'name': 'test' }"));

        // Act & Assert
        await _jsonValidator.ValidateAsync(
            content,
            schema,
            "application/json");
    }

    [Fact]
    public async Task JsonValidation_InvalidContent_ThrowsException()
    {
        // Arrange
        var schema = @"{
            'type': 'object',
            'properties': {
                'name': { 'type': 'string' }
            },
            'required': ['name']
        }";

        var content = new MemoryStream(
            Encoding.UTF8.GetBytes("{}"));

        // Act & Assert
        await Assert.ThrowsAsync<SchemaValidationException>(() =>
            _jsonValidator.ValidateAsync(
                content,
                schema,
                "application/json"));
    }

    [Fact]
    public async Task XmlValidation_ValidContent_Succeeds()
    {
        // Arrange
        var schema = @"<?xml version='1.0'?>
            <xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>
                <xs:element name='root'>
                    <xs:complexType>
                        <xs:sequence>
                            <xs:element name='name' type='xs:string'/>
                        </xs:sequence>
                    </xs:complexType>
                </xs:element>
            </xs:schema>";

        var content = new MemoryStream(
            Encoding.UTF8.GetBytes(
                @"<?xml version='1.0'?>
                <root>
                    <name>test</name>
                </root>"));

        // Act & Assert
        await _xmlValidator.ValidateAsync(
            content,
            schema,
            "application/xml");
    }
} 