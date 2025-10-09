# Testing the Endpoints

## Configuration Structure

### Two Building Blocks

#### 1. Token Configuration (Identity Authorization)

Tokens are in `appsettings.json` under `Authentication:BearerTokens`:

```json
{
  "Authentication": {
    "BearerTokens": {
      "demo-token-12345": {
        "UserId": "user1",
        "Roles": ["User"]
      },
      "demo-token-67890": {
        "UserId": "user2",
        "Roles": ["User", "DataReader"]
      },
      "partner1-token-xyz": {
        "UserId": "partner1",
        "Roles": ["Partner1", "DataReader"]
      },
      "admin-token-123": {
        "UserId": "admin1",
        "Roles": ["Admin", "DataWriter", "DataReader"]
      }
    }
  }
}
```

**Properties**:
- **UserId**: User identifier
- **Roles**: Array of roles for authorization

#### 2. IP Policy Configuration (IP Authorization)

IP policies are in `appsettings.json` under `Authorization:IpPolicies`:

```json
{
  "Authorization": {
    "IpPolicies": {
      "AdditionalSameHostIPs": [],
      "Policies": {
        "SameHost": {
          "IncludeSameHost": true,
          "AllowedIPs": []
        },
        "OpenApi": {
          "IncludeSameHost": true,
          "AllowedIPs": []
        },
        "Partner1": {
          "IncludeSameHost": false,
          "AllowedIPs": ["127.0.0.1", "::1"]
        },
        "DataWriter": {
          "IncludeSameHost": false,
          "AllowedIPs": ["127.0.0.1", "::1"]
        }
      }
    }
  }
}
```

**Key points**:
- `IncludeSameHost: true` = Auto-populate with localhost + local NIC IPs
- `AllowedIPs: []` = Additional specific IPs
- `AdditionalSameHostIPs` = Global IPs to treat as same-host (for hairpin NAT)

## Public Endpoints (No Authorization)

No authentication or IP restrictions:

```bash
curl https://localhost:7025/publicweather/weatherforecast
```

**Expected**: 200 OK with weather data

## Private Endpoints (Identity Only - Bearer Token)

Requires valid token, no IP restrictions.

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

## Partner Endpoints (Identity AND IP)

Requires valid token + specific role + IP whitelist match.

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
**Expected**: 200 OK (if calling from 127.0.0.1 or ::1)

### Correct Role + Disallowed IP

Change `Partner1` policy to exclude your IP:

```json
"Partner1": {
  "IncludeSameHost": false,
  "AllowedIPs": ["203.0.113.99"]
}
```

Then:
```bash
curl -H "Authorization: Bearer partner1-token-xyz" \
  https://localhost:7025/privateclient1weather/weatherforecast
```
**Expected**: 403 Forbidden

### Different Requirements for GET vs POST

**GET /data** - Identity only (DataReader role, no IP check):
```bash
curl -H "Authorization: Bearer demo-token-67890" \
  https://localhost:7025/privateclient1weather/data
```
**Expected**: 200 OK

**POST /data** - Identity AND IP (DataWriter role + IP whitelist):
```bash
curl -X POST -H "Authorization: Bearer admin-token-123" \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}' \
  https://localhost:7025/privateclient1weather/data
```
**Expected**: 200 OK (if calling from allowed IP in `DataWriter` policy)

## Same-Host Endpoints (IP Only - Auto-Populated)

Uses IP authorization with `IncludeSameHost: true` (no token required).

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

## OpenAPI Endpoint (IP Only - Same-Host OR Specific IPs)

Default configuration allows same-host OR specific IPs:

```json
{
  "OpenApi": {
    "IncludeSameHost": true,
    "AllowedIPs": []
  }
}
```

### From Localhost
```bash
curl https://localhost:7025/openapi/v1.json
```
**Expected**: 200 OK

### Add Specific IP
```json
{
  "OpenApi": {
    "IncludeSameHost": true,
    "AllowedIPs": ["203.0.113.10"]
  }
}
```
Now accessible from localhost **OR** 203.0.113.10 (OR logic built-in!).

### IP Only (Disable Same-Host)
```json
{
  "OpenApi": {
    "IncludeSameHost": false,
    "AllowedIPs": ["203.0.113.10", "192.168.1.100"]
  }
}
```
Only accessible from those two IPs.

## Authorization Pattern Examples

### Pattern 1: IP Only (No Token)

```json
{
  "AdminPanel": {
    "IncludeSameHost": false,
    "AllowedIPs": ["10.0.0.5"]
  }
}
```

```csharp
app.MapGet("/admin-panel", () => Results.Ok("Admin panel"))
   .RequireAuthorization(policy => 
      policy.Requirements.Add(new IpAuthorizationRequirement("AdminPanel")));
```

Test:
```bash
curl https://localhost:7025/admin-panel
```

### Pattern 2: Identity Only (No IP)

Already implemented as `DataReaderPolicy`:

```bash
curl -H "Authorization: Bearer demo-token-67890" \
  https://localhost:7025/privateclient1weather/data
```

### Pattern 3: Identity AND IP

Already implemented as `Partner1Policy` and `DataWriterPolicy`.

## Cloud Scenarios

### AWS EC2 with Elastic IP (Hairpin NAT)

If calling the API from the same EC2 instance via its public IP:

```json
{
  "IpPolicies": {
    "AdditionalSameHostIPs": ["52.19.108.174"],
    "Policies": {
      "OpenApi": {
        "IncludeSameHost": true,
        "AllowedIPs": []
      }
    }
  }
}
```

Now `52.19.108.174` is treated as same-host globally.

### Azure VM with Public IP

Similar configuration if hairpin NAT occurs.

## Adding Production Tokens

For your remote server, add to `appsettings.Production.json`:

```json
{
  "Authentication": {
    "BearerTokens": {
      "prod-partner1-a1b2c3": {
        "UserId": "partner1-prod",
        "Roles": ["Partner1", "DataReader"]
      }
    }
  },
  "Authorization": {
    "IpPolicies": {
      "Policies": {
        "Partner1": {
          "IncludeSameHost": false,
          "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
        }
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

### Simplified Architecture

**Two building blocks:**
- ? **IP Authorization** (includes same-host as special case via `IncludeSameHost`)
- ? **Identity Authorization** (authentication + roles)

**Benefits:**
- Same-host is just IP whitelist with auto-population
- Cleaner mental model (2 concerns instead of 3)
- Easy to express AND/OR combinations
- Less code duplication

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
- Consider CIDR ranges for IP policies (e.g., `192.168.1.0/24`)

## Extending This Demo

### Add CIDR Support

For IP ranges instead of individual IPs:

```csharp
// Add NuGet: System.Net.IPNetwork
if (IPNetwork.TryParse(allowedIp, out var network))
{
   if (network.Contains(remoteIpAddress))
   {
      context.Succeed(requirement);
   }
}
```

### Add Token Expiration

Extend `TokenConfiguration`:

```csharp
public class TokenConfiguration
{
    public string UserId { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
}
```

Validate in authentication handler.

### Migrate to Database

Replace `ConfigurationOpaqueTokenValidator` with database implementation for production scale.

### Add OR Logic for IP and Identity

Use `IpOrIdentityRequirement` for flexible access:

```csharp
.AddPolicy("FlexibleAdmin", policy =>
{
    policy.Requirements.Add(new IpOrIdentityRequirement("AdminOffice", "Admin"));
});
```

Allows access from office IP **OR** with Admin token.
