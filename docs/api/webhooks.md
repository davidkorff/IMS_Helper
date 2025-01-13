# Webhook Events

## Overview
Webhooks allow you to receive real-time notifications when events occur in the IMS system. To use webhooks, configure your endpoint URL in the developer portal.

## Configuration
- Webhook URLs must be HTTPS
- Webhooks will retry 3 times with exponential backoff
- Each request includes a signature for verification

## Authentication
Each webhook request includes an `X-Webhook-Signature` header. Verify this signature using your webhook secret:

python
import hmac
import hashlib
def verify_signature(payload, signature, secret):
expected = hmac.new(
secret.encode('utf-8'),
payload.encode('utf-8'),
hashlib.sha256
).hexdigest()
return hmac.compare_digest(expected, signature)


## Events

### quote.created
Triggered when a new quote is created.

json
{
"event": "quote.created",
"timestamp": "2024-01-01T12:00:00Z",
"data": {
"quoteId": "Q123456",
"status": "QUOTED",
"programCode": "TEST",
"premium": {
"totalPremium": 1105.00
}
}
}


### quote.bound
Triggered when a quote is bound into a policy.

json
{
"event": "quote.bound",
"timestamp": "2024-01-01T12:00:00Z",
"data": {
"quoteId": "Q123456",
"policyId": "P123456",
"status": "ACTIVE",
"effectiveDate": "2024-01-01"
}
}


### quote.updated
Triggered when a quote is modified.

json
{
"event": "quote.updated",
"timestamp": "2024-01-01T12:00:00Z",
"data": {
"quoteId": "Q123456",
"status": "MODIFIED",
"changes": [
{
"field": "premium.totalPremium",
"oldValue": 1000.00,
"newValue": 1105.00
}
]
}
}


### policy.renewal_notice
Triggered when a policy is up for renewal.

json
{
"event": "policy.renewal_notice",
"timestamp": "2024-01-01T12:00:00Z",
"data": {
"policyId": "P123456",
"expirationDate": "2024-12-31",
"renewalQuoteId": "Q789012",
"daysUntilExpiration": 60
}
}


## Error Handling

### Retry Policy
- First retry: 30 seconds
- Second retry: 5 minutes
- Third retry: 30 minutes

### Webhook Failures
If a webhook fails all retries:
1. Event is stored in failure queue
2. Notification is sent to configured email
3. Event can be manually retried via API

### Example Error Response
Your endpoint should return 2xx for success. Any other response triggers retry:

json
{
"error": {
"code": "PROCESSING_ERROR",
"message": "Failed to process webhook",
"details": {
"reason": "Database connection failed"
}
}
}


## Best Practices
1. Verify webhook signatures
2. Process webhooks asynchronously
3. Return 200 quickly, handle processing in background
4. Store raw webhook data before processing
5. Implement idempotency checks

## Testing
Use the test endpoint to send sample webhooks:

bash
curl -X POST https://api.example.com/webhooks/test \
-H "X-API-Key: your-api-key" \
-d '{"event": "quote.created"}'


## Monitoring
Monitor webhook delivery in the developer portal:
- Success/failure rates
- Response times
- Error logs
- Retry status

## Support
Contact api-support@example.com for webhook assistance