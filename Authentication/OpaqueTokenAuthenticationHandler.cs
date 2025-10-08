using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

public class OpaqueTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Custom authentication handler for opaque bearer tokens.
/// Validates tokens from Authorization header and creates claims principal with roles.
/// </summary>
public class OpaqueTokenAuthenticationHandler : AuthenticationHandler<OpaqueTokenAuthenticationOptions>
{
   private const string AuthorizationHeaderName = "Authorization";
   private const string BearerPrefix = "Bearer ";

   private readonly IOpaqueTokenValidator _tokenValidator;

   public OpaqueTokenAuthenticationHandler(
       IOptionsMonitor<OpaqueTokenAuthenticationOptions> options,
       ILoggerFactory logger,
       UrlEncoder encoder,
       IOpaqueTokenValidator tokenValidator)
       : base(options, logger, encoder) => _tokenValidator = tokenValidator;

   protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
   {
      // Extract Bearer token from Authorization header
      if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeaderValues))
      {
         return AuthenticateResult.NoResult();
      }

      var authorizationHeader = authorizationHeaderValues.ToString();

      if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
      {
         return AuthenticateResult.NoResult();
      }

      var token = authorizationHeader[BearerPrefix.Length..].Trim();

      if (string.IsNullOrEmpty(token))
      {
         return AuthenticateResult.Fail("Invalid token format");
      }

      // Validate token against configured tokens in appsettings.json
      var isValid = await _tokenValidator.ValidateTokenAsync(token);
      if (!isValid)
      {
         return AuthenticateResult.Fail("Invalid token");
      }

      // Get token configuration (user, roles, allowed IPs)
      var tokenConfig = await _tokenValidator.GetTokenConfigurationAsync(token);
      if (tokenConfig == null || string.IsNullOrEmpty(tokenConfig.UserId))
      {
         return AuthenticateResult.Fail("Unable to retrieve token configuration");
      }

      // Build claims for the authenticated user
      var claims = new List<Claim>
      {
          new(ClaimTypes.NameIdentifier, tokenConfig.UserId),
          new(ClaimTypes.Name, tokenConfig.UserId),
          new("token", token)
      };

      // Add role claims (used by .RequireRole() in authorization policies)
      foreach (var role in tokenConfig.Roles)
      {
         claims.Add(new Claim(ClaimTypes.Role, role));
      }

      // Store allowed IPs as claim for IP whitelist authorization handler
      if (tokenConfig.AllowedIPs.Length > 0)
      {
         claims.Add(new Claim("allowed_ips", string.Join(",", tokenConfig.AllowedIPs)));
      }

      // Create authenticated principal and ticket
      var identity = new ClaimsIdentity(claims, Scheme.Name);
      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, Scheme.Name);

      return AuthenticateResult.Success(ticket);
   }

   /// <summary>
   /// Handles 401 Unauthorized responses with RFC 6750 compliant WWW-Authenticate header.
   /// </summary>
   protected override Task HandleChallengeAsync(AuthenticationProperties properties)
   {
      Response.Headers.Append("WWW-Authenticate", $"Bearer realm=\"{Request.Host}\", charset=\"UTF-8\"");
      Response.StatusCode = StatusCodes.Status401Unauthorized;
      return Task.CompletedTask;
   }

   /// <summary>
   /// Handles 403 Forbidden responses (authenticated but not authorized).
   /// No WWW-Authenticate header as per RFC 6750.
   /// </summary>
   protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
   {
      Response.StatusCode = StatusCodes.Status403Forbidden;
      return Task.CompletedTask;
   }
}