# Quote Management API

## Overview
The Quote Management API provides endpoints for creating, retrieving, and binding insurance quotes through the IMS system.

## Authentication
All endpoints require an API key passed in the `X-API-Key` header.

## Rate Limiting
- Standard tier: 1,000 requests per hour
- Premium tier: 5,000 requests per hour

## Endpoints

### Create Quote
Creates a new insurance quote in the IMS system.

**Endpoint:** `POST /api/quotes`

**Request Headers:** 
X-API-Key: your-api-key
Content-Type: application/json

**Request Body:**
json
{
"programCode": "string",
"insured": {
"firstName": "string",
"lastName": "string",
"email": "string",
"phone": "string",
"address": {
"street1": "string",
"street2": "string",
"city": "string",
"state": "string",
"zipCode": "string"
}
},
"coverage": {
"lineOfBusiness": "string",
"effectiveDate": "string",
"expirationDate": "string",
"coverages": ["string"]
},
"locations": [
{
"address": {
"street1": "string",
"street2": "string",
"city": "string",
"state": "string",
"zipCode": "string"
},
"buildingType": "string",
"buildingValue": 0,
"contentsValue": 0
}
],
"premium": {
"basePremium": 0,
"tax": 0,
"fee": 0,
"totalPremium": 0
}
}

# Quote Management API

## Overview
The Quote Management API provides endpoints for creating, retrieving, and binding insurance quotes through the IMS system.

## Authentication
All endpoints require an API key passed in the `X-API-Key` header.

## Rate Limiting
- Standard tier: 1,000 requests per hour
- Premium tier: 5,000 requests per hour

## Endpoints

### Create Quote
Creates a new insurance quote in the IMS system.

**Endpoint:** `POST /api/quotes`

**Request Headers:**
json
{
"X-API-Key": "your-api-key",
"Content-Type": "application/json"
}

**Request Body:**
json
{
"programCode": "string",
"insured": {
"firstName": "string",
"lastName": "string",
"email": "string",
"phone": "string",
"address": {
"street1": "string",
"street2": "string",
"city": "string",
"state": "string",
"zipCode": "string"
}
},
"coverage": {
"lineOfBusiness": "string",
"effectiveDate": "string",
"expirationDate": "string",
"coverages": ["string"]
},
"locations": [
{
"address": {
"street1": "string",
"street2": "string",
"city": "string",
"state": "string",
"zipCode": "string"
},
"buildingType": "string",
"buildingValue": 0,
"contentsValue": 0
}
],
"premium": {
"basePremium": 0,
"tax": 0,
"fee": 0,
"totalPremium": 0
}
}

**Success Response:** `200 OK`

json
{
"quoteId": "Q123456",
"status": "QUOTED",
"premium": {
"basePremium": 1000.00,
"tax": 80.00,
"fee": 25.00,
"totalPremium": 1105.00
}
}


### Get Quote
Retrieves an existing quote by ID.

**Endpoint:** `GET /api/quotes/{quoteId}`

**Success Response:** `200 OK`

json
{
"quoteId": "Q123456",
"status": "QUOTED",
"insured": {
"firstName": "John",
"lastName": "Doe",
"email": "john@example.com",
"phone": "1234567890",
"address": {
"street1": "123 Main St",
"city": "Anytown",
"state": "ST",
"zipCode": "12345"
}
},
"coverage": {
"lineOfBusiness": "BOP",
"effectiveDate": "2024-01-01",
"expirationDate": "2025-01-01",
"coverages": ["GL", "PROPERTY"]
},
"premium": {
"basePremium": 1000.00,
"tax": 80.00,
"fee": 25.00,
"totalPremium": 1105.00
}
}


### Bind Quote
Converts a quote into a policy.

**Endpoint:** `POST /api/quotes/{quoteId}/bind`

**Success Response:** `200 OK`

json
{
"policyId": "P123456",
"status": "ACTIVE",
"effectiveDate": "2024-01-01",
"expirationDate": "2025-01-01"
}


## Error Responses

All errors follow this format:

json
{
"code": "ERROR_CODE",
"message": "Human readable message",
"traceId": "request-trace-id",
"details": {} // Optional additional information
}


### Error Codes
| Code | Description |
|------|-------------|
| `QUOTE_CREATE_ERROR` | Failed to create quote |
| `QUOTE_GET_ERROR` | Failed to retrieve quote |
| `QUOTE_BIND_ERROR` | Failed to bind quote |
| `VALIDATION_ERROR` | Request validation failed |
| `AUTH_ERROR` | Authentication failed |
| `INTERNAL_ERROR` | Unexpected server error |

## Rate Limit Headers

http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 2024-01-01T00:00:00Z


## Code Examples

### C#

csharp
var client = new IMSApiClient("your-api-key");
var request = new CreateQuoteRequest
{
ProgramCode = "TEST",
Insured = new InsuredInfo
{
FirstName = "John",
LastName = "Doe",
Email = "john@example.com"
}
};
var quote = await client.CreateQuoteAsync(request);


### Python

python
from ims_api_client import IMSClient
client = IMSClient("your-api-key")
quote = client.create_quote({
"programCode": "TEST",
"insured": {
"firstName": "John",
"lastName": "Doe",
"email": "john@example.com"
}
})


## Support
- Email: api-support@example.com
- Documentation: https://docs.example.com/api
- Status Page: https://status.example.com