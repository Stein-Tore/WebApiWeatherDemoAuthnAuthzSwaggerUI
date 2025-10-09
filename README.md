# Web API Demo: Authentication & Authorization with Swagger UI

A demonstration of clean, separated authentication and authorization patterns in ASP.NET Core (.NET 10).

## What This Is

This is a **learning project** showing how to:
- Separate authentication (who you are) from authorization (what you can access)
- Use two simple building blocks: **IP-based** and **Identity-based** authorization
- Combine authorization requirements flexibly (AND/OR logic)
- Secure API endpoints with bearer tokens
- Use role-based authorization
- Restrict access by IP address (including same-host detection)
- Get Swagger UI working with OpenApi for .NET 9

**Not production-ready** - it's a starting point for understanding these concepts.

## Core Concept: Two Authorization Building Blocks

### Block 1: IP-Based Authorization
Controls access based on **where the request comes from**:
- Specific IP addresses (e.g., `203.0.113.10`)
- Same-host detection (auto-populated: loopback + local NICs + configured IPs)
- **No authentication required** - purely network-level

### Block 2: Identity-Based Authorization
Controls access based on **who the user is**:
- Bearer token authentication
- User roles (e.g., Admin, Partner1, DataReader)
- **Requires authentication** - must have valid token

### Flexible Combinations

These two blocks can be combined in four ways:

| Pattern | IP Auth | Identity Auth | Example Use Case |
|---------|---------|---------------|------------------|
| **IP only** | ✅ Required | ❌ Not required | OpenAPI endpoint (same-host OR specific IPs) |
| **Identity only** | ❌ Not required | ✅ Required | Private weather endpoint (any authenticated user) |
| **IP AND Identity** | ✅ Required | ✅ Required | Partner endpoint (token + role + IP) |
| **IP OR Identity** | ✅ One of | ✅ One of | Admin panel (either from office IP OR admin token) |

## The Four Endpoint Types

| Path | Security | Pattern | Use Case |
|------|----------|---------|----------|
| `/publicweather/*` | None | - | Public API - anyone |
| `/privateweather/*` | Bearer token | Identity only | Authenticated users |
| `/privateclient1weather/*` | Token + Role + IP | Identity AND IP | Partner APIs with IP restrictions |
| `/samehostweather/*` | Same machine | IP only (auto) | Web app on same server |

## Quick Start

```bash
git clone https://github.com/Stein-Tore/WebApiWeatherDemoAuthnAutnzSwaggerUI.git
cd WebApiWeatherDemoAuthnAutnzSwaggerUI
dotnet run
```

Navigate to `https://localhost:7025/swagger`

## Configuration

### IP Policies (Authorization)

All IP-based authorization in one place:

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
          "AllowedIPs": ["203.0.113.10"]
        },
        "Partner1": {
          "IncludeSameHost": false,
          "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
        }
      }
    }
  }
}
```

**Key points:**
- `IncludeSameHost: true` - Auto-populate with localhost + local NICs
- `AllowedIPs: []` - Additional specific IPs
- `AdditionalSameHostIPs` - Global addition for hairpin NAT scenarios (AWS EIP, Azure public IP)

### Token Configuration (Authentication)

Tokens identify users and assign roles:

```json
{
  "Authentication": {
    "BearerTokens": {
      "demo-token-12345": {
        "UserId": "user1",
        "Roles": ["User"]
      },
      "partner1-token-xyz": {
        "UserId": "partner1",
        "Roles": ["Partner1", "DataReader"]
      }
    }
  }
}
```

- **UserId**: Who this token represents
- **Roles**: What groups they belong to (used for role-based authorization)

## Example Configurations

### Same-Host Only
```json
{
  "SameHost": {
    "IncludeSameHost": true,
    "AllowedIPs": []
  }
}
```
Allows: localhost, 127.0.0.1, ::1, local NIC IPs

### Specific IPs Only
```json
{
  "AdminPanel": {
    "IncludeSameHost": false,
    "AllowedIPs": ["203.0.113.10", "203.0.113.11"]
  }
}
```
Allows: Only those two IPs

### Same-Host OR Specific IPs
```json
{
  "OpenApi": {
    "IncludeSameHost": true,
    "AllowedIPs": ["203.0.113.10"]
  }
}
```
Allows: localhost OR 203.0.113.10 (OR logic built-in!)

### Cloud Scenarios (Hairpin NAT)
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
Treats the EIP as same-host globally.

## Authorization Policies

### Predefined Policies

```csharp
// 1. BearerTokenPolicy - Identity only (any authenticated user)
policy.RequireAuthenticatedUser();

// 2. SameHostPolicy - IP only (same machine)
policy.Requirements.Add(new IpAuthorizationRequirement("SameHost"));

// 3. Partner1Policy - Identity AND IP (token + role + IP)
policy.RequireAuthenticatedUser();
policy.RequireRole("Partner1");
policy.Requirements.Add(new IpAuthorizationRequirement("Partner1"));

// 4. DataReaderPolicy - Identity only (token + role, no IP)
policy.RequireAuthenticatedUser();
policy.RequireRole("DataReader");

// 5. DataWriterPolicy - Identity AND IP (token + role + IP)
policy.RequireAuthenticatedUser();
policy.RequireRole("DataWriter");
policy.Requirements.Add(new IpAuthorizationRequirement("DataWriter"));

// 6. OpenApiPolicy - IP only (same-host OR specific IPs)
policy.Requirements.Add(new IpAuthorizationRequirement("OpenApi"));
```

### Custom OR Logic (Future)

For endpoints that accept **either** IP **OR** identity:

```csharp
.AddPolicy("FlexibleAdmin", policy =>
{
    policy.Requirements.Add(new IpOrIdentityRequirement("AdminOffice", "Admin"));
});
```
Allows access from office IP **OR** with Admin role.

## Project Structure

```
WebApiWeatherDemoAuthnAutnzSwaggerUI/
├── Authentication/
│   ├── IOpaqueTokenValidator.cs              # Token validation interface
│   ├── ConfigurationOpaqueTokenValidator.cs  # Config-based implementation
│   ├── OpaqueTokenAuthenticationHandler.cs   # Bearer token auth handler
│   └── BearerTokenOptions.cs                 # Token configuration models
├── Authorization/
│   ├── IpAuthorizationRequirement.cs         # Unified IP authorization (includes same-host)
│   └── IpOrIdentityRequirement.cs            # OR logic between IP and Identity
├── Endpoints/
│   ├── WeatherForecast.cs                    # Shared models
│   ├── UnAuthenticatedEndpoints.cs           # Public endpoints
│   ├── AuthenticatedEndpoints.cs             # Private + partner endpoints
│   └── SameHostEndpoints.cs                  # Same-host endpoints
└── Program.cs                                 # App setup
```

## Why This Design?

### Two Building Blocks
- ✅ **IP Authorization** (includes same-host as special case)
- ✅ **Identity Authorization** (authentication + roles)

**Benefits:**
- Simple mental model
- Same-host is just IP whitelist with `IncludeSameHost: true`
- Easy to express AND/OR combinations
- clear intent

## Testing

See [TESTING_ENDPOINTS.md](TESTING_ENDPOINTS.md) for detailed examples.

**Quick tests:**

```bash
# Public - no auth
curl https://localhost:7025/publicweather/weatherforecast

# Private - requires token
curl -H "Authorization: Bearer demo-token-12345" \
  https://localhost:7025/privateweather/weatherforecast

# Partner - requires token + role + IP
curl -H "Authorization: Bearer partner1-token-xyz" \
  https://localhost:7025/privateclient1weather/weatherforecast

# Same-host - only from localhost
curl https://localhost:7025/samehostweather/weatherforecast
```

## Making It Production-Ready

This is a learning starting point. For production:

1. **Move tokens to database** - Don't store in appsettings.json
2. **Add token expiration** - Tokens should expire
3. **Add rate limiting** - Prevent abuse
4. **Use HTTPS everywhere** - Never http
5. **Secure configuration** - Use Key Vault, environment variables
6. **Add logging** - Monitor authentication failures
7. **Consider CIDR ranges** - For IP whitelisting (e.g., `192.168.1.0/24`)
