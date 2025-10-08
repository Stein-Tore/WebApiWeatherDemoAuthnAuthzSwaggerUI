namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class UnAuthenticatedEndpoints
{
   /// <summary>
   /// Maps public endpoints that don't require authentication.
   /// </summary>
   public static void MapPublicWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/publicweather")
         .WithTags("Public Weather")
         .AllowAnonymous(); // Explicitly allow anonymous access

      group.MapGet("/weatherforecast", () => WeatherData.GenerateForecast())
         .WithName("GetPublicWeatherForecast")
         .WithDescription("Public weather forecast - no authentication required");
   }
}

