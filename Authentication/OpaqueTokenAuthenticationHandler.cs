using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

public class OpaqueTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}

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
      // Check if Authorization header exists
      if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeaderValues))
      {
         return AuthenticateResult.NoResult();
      }

      var authorizationHeader = authorizationHeaderValues.ToString();

      // Check if it's a Bearer token
      if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
      {
         return AuthenticateResult.NoResult();
      }

      var token = authorizationHeader[BearerPrefix.Length..].Trim();

      if (string.IsNullOrEmpty(token))
      {
         return AuthenticateResult.Fail("Invalid token format");
      }

      // Validate the token
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

      // Create claims including roles
      var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tokenConfig.UserId),
            new(ClaimTypes.Name, tokenConfig.UserId),
            new("token", token)
        };

      // Add role claims
      foreach (var role in tokenConfig.Roles)
      {
         claims.Add(new Claim(ClaimTypes.Role, role));
      }

      // Store allowed IPs as a claim for use in authorization handlers
      if (tokenConfig.AllowedIPs.Length > 0)
      {
         claims.Add(new Claim("allowed_ips", string.Join(",", tokenConfig.AllowedIPs)));
      }

      var identity = new ClaimsIdentity(claims, Scheme.Name);
      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, Scheme.Name);

      return AuthenticateResult.Success(ticket);
   }

   protected override Task HandleChallengeAsync(AuthenticationProperties properties)
   {
      // RFC 6750 compliant WWW-Authenticate header for Bearer token authentication
      Response.Headers.Append("WWW-Authenticate", $"Bearer realm=\"{Request.Host}\", charset=\"UTF-8\"");
      Response.StatusCode = StatusCodes.Status401Unauthorized;
      return Task.CompletedTask;
   }

   protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
   {
      {
         // Return 403 when authenticated but not authorized
         Response.StatusCode = StatusCodes.Status403Forbidden;
         return Task.CompletedTask;
      }
   }
}