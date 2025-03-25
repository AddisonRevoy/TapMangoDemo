
# TapMango SMS Throttler

A .NET Core API that sends SMS messages while enforcing rate limits at both global and per-sender levels.


## Configuration (appsettings.json)

Example configuration:
```json
{
  "SmsSettings": {
    "RequestsPerSecond": 5,
    "DefaultSenderLimit": 2,
    "SenderInactivitySeconds": 300,
    "SenderLimits": {
      "+1111111111": 3,
      "+2222222222": 4
    }
  }
}
```
* RequestsPerSecond → Global API rate limit
* DefaultSenderLimit → Limit for senders not in SenderLimits
* SenderInactivitySeconds → Removes inactive senders after X seconds
* SenderLimits → Custom per-sender limits, formated as "<SenderNumber>" : <Limit>

## API Endpoints
### Send SMS
**`POST /phonenumber/send`**  
#### Request Body:
```json
{
  "phoneNumber": "+1234567890",
  "message": "Hello!"
}
```
#### Responses:
| Status Code | Description |
|------------|-------------|
| `200 OK` | SMS sent successfully |
| `400 Bad Request` | Invalid request format |
| `429 Too Many Requests` | Rate limit exceeded |
