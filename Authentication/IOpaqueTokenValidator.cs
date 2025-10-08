namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

/// <summary>
/// Validates opaque bearer tokens and retrieves their configuration.
/// </summary>
public interface IOpaqueTokenValidator
{
   /// <summary>
   /// Checks if a token is valid.
   /// </summary>
   Task<bool> ValidateTokenAsync(string token);

   /// <summary>
   /// Gets the configuration (user, roles, IPs) for a valid token.
   /// </summary>
   Task<TokenConfiguration?> GetTokenConfigurationAsync(string token);
}
