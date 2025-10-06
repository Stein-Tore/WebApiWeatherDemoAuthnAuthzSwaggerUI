namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

public static class SameHostEndpoints
{
   public static void MapSameHostWeatherEndpoints(this IEndpointRouteBuilder routes)
   {
      var group = routes.MapGroup("/samehostweather")
         .WithTags("Same Host Weather")
         .RequireAuthorization("SameHostPolicy")
         ; // Require request from same host

      _ = group.MapGet("/weatherforecast", (ILogger<Category> logger) =>
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
      .WithName("GetSameHostWeatherForecast");
   }

   // Non-static category type for ILogger<T> (static types can't be used as T)
   private sealed class Category { }

   internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
   {
      public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
   }

   private static readonly string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy!!!!", "Hot", "Sweltering", "Scorching"];
}

