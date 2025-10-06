namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

/// <summary>
/// Configuration options for bearer tokens.
/// </summary>
public class BearerTokenOptions
{
   public const string SectionName = "Authentication:BearerTokens";

   /// <summary>
   /// Dictionary of token -> token configuration mappings.
   /// </summary>
   public Dictionary<string, TokenConfiguration> Tokens { get; set; } = [];
}

/// <summary>
/// Configuration for an individual token.
/// </summary>
public class TokenConfiguration
{
   /// <summary>
   /// User identifier associated with this token.
   /// </summary>
   public string UserId { get; set; } = string.Empty;

   /// <summary>
   /// Roles assigned to this token/user.
   /// </summary>
   public string[] Roles { get; set; } = [];

   /// <summary>
   /// IP addresses allowed to use this token.
   /// Empty array = no IP restrictions.
   /// </summary>
   public string[] AllowedIPs { get; set; } = [];
}
