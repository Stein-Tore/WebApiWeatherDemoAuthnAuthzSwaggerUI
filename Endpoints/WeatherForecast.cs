namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

/// <summary>
/// Shared weather forecast model used across all endpoint groups.
/// </summary>
internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
   public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

/// <summary>
/// Shared weather summaries used for generating forecasts.
/// </summary>
internal static class WeatherData
{
   public static readonly string[] Summaries = 
   [
      "Freezing", "Bracing", "Chilly", "Cool", "Mild", 
      "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
   ];

   public static WeatherForecast[] GenerateForecast(int days = 5)
   {
      return Enumerable.Range(1, days).Select(index =>
          new WeatherForecast(
              DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
              Random.Shared.Next(-20, 55),
              Summaries[Random.Shared.Next(Summaries.Length)]
          ))
          .ToArray();
   }
}
