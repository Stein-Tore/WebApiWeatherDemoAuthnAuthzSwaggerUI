namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class AuthenticatedEndpoints
{
   /// <summary>
   /// Maps private endpoints requiring bearer token authentication.
   /// </summary>
   public static void MapPrivateWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/privateweather")
         .WithTags("Private Weather")
         .RequireAuthorization("BearerTokenPolicy"); // Requires valid bearer token

      group.MapGet("/weatherforecast", () => WeatherData.GenerateForecast())
         .WithName("GetPrivateWeatherForecast")
         .WithDescription("Private weather forecast - requires bearer token");
   }

   /// <summary>
   /// Maps partner-specific endpoints requiring Partner1 role and IP whitelist validation.
   /// </summary>
   public static void MapPrivateClient1WeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/privateclient1weather")
         .WithTags("Private Client1 Weather")
         .RequireAuthorization("Partner1Policy"); // Requires Partner1 role + IP whitelist

      group.MapGet("/weatherforecast", () => WeatherData.GenerateForecast())
         .WithName("GetPrivateClient1WeatherForecast")
         .WithDescription("Partner1 weather forecast - requires Partner1 role and IP whitelist");

      // Example: GET requires DataReader role only (no IP restriction)
      group.MapGet("/data", () =>
         Results.Ok(new { Message = "Data for Partner1", Data = new[] { 1, 2, 3 } }))
         .WithName("GetPartner1Data")
         .WithDescription("Get data - requires DataReader role")
         .RequireAuthorization("DataReaderPolicy");

      // Example: POST requires DataWriter role + IP whitelist
      group.MapPost("/data", (object data) =>
         Results.Ok(new { Message = "Data created for Partner1", ReceivedData = data }))
         .WithName("PostPartner1Data")
         .WithDescription("Post data - requires DataWriter role and IP whitelist")
         .RequireAuthorization("DataWriterPolicy");
   }
}
