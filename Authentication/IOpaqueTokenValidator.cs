namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;

public interface IOpaqueTokenValidator
{
   Task<bool> ValidateTokenAsync(string token);
   Task<TokenConfiguration?> GetTokenConfigurationAsync(string token);
}
