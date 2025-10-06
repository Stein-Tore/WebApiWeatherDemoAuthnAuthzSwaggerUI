namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class AuthenticatedEndpoints
{
   public static void MapPrivateWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/privateweather")
         .WithTags("Private Weather")
         .RequireAuthorization("BearerTokenPolicy"); // Require opaque bearer token authentication

      _ = group.MapGet("/weatherforecast", () =>
      {
         var forecast = Enumerable.Range(1, 5).Select(index =>
             new WeatherForecast
             (
                 DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                 Random.Shared.Next(-20, 55),
                 summaries[Random.Shared.Next(summaries.Length)]
             ))
             .ToArray();
         return forecast;
      })
      .WithName("GetPrivateWeatherForecast");
   }

   public static void MapPrivateClient1WeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/privateclient1weather")
         .WithTags("Private Client1 Weather")
         .RequireAuthorization("Partner1Policy"); // Require Partner1 role + IP whitelist

      _ = group.MapGet("/weatherforecast", () =>
      {
         var forecast = Enumerable.Range(1, 5).Select(index =>
             new WeatherForecast
             (
                 DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                 Random.Shared.Next(-20, 55),
                 summaries[Random.Shared.Next(summaries.Length)]
             ))
             .ToArray();
         return forecast;
      })
      .WithName("GetPrivateClient1WeatherForecast");

      // Example: GET endpoint requires DataReader role (any authenticated user with role)
      _ = group.MapGet("/data", () =>
         Results.Ok(new { Message = "Data for Partner1", Data = new[] { 1, 2, 3 } }))
         .WithName("GetPartner1Data")
         .RequireAuthorization("DataReaderPolicy");

      // Example: POST endpoint requires DataWriter role + IP whitelist
      _ = group.MapPost("/data", (object data) =>
         Results.Ok(new { Message = "Data created for Partner1", ReceivedData = data }))
         .WithName("PostPartner1Data")
         .RequireAuthorization("DataWriterPolicy");
   }

   internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
   {
      public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
   }

   private static readonly string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy!!!!", "Hot", "Sweltering", "Scorching"];
}
