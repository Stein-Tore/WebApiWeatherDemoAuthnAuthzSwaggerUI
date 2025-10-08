# Testing the Endpoints

## Token Configuration

Tokens are in `appsettings.json`:

```json
{
  "Authentication": {
    "BearerTokens": {
      "demo-token-12345": {
        "UserId": "user1",
        "Roles": ["User"],
        "AllowedIPs": []
      },
      "demo-token-67890": {
        "UserId": "user2",
        "Roles": ["User", "DataReader"],
        "AllowedIPs": []
      },
      "partner1-token-xyz": {
        "UserId": "partner1",
        "Roles": ["Partner1", "DataReader"],
        "AllowedIPs": ["127.0.0.1", "::1"]
      },
      "admin-token-123": {
        "UserId": "admin1",
        "Roles": ["Admin", "DataWriter", "DataReader"],
        "AllowedIPs": []
      }
    }
  }
}
```

**Properties**:
- **UserId**: User identifier
- **Roles**: Array of roles for authorization
- **AllowedIPs**: IP whitelist (empty = no restrictions)

## Public Endpoints

No authentication needed:

```bash
curl https://localhost:7025/publicweather/weatherforecast
```

**Expected**: 200 OK with weather data

## Private Endpoints (Bearer Token)

### Without Token
```bash
curl -i https://localhost:7025/privateweather/weatherforecast
```
**Expected**: 401 Unauthorized with `WWW-Authenticate: Bearer` header

### With Valid Token
```bash
curl -H "Authorization: Bearer demo-token-12345" \
  https://localhost:7025/privateweather/weatherforecast
```
**Expected**: 200 OK with weather data

## Partner Endpoints (Role + IP Whitelist)

### Without Token
```bash
curl -i https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected**: 401 Unauthorized

### Wrong Role
```bash
curl -i -H "Authorization: Bearer demo-token-12345" \
  https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected**: 403 Forbidden (user1 doesn't have Partner1 role)

### Correct Role + Allowed IP
```bash
curl -H "Authorization: Bearer partner1-token-xyz" \
  https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected**: 200 OK (if calling from localhost)

### Different Requirements for GET vs POST

**GET /data** - Requires DataReader role only:
```bash
curl -H "Authorization: Bearer demo-token-67890" \
  https://localhost:7025/privateclient1weather/data
```
**Expected**: 200 OK

**POST /data** - Requires DataWriter role + IP whitelist:
```bash
curl -X POST -H "Authorization: Bearer admin-token-123" \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}' \
  https://localhost:7025/privateclient1weather/data
```
**Expected**: 200 OK if IP matches (or AllowedIPs is empty)

## Same-Host Endpoints

### From Same Machine
```bash
curl https://localhost:7025/samehostweather/weatherforecast
```
**Expected**: 200 OK

### From Remote Machine
```bash
curl https://your-server:7025/samehostweather/weatherforecast
```
**Expected**: 403 Forbidden

## Same-Host Policy Configuration

The policy automatically accepts:
- Loopback: 127.0.0.1, ::1
- Local NIC IPs: Any IP bound to network interfaces

For cloud/NAT scenarios (AWS EC2, Azure VM):

```json
{
  "Authorization": {
    "SameHostPolicy": {
      "AdditionalSameHostIPs": ["52.19.108.174"]
    }
  }
}
```

**When to use**:
- Your public IP appears due to hairpin NAT
- Behind load balancer where same-machine calls show external IP
- Prefer calling via localhost/private IP if possible

## Adding Production Tokens

For your remote server, add to `appsettings.Production.json`:

```json
{
  "Authentication": {
    "BearerTokens": {
      "prod-partner1-a1b2c3": {
        "UserId": "partner1-prod",
        "Roles": ["Partner1", "DataReader"],
        "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
      }
    }
  }
}
```

**Generating secure tokens**:

```powershell
# PowerShell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

```bash
# Linux/Mac
openssl rand -base64 32
```

## Security Notes

### Defense-in-Depth

Partner endpoints enforce multiple layers:
1. Valid bearer token
2. Required role (e.g., Partner1)
3. IP whitelist (if configured)

All must pass for access.

### RFC 6750 Compliance

- **401 Unauthorized**: No token or invalid token (includes WWW-Authenticate header)
- **403 Forbidden**: Valid token but wrong role/IP (no WWW-Authenticate header)

### Best Practices

- Always use HTTPS in production
- Don't commit tokens to source control
- Use environment-specific config files
- Consider migrating to database for production
- Add token expiration
- Implement rate limiting
- Monitor authentication failures

## Extending This Demo

### Add Rate Limiting

```csharp
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("api-rate-limit", opt => {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});

// Apply to endpoint
.RequireRateLimiting("api-rate-limit")
```

### Migrate to Database

Replace `ConfigurationOpaqueTokenValidator` with a database implementation:

```csharp
public class DatabaseOpaqueTokenValidator : IOpaqueTokenValidator
{
    private readonly IDbConnection _db;
    
    public async Task<bool> ValidateTokenAsync(string token)
    {
        return await _db.QuerySingleOrDefaultAsync<bool>(
            "SELECT COUNT(*) FROM Tokens WHERE Token = @Token AND ExpiresAt > GETUTCDATE()",
            new { Token = token });
    }
    
    // ... implement GetTokenConfigurationAsync similarly
}
```

### Add Token Expiration

Extend `TokenConfiguration`:

```csharp
public class TokenConfiguration
{
    public string UserId { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public string[] AllowedIPs { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }  // Add this
}
```

Validate in authentication handler:

```csharp
if (tokenConfig.ExpiresAt.HasValue && tokenConfig.ExpiresAt.Value < DateTime.UtcNow)
{
    return AuthenticateResult.Fail("Token expired");
}
