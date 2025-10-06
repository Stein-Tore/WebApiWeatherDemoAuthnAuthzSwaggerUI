namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class UnAuthenticatedEndpoints
{
   public static void MapPublicWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/publicweather")
         .WithTags("Public Weather")
         .AllowAnonymous(); // Public endpoints - no authentication required

      group.MapGet("/weatherforecast", () =>
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
      .WithName("GetPublicWeatherForecast");
   }

   internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
   {
      public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
   }

   private static readonly string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy!!!!", "Hot", "Sweltering", "Scorching"];
}

