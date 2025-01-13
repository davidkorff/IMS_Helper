using System;
using System.Net;
using System.Xml.Linq;
using Xunit;

public class IMSErrorMappingTests
{
    [Fact]
    public void MapSoapFaultToRestError_AuthenticationError_ReturnsUnauthorized()
    {
        // Arrange
        var fault = CreateSoapFault(
            "Invalid credentials",
            "IMS.Auth.InvalidCredentials");

        // Act
        var error = IMSErrorMapping.MapSoapFaultToRestError(fault);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal("AUTH001", error.ErrorCode);
        Assert.NotNull(error.TraceId);
        Assert.NotEmpty(error.Details);
    }

    [Fact]
    public void MapSoapFaultToRestError_ValidationError_ReturnsBadRequest()
    {
        // Arrange
        var fault = CreateSoapFault(
            "Missing required fields",
            "IMS.Validation.MissingRequired",
            new[]
            {
                CreateValidationError("PolicyNumber", "VAL001", "Policy number is required"),
                CreateValidationError("EffectiveDate", "VAL002", "Effective date is required")
            });

        // Act
        var error = IMSErrorMapping.MapSoapFaultToRestError(fault);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("VAL002", error.ErrorCode);
        Assert.Equal(2, error.Details.Count);
        Assert.Contains(error.Details, d => d.Field == "PolicyNumber");
        Assert.Contains(error.Details, d => d.Field == "EffectiveDate");
    }

    [Fact]
    public void MapSoapFaultToRestError_BusinessError_ReturnsAppropriateStatus()
    {
        // Arrange
        var fault = CreateSoapFault(
            "Entity not found",
            "IMS.Business.EntityNotFound",
            new[]
            {
                CreateBusinessError("Policy", "BUS001", "Policy POL123 not found")
            });

        // Act
        var error = IMSErrorMapping.MapSoapFaultToRestError(fault);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal("BUS001", error.ErrorCode);
        Assert.Single(error.Details);
        Assert.Contains(error.Details, d => d.Field == "Policy");
    }

    [Fact]
    public void MapSoapFaultToRestError_SystemError_ReturnsInternalServerError()
    {
        // Arrange
        var fault = CreateSoapFault(
            "Internal system error",
            "IMS.System.InternalError");

        // Act
        var error = IMSErrorMapping.MapSoapFaultToRestError(fault);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Equal("SYS003", error.ErrorCode);
    }

    [Fact]
    public void MapSoapFaultToRestError_UnknownError_ReturnsDefaultMapping()
    {
        // Arrange
        var fault = CreateSoapFault(
            "Unknown error occurred",
            "IMS.Unknown.Error");

        // Act
        var error = IMSErrorMapping.MapSoapFaultToRestError(fault);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Equal("GEN002", error.ErrorCode);
    }

    private static SoapFault CreateSoapFault(
        string message, 
        string errorCode, 
        XElement[] details = null)
    {
        var detailElement = new XElement("Detail",
            new XElement("ErrorCode", errorCode));

        if (details != null)
        {
            foreach (var detail in details)
            {
                detailElement.Add(detail);
            }
        }

        return new SoapFault
        {
            Message = message,
            Detail = detailElement
        };
    }

    private static XElement CreateValidationError(
        string field, 
        string code, 
        string message)
    {
        return new XElement("ValidationError",
            new XElement("Field", field),
            new XElement("Code", code),
            new XElement("Message", message));
    }

    private static XElement CreateBusinessError(
        string entity, 
        string rule, 
        string description)
    {
        return new XElement("BusinessError",
            new XElement("Entity", entity),
            new XElement("Rule", rule),
            new XElement("Description", description));
    }
} 