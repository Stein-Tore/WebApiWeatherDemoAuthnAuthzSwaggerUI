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
/// Authentication only - authorization (IP restrictions, etc.) handled separately.
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
      Logger.LogDebug("Starting authentication for request to {Path}", Request.Path);

      // Extract Bearer token from Authorization header
      if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeaderValues))
      {
         Logger.LogDebug("No Authorization header found for {Path}", Request.Path);
         return AuthenticateResult.NoResult();
      }

      var authorizationHeader = authorizationHeaderValues.ToString();

      if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
      {
         Logger.LogDebug("Authorization header does not start with 'Bearer' for {Path}", Request.Path);
         return AuthenticateResult.NoResult();
      }

      var token = authorizationHeader[BearerPrefix.Length..].Trim();

      if (string.IsNullOrEmpty(token))
      {
         Logger.LogWarning("Empty token provided for {Path}", Request.Path);
         return AuthenticateResult.Fail("Invalid token format");
      }

      Logger.LogDebug("Validating token for {Path}", Request.Path);

      // Validate token against configured tokens in appsettings.json
      var isValid = await _tokenValidator.ValidateTokenAsync(token);
      if (!isValid)
      {
         Logger.LogWarning("Invalid token provided for {Path}", Request.Path);
         return AuthenticateResult.Fail("Invalid token");
      }

      // Get token configuration (user, roles)
      var tokenConfig = await _tokenValidator.GetTokenConfigurationAsync(token);
      if (tokenConfig == null || string.IsNullOrEmpty(tokenConfig.UserId))
      {
         Logger.LogError("Unable to retrieve token configuration for valid token on {Path}", Request.Path);
         return AuthenticateResult.Fail("Unable to retrieve token configuration");
      }

      Logger.LogInformation("Token validated successfully for user {UserId} with roles [{Roles}] on {Path}", 
         tokenConfig.UserId, 
         string.Join(", ", tokenConfig.Roles),
         Request.Path);

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

      // Create authenticated principal and ticket
      var identity = new ClaimsIdentity(claims, Scheme.Name);
      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, Scheme.Name);

      Logger.LogDebug("Authentication successful for {UserId} on {Path}", tokenConfig.UserId, Request.Path);
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