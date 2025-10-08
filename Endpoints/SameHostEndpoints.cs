namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class SameHostEndpoints
{
   /// <summary>
   /// Maps endpoints that only accept requests from the same machine.
   /// No bearer token required - uses network-level verification.
   /// </summary>
   public static void MapSameHostWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/samehostweather")
         .WithTags("Same Host Weather")
         .RequireAuthorization("SameHostPolicy"); // Only accessible from same machine

      group.MapGet("/weatherforecast", () => WeatherData.GenerateForecast())
         .WithName("GetSameHostWeatherForecast")
         .WithDescription("Same-host weather forecast - only accessible from same machine (no token required)");
   }
}

