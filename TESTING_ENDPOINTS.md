# Testing the Protected Endpoints

## Overview

The API now has four types of endpoints with different access controls:

1. **Public Endpoints** (`/publicweather/*`) - Open to everyone
2. **Private Endpoints** (`/privateweather/*`) - Require opaque bearer token authentication
3. **Partner-Specific Endpoints** (`/privateclient1weather/*`) - Require specific role + IP whitelist
4. **Same-Host Endpoints** (`/samehostweather/*`) - Only accessible from the same machine (same host)

## Configuration

Bearer tokens are configured in `appsettings.json` with roles and IP restrictions:

```json
{
  "Authentication": {
    "BearerTokens": {
      "demo-token-12345": {
        "UserId": "user1",
        "Roles": ["User"],
        "AllowedIPs": []
      },
      "partner1-token-xyz": {
        "UserId": "partner1",
        "Roles": ["Partner1", "DataReader"],
        "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
      },
      "admin-token-123": {
        "UserId": "admin",
        "Roles": ["Admin", "DataWriter", "DataReader"],
        "AllowedIPs": []
      }
    }
  }
}
```

**Token Configuration Properties:**
- **UserId**: User identifier
- **Roles**: Array of roles for authorization
- **AllowedIPs**: Array of IP addresses allowed to use this token. Empty array = no IP restrictions.

### Authorization Policies

The following policies are configured:

| Policy | Requirements |
|--------|-------------|
| `BearerTokenPolicy` | Valid bearer token |
| `Partner1Policy` | Valid token + `Partner1` role + IP whitelist check |
| `DataReaderPolicy` | Valid token + `DataReader` role |
| `DataWriterPolicy` | Valid token + `DataWriter` role + IP whitelist check |
| `SameHostPolicy` | Request from same machine (no token required) |

### Same-Host Policy Configuration

The `SameHostPolicy` allows requests originating from the same machine where the API runs. By default, it accepts:
- **Loopback addresses**: 127.0.0.1, ::1, and IPv6-mapped IPv4 loopback
- **Local NIC addresses**: Any IP address assigned to an active network interface on the machine

For cloud/NAT scenarios (e.g., AWS EC2 with Elastic IP, hairpin NAT), you may need to add additional IPs:

```json
{
  "Authorization": {
    "SameHostPolicy": {
      "AdditionalSameHostIPs": [
        "52.19.108.174"
      ]
    }
  }
}
```

**When to use `AdditionalSameHostIPs`:**
- **AWS EC2 with Elastic IP**: If your web app calls the API via the public EIP, the request appears to come from the EIP (hairpin NAT). Add the EIP to `AdditionalSameHostIPs`.
- **Azure VMs with public IP**: Similar behavior may occur depending on your network configuration.
- **Behind NAT/proxy**: If the API sees the NAT gateway's IP instead of localhost when called from the same machine.

**Best practices:**
- Prefer calling the API via `localhost`, `127.0.0.1`, or the instance's private IP (e.g., `172.31.21.104` on AWS) to avoid hairpin NAT.
- Only add public IPs to `AdditionalSameHostIPs` if hairpin NAT is unavoidable.
- Do not use `SameHostPolicy` if your web app runs on a different machine than the API.

### Adding New Tokens

To add new tokens for your remote test server:

1. Open `appsettings.json` (or `appsettings.Production.json` for production)
2. Add entries to the `Authentication:BearerTokens` section:
   ```json
   "your-token-here": {
     "UserId": "username",
     "Roles": ["Partner1", "DataReader"],
     "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
   }
   ```
3. Restart the application

**Example for production:**
```json
{
  "Authentication": {
    "BearerTokens": {
      "prod-partner1-a1b2c3": {
        "UserId": "partner1-prod",
        "Roles": ["Partner1", "DataReader"],
        "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
      },
      "prod-admin-d4e5f6": {
        "UserId": "admin-prod",
        "Roles": ["Admin", "DataWriter", "DataReader"],
        "AllowedIPs": []
      }
    }
  }
}
```

## Testing Public Endpoints

No authentication required:

```bash
curl https://localhost:7025/publicweather/weatherforecast
```

**Expected:** Returns weather forecast data (200 OK)

## Testing Private Endpoints (Bearer Token)

### Without Token (Should Fail)
```bash
curl -i https://localhost:7025/privateweather/weatherforecast
```
**Expected:** 
- Status: 401 Unauthorized
- Header: `WWW-Authenticate: Bearer realm="localhost:7025", charset="UTF-8"`

### With Valid Token (Should Succeed)
```bash
curl -H "Authorization: Bearer demo-token-12345" https://localhost:7025/privateweather/weatherforecast
```
**Expected:** Returns weather forecast data (200 OK)

