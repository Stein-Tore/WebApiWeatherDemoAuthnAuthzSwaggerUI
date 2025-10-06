using Microsoft.AspNetCore.Authorization;
using Serilog;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
   configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add HttpContextAccessor for authorization handlers
builder.Services.AddHttpContextAccessor();

// Options for SameHost policy (AdditionalSameHostIPs)
builder.Services.Configure<SameHostPolicyOptions>(
    builder.Configuration.GetSection("Authorization:SameHostPolicy"));

// Register token validator (reads from appsettings.json)
builder.Services.AddSingleton<IOpaqueTokenValidator, ConfigurationOpaqueTokenValidator>();

// Add authentication with custom opaque token handler
builder.Services.AddAuthentication("OpaqueToken")
    .AddScheme<OpaqueTokenAuthenticationOptions, OpaqueTokenAuthenticationHandler>(
        "OpaqueToken", 
        options => { });

// Add authorization with custom policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("BearerTokenPolicy", policy =>
    {
        policy.AuthenticationSchemes.Add("OpaqueToken");
        policy.RequireAuthenticatedUser();
    })
    .AddPolicy("SameHostPolicy", policy =>
    {
        policy.Requirements.Add(new SameHostRequirement());
    })
    .AddPolicy("Partner1Policy", policy =>
    {
        policy.AuthenticationSchemes.Add("OpaqueToken");
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Partner1");
        policy.Requirements.Add(new AllowedIpRequirement());
    })
    .AddPolicy("DataReaderPolicy", policy =>
    {
        policy.AuthenticationSchemes.Add("OpaqueToken");
        policy.RequireAuthenticatedUser();
        policy.RequireRole("DataReader");
    })
    .AddPolicy("DataWriterPolicy", policy =>
    {
        policy.AuthenticationSchemes.Add("OpaqueToken");
        policy.RequireAuthenticatedUser();
        policy.RequireRole("DataWriter");
        policy.Requirements.Add(new AllowedIpRequirement());
    });

// Register authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, SameHostAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AllowedIpAuthorizationHandler>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting application in {Environment}", app.Environment.EnvironmentName);

// Optional: log request information using Serilog middleware
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   app.MapOpenApi();
   app.UseSwaggerUI(); // (options => options.SwaggerEndpoint("/openapi/v1.json", "v1")); // No need to set as it is the default.
   app.MapGet("/swagger/v1/swagger.json", (HttpContext ctx) => Results.Redirect($"{ctx.Request.PathBase}/openapi/v1.json")); // Compatibility shim for Swagger UI default discovery path: /swagger/v1/swagger.json -> /openapi/v1.json (preserve PathBase)
}

app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapPublicWeatherEndpoints(); // Should be avaliable to anonymous users, i.e. not authenticated.
app.MapPrivateWeatherEndpoints(); // Should only be available for authenticated users.
app.MapPrivateClient1WeatherEndpoints(); // Should only be available for authenticated users with Partner1 role.
app.MapSameHostWeatherEndpoints(); // Should only be available for web application(s) running on the same host

// Log lifecycle events
app.Lifetime.ApplicationStarted.Register(() =>
    logger.LogInformation("Application started and listening"));
app.Lifetime.ApplicationStopping.Register(() =>
    logger.LogInformation("Application stopping"));
app.Lifetime.ApplicationStopped.Register(() =>
    logger.LogInformation("Application stopped"));

app.Run();