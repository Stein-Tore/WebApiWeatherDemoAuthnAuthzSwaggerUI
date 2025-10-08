# Web API Demo: Authentication & Authorization with Swagger UI

A simple demonstration of different authentication and authorization patterns in ASP.NET Core (.NET 10).

## What This Is

This is a **learning project** showing how to:
- Secure API endpoints with bearer tokens
- Use role-based authorization
- Restrict access by IP address
- Create same-host-only endpoints (for web app + API on same server)
- Get Swagger UI working in IIS

**Not production-ready** - it's a starting point for understanding these concepts. Tokens are stored in `appsettings.json` which is fine for demos but not for real applications.

## The Four Endpoint Types

| Path | Security | Use Case |
|------|----------|----------|
| `/publicweather/*` | None | Public API - anyone can access |
| `/privateweather/*` | Bearer token | Authenticated users only |
| `/privateclient1weather/*` | Bearer token + Role + IP | Partner APIs with IP restrictions |
| `/samehostweather/*` | Same machine only | Web app calling API on same server (no token) |

## Quick Start

```bash
git clone https://github.com/Stein-Tore/WebApiWeatherDemoAuthnAutnzSwaggerUI.git
cd WebApiWeatherDemoAuthnAutnzSwaggerUI
dotnet run
```

Navigate to `https://localhost:7025/swagger`

## Testing

```bash
# Public - no auth needed
curl https://localhost:7025/publicweather/weatherforecast

# Private - requires token
curl -H "Authorization: Bearer demo-token-12345" \
  https://localhost:7025/privateweather/weatherforecast

# Partner - requires token + role + IP
curl -H "Authorization: Bearer partner1-token-xyz" \
  https://localhost:7025/privateclient1weather/weatherforecast

# Same-host - only works from same machine
curl https://localhost:7025/samehostweather/weatherforecast
```

See [TESTING_ENDPOINTS.md](TESTING_ENDPOINTS.md) for detailed examples.

## How It Works

### Bearer Token Authentication

Tokens are configured in `appsettings.json`:

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
        "AllowedIPs": ["127.0.0.1", "::1"]
      }
    }
  }
}
```

- **UserId**: Who this token represents
- **Roles**: What they can do (used with `RequireRole()`)
- **AllowedIPs**: Where they can call from (empty = anywhere)

### Authorization Policies

Defined in `Program.cs`:

- **BearerTokenPolicy** - Just needs a valid token
- **Partner1Policy** - Needs Partner1 role + IP check
- **DataReaderPolicy** - Needs DataReader role (no IP check)
- **DataWriterPolicy** - Needs DataWriter role + IP check
- **SameHostPolicy** - Request must come from same machine (no token)

### Same-Host Policy

For when you have a web app and API on the same server:

- Automatically detects localhost (127.0.0.1, ::1)
- Automatically detects server's own IP addresses
- Can add extra IPs via config (for cloud/NAT scenarios)

**When to use**: Web app calling API on same IIS server without needing tokens.

**Config for cloud** (if needed):
```json
{
  "Authorization": {
    "SameHostPolicy": {
      "AdditionalSameHostIPs": ["52.19.108.174"]
    }
  }
}
```

## Project Structure

```
├── Authentication/
│   ├── IOpaqueTokenValidator.cs              # Token validation interface
│   ├── ConfigurationOpaqueTokenValidator.cs  # Reads tokens from appsettings.json
│   ├── OpaqueTokenAuthenticationHandler.cs   # Validates bearer tokens
│   └── BearerTokenOptions.cs                 # Configuration models
├── Authorization/
│   ├── AllowedIpAuthorizationHandler.cs      # IP whitelist checking
│   ├── SameHostAuthorizationHandler.cs       # Same-machine verification
│   └── SameHostPolicyOptions.cs              # Same-host config
├── Endpoints/
│   ├── WeatherForecast.cs                    # Shared models
│   ├── UnAuthenticatedEndpoints.cs           # Public endpoints
│   ├── AuthenticatedEndpoints.cs             # Private + partner endpoints
│   └── SameHostEndpoints.cs                  # Same-host endpoints
└── Program.cs                                 # App setup
```

## Making It Production-Ready

This demo is a starting point. For real applications:

1. **Move tokens to database** - Don't store in appsettings.json
2. **Add token expiration** - Tokens should expire
3. **Add rate limiting** - Prevent abuse
4. **Use HTTPS everywhere** - Never http in production
5. **Secure configuration** - Use Key Vault, environment variables, etc.
6. **Add logging** - Monitor authentication failures
7. **Token rotation** - Let partners refresh tokens

See [TESTING_ENDPOINTS.md](TESTING_ENDPOINTS.md) for examples of these enhancements.

## Getting Swagger UI to Work in IIS

This was the original challenge. Key findings:

- Set `"launchBrowser": false` in IIS profile
- Add compatibility shim for Swagger's expected path:
  ```csharp
  app.MapGet("/swagger/v1/swagger.json", (HttpContext ctx) => 
      Results.Redirect($"{ctx.Request.PathBase}/openapi/v1.json"));
  ```
- If you get 404/503 errors: run `iisreset`

## License

MIT - Use however you want.