## Testing Partner-Specific Endpoints (Role + IP Whitelist)

### Partner1 Endpoints

#### Without Token (Should Fail - 401)
```bash
curl -i https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected:** 401 Unauthorized

#### With Token but Wrong Role (Should Fail - 403)
```bash
curl -i -H "Authorization: Bearer demo-token-12345" https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected:** 403 Forbidden (user1 doesn't have Partner1 role)

#### With Token and Role from Allowed IP (Should Succeed)
```bash
curl -H "Authorization: Bearer partner1-token-xyz" https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected:** 200 OK (if you're testing from 127.0.0.1)

#### With Token and Role from Disallowed IP (Should Fail - 403)
If your IP is not in the AllowedIPs list:
```bash
curl -H "Authorization: Bearer partner1-token-xyz" https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected:** 403 Forbidden

### GET vs POST Authorization Example

The `/privateclient1weather/data` endpoints demonstrate different authorization for GET vs POST:

**GET - Requires DataReader role (no IP restriction):**
```bash
curl -H "Authorization: Bearer demo-token-67890" https://localhost:7025/privateclient1weather/data
```
**Expected:** 200 OK (user2 has DataReader role)

**POST - Requires DataWriter role + IP whitelist:**
```bash
curl -X POST -H "Authorization: Bearer admin-token-123" \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}' \
  https://localhost:7025/privateclient1weather/data
```
**Expected:** 
- 200 OK if admin has IP restrictions matching your IP or empty AllowedIPs
- 403 Forbidden if IP doesn't match

## Testing Same-Host Endpoints (Same Machine Only)

### From Same Machine (Should Succeed)

**Using localhost:**
```bash
curl https://localhost:7025/samehostweather/weatherforecast
```
**Expected:** Returns weather forecast data (200 OK)

**Using machine's IP address:**
```bash
curl https://192.168.1.100:7025/samehostweather/weatherforecast
```
**Expected:** Returns weather forecast data (200 OK) - the API recognizes the IP as belonging to the same machine

**On AWS EC2 (with configured EIP):**
```bash
curl https://your-hostname:7025/samehostweather/weatherforecast
```
**Expected:** Returns weather forecast data (200 OK) - if the EIP is added to `AdditionalSameHostIPs`

### From Remote Machine (Should Fail)
```bash
curl https://your-server:7025/samehostweather/weatherforecast
```
**Expected:** 403 Forbidden (request originates from a different machine)

### Troubleshooting Same-Host Access

If you're getting 401/403 errors when calling from the same machine:

1. **Check what IP the API sees:**
   ```bash
   curl https://your-host/diagnostics/remoteip
   ```
   This shows the `RemoteIpAddress` as seen by the API.

2. **Check which IPs are considered "same-host":**
   ```bash
   # Run this from the server itself
   curl https://localhost/diagnostics/localaddresses
   ```
   This shows all addresses the policy accepts (requires same-host access to work first).

3. **Common issues:**
   - **Hairpin NAT (AWS/cloud)**: The API sees your public IP instead of localhost. Add it to `AdditionalSameHostIPs`.
   - **Behind load balancer/proxy**: The API may see the proxy's IP. Either:
     - Call the API directly via localhost/private IP
     - Add the proxy IP to `AdditionalSameHostIPs`
     - Consider using forwarded headers middleware (see Production Notes below)

## Configured Demo Tokens

| Token | User ID | Roles | Allowed IPs | Use Case |
|-------|---------|-------|-------------|----------|
| `demo-token-12345` | user1 | User | None | Basic authenticated user |
| `demo-token-67890` | user2 | User, DataReader | None | User with read permissions |
| `test-token-abcde` | testuser | User | None | Test user |
| `partner1-token-xyz` | partner1 | Partner1, DataReader | 127.0.0.1, ::1 | Partner with IP restrictions (localhost only for demo) |
| `admin-token-123` | admin | Admin, DataWriter, DataReader | None | Admin with all permissions |

## RFC 6750 Compliance

The authentication implementation follows [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) (Bearer Token Usage):

- **401 Unauthorized**: Returned when authentication is missing or invalid
  - Includes `WWW-Authenticate: Bearer` header with realm
  
- **403 Forbidden**: Returned when authenticated but lacking permissions (role or IP)
  - No `WWW-Authenticate` header (authentication already succeeded)

## Defense-in-Depth Security

For partner endpoints, multiple security layers are enforced:

1. **Bearer Token** - Must provide valid token
2. **Role** - Token must have required role (e.g., Partner1)
3. **IP Whitelist** - Request must originate from allowed IP (if configured)

**Example: Partner1 with IP restrictions**
```json
"partner1-token": {
  "UserId": "partner1",
  "Roles": ["Partner1", "DataReader"],
  "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
}
```

This token will only work if:
- ? Token is valid
- ? User has `Partner1` role
- ? Request comes from `203.0.113.10` or `203.0.113.11`

If any layer fails, request is rejected with 401 (no token) or 403 (wrong role/IP).

## Production Notes

### Token Management

**Generating secure tokens:**
```powershell
# PowerShell - Generate a random token
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

```bash
# Linux/Mac - Generate a random token
openssl rand -base64 32
```

### IP Address Configuration

**Important considerations:**
- Use your partner's public IP addresses
- If behind NAT/proxy, get the actual source IP (may need `X-Forwarded-For` header handling)
- IPv4 and IPv6 are both supported (IPv6-mapped IPv4 addresses are automatically normalized)
- Empty `AllowedIPs` array = no IP restrictions

**Getting your public IP:**
```bash
curl https://api.ipify.org
```

### Same-Host Policy in Production

**Network configuration matters:**

1. **Direct deployment (no proxy):**
   - No additional configuration needed
   - API automatically detects loopback and NIC addresses

2. **Cloud with public IP (AWS EC2, Azure VM):**
   - If hairpin NAT occurs, add public IP to `AdditionalSameHostIPs`
   - Better: call API via localhost or private IP from web app

3. **Behind reverse proxy/load balancer:**
   - Option A: Add proxy IP to `AdditionalSameHostIPs`
   - Option B: Enable forwarded headers (see below)
   - Option C: Use a different authentication method for inter-app communication

**Using forwarded headers (advanced):**

If your API sits behind a reverse proxy and you need to see the original client IP:

```csharp
// In Program.cs, BEFORE app.UseAuthentication()
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = 
        ForwardedHeaders.XForwardedFor | 
        ForwardedHeaders.XForwardedProto,
    // IMPORTANT: Only trust known proxies
    KnownProxies = { IPAddress.Parse("10.0.1.10") },
    // Or trust known networks
    KnownNetworks = { new IPNetwork(IPAddress.Parse("172.31.0.0"), 16) },
    ForwardLimit = 1
});
```

**Warning:** Never enable forwarded headers without configuring `KnownProxies` or `KnownNetworks`. Clients could spoof headers.

**Note:** If you enable forwarded headers, `SameHostPolicy` will see the original client IP, not the proxy's IP. For same-host checks between apps on the same machine, prefer direct localhost calls.

### Rate Limiting (Future Enhancement)

For partners that have been compromised or are spamming:

```csharp
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("partner-rate-limit", opt => {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Apply to endpoint group
.RequireRateLimiting("partner-rate-limit")
```

### Security Considerations

1. **Always use HTTPS** in production
2. **Secure configuration files**: 
   - Don't commit tokens to source control
   - Use `appsettings.Production.json` with restricted file permissions
   - Consider Azure Key Vault, AWS Secrets Manager, etc.
3. **Token expiration**: Consider migrating to database with expiration timestamps
4. **Logging**: Monitor authentication failures for security incidents
5. **IP verification**: Ensure you're using the correct source IP (not proxy IP)
6. **Token rotation**: Implement token refresh/rotation for long-lived partnerships
7. **Rate limiting**: Add rate limiting to prevent abuse from compromised partners
8. **Same-host security**: Only use `SameHostPolicy` for APIs and web apps on the same physical/virtual machine

### Migrating to Database

For production scale, consider:

```csharp
public class DatabaseOpaqueTokenValidator : IOpaqueTokenValidator
{
    private readonly IDbConnection _db;
    
    public async Task<bool> ValidateTokenAsync(string token)
    {
        return await _db.QuerySingleOrDefaultAsync<bool>(
            @"SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END 
              FROM Tokens 
              WHERE Token = @Token 
              AND ExpiresAt > GETUTCDATE() 
              AND IsActive = 1",
            new { Token = token });
    }
    
    public async Task<TokenConfiguration?> GetTokenConfigurationAsync(string token)
    {
        return await _db.QuerySingleOrDefaultAsync<TokenConfiguration>(
            @"SELECT UserId, 
                     (SELECT STRING_AGG(RoleName, ',') FROM TokenRoles WHERE TokenId = t.Id) as Roles,
                     (SELECT STRING_AGG(IpAddress, ',') FROM TokenAllowedIPs WHERE TokenId = t.Id) as AllowedIPs
              FROM Tokens t
              WHERE Token = @Token 
              AND ExpiresAt > GETUTCDATE() 
              AND IsActive = 1",
            new { Token = token });
    }
}
