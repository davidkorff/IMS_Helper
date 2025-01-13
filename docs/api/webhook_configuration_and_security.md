# Webhook Configuration & Security

## Configuration

### Developer Portal Setup
1. Log into the developer portal at `https://developers.example.com`
2. Navigate to "Webhooks" → "Configuration"
3. Add webhook endpoints:

json
{
"url": "https://your-domain.com/webhooks",
"events": ["quote.created", "quote.bound", "policy.renewal_notice"],
"description": "Production webhook endpoint",
"version": "v1"
}


### Environment-Specific Configuration
Configure different endpoints for each environment:

json
{
"environments": {
"production": {
"url": "https://api.your-domain.com/webhooks",
"secret": "prod_whsec_..."
},
"staging": {
"url": "https://staging.your-domain.com/webhooks",
"secret": "stage_whsec_..."
}
}
}


### Webhook Headers
Configure which headers you receive:

json
{
"headers": {
"X-IMS-Event": true,
"X-IMS-Delivery-ID": true,
"X-IMS-Signature": true,
"X-IMS-Timestamp": true,
"Custom-Header": "custom-value"
}
}


## Security Best Practices

### 1. Signature Verification
Always verify webhook signatures:

python
Python Example
import hmac
import hashlib
def verify_webhook(payload, signature_header, webhook_secret):
expected_signature = hmac.new(
webhook_secret.encode('utf-8'),
payload.encode('utf-8'),
hashlib.sha256
).hexdigest()
return hmac.compare_digest(
expected_signature,
signature_header
)

csharp
// C# Example
private bool VerifyWebhookSignature(string payload, string signatureHeader, string webhookSecret)
{
using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret)))
{
var payloadBytes = Encoding.UTF8.GetBytes(payload);
var hash = hmac.ComputeHash(payloadBytes);
var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();
return computedSignature == signatureHeader;
}
}


### 2. TLS Requirements
- Minimum TLS version: 1.2
- Strong cipher suites only
- Valid SSL certificate from trusted CA
- Regular certificate rotation

### 3. IP Allowlisting
Whitelist IMS webhook IPs:

text
18.XXX.XXX.XXX/32
52.XXX.XXX.XXX/32
54.XXX.XXX.XXX/32


### 4. Request Validation
Implement these checks:

python
def validate_webhook_request(request):
# 1. Verify timestamp is recent
timestamp = request.headers.get('X-IMS-Timestamp')
if abs(time.time() - int(timestamp)) > 300: # 5 minute window
return False
# 2. Check for replay attacks
delivery_id = request.headers.get('X-IMS-Delivery-ID')
if delivery_id_previously_processed(delivery_id):
return False
# 3. Verify signature
if not verify_webhook_signature(...):
return False
return True


### 5. Secure Storage

python
class WebhookSecret:
def init(self):
self.secret_manager = SecretManager()
def get_webhook_secret(self):
# Use secure secret management
return self.secret_manager.get_secret('webhook_secret')
def rotate_webhook_secret(self):
# Implement regular secret rotation
new_secret = generate_secure_secret()
self.secret_manager.update_secret('webhook_secret', new_secret)
return new_secret


### 6. Rate Limiting
Configure rate limits:

json
{
"rate_limiting": {
"max_requests_per_second": 10,
"burst_size": 20,
"timeout_seconds": 30
}
}


### 7. Error Handling

python
def handle_webhook():
try:
# 1. Verify request is valid
if not validate_webhook_request(request):
return error_response(401, "Invalid webhook request")
# 2. Process asynchronously
process_webhook.delay(request.json)
# 3. Return 200 quickly
return success_response(200, "Webhook accepted")
except Exception as e:
# 4. Log error details
log_webhook_error(e)
# 5. Return appropriate error
return error_response(500, "Webhook processing failed")


### 8. Monitoring & Alerts
Configure alerts for:
- Failed signature verifications
- High error rates
- Response time anomalies
- Certificate expiration
- Secret rotation reminders

### 9. Audit Logging

python
def log_webhook_event(webhook_data):
audit_log.info({
'event_type': webhook_data['event'],
'delivery_id': webhook_data['delivery_id'],
'timestamp': webhook_data['timestamp'],
'ip_address': request.remote_addr,
'verification_status': 'success',
'processing_time_ms': processing_time
})


### 10. Disaster Recovery
- Implement webhook queue backup
- Store failed webhooks for replay
- Maintain backup endpoints
- Document recovery procedures

## Testing & Validation
Use our webhook test tool:

bash
curl -X POST https://api.example.com/webhook/test \
-H "X-API-Key: your_api_key" \
-d '{"test_mode": true, "event": "quote.created"}'