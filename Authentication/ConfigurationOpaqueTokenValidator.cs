namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

/// <summary>
/// Validates opaque bearer tokens from appsettings.json configuration.
/// </summary>
public class ConfigurationOpaqueTokenValidator : IOpaqueTokenValidator
{
   private readonly Dictionary<string, TokenConfiguration> _validTokens;

   public ConfigurationOpaqueTokenValidator(IConfiguration configuration)
   {
      // Load token configurations from appsettings.json
      _validTokens = configuration.GetSection(BearerTokenOptions.SectionName)
          .Get<Dictionary<string, TokenConfiguration>>() ?? [];
   }

   public Task<bool> ValidateTokenAsync(string token)
   {
      return Task.FromResult(_validTokens.ContainsKey(token));
   }

   public Task<TokenConfiguration?> GetTokenConfigurationAsync(string token)
   {
      _validTokens.TryGetValue(token, out var config);
      return Task.FromResult(config);
   }
}
