using Microsoft.AspNetCore.Authorization;
using Serilog;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Authentication;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;
using WebApiWeatherDemoAuthnAutnzSwaggerUI.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging (reads from appsettings.json)
builder.Host.UseSerilog((context, configuration) =>
   configuration.ReadFrom.Configuration(context.Configuration));

// Add OpenAPI support for API documentation
builder.Services.AddOpenApi();

// Required for authorization handlers to access HTTP request information
builder.Services.AddHttpContextAccessor();

// Bind SameHostPolicy configuration from appsettings.json
builder.Services.Configure<SameHostPolicyOptions>(
    builder.Configuration.GetSection("Authorization:SameHostPolicy"));

// Register our custom token validator (reads tokens from appsettings.json)
builder.Services.AddSingleton<IOpaqueTokenValidator, ConfigurationOpaqueTokenValidator>();

// Register custom authentication scheme for opaque bearer tokens
builder.Services.AddAuthentication("OpaqueToken")
    .AddScheme<OpaqueTokenAuthenticationOptions, OpaqueTokenAuthenticationHandler>(
        "OpaqueToken",
        options => { });

// Configure authorization policies
builder.Services.AddAuthorizationBuilder()
    // Basic authentication - any valid token
    .AddPolicy("BearerTokenPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
    })
    // Same-host only - request must come from same machine (no token required)
    .AddPolicy("SameHostPolicy", policy =>
    {
       policy.Requirements.Add(new SameHostRequirement());
    })
    // Partner1 - requires specific role + IP whitelist
    .AddPolicy("Partner1Policy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("Partner1");
       policy.Requirements.Add(new AllowedIpRequirement());
    })
    // DataReader - requires role (no IP restriction)
    .AddPolicy("DataReaderPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("DataReader");
    })
    // DataWriter - requires role + IP whitelist
    .AddPolicy("DataWriterPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("DataWriter");
       policy.Requirements.Add(new AllowedIpRequirement());
    });

// Register custom authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, SameHostAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AllowedIpAuthorizationHandler>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting application in {Environment}", app.Environment.EnvironmentName);

// Log HTTP requests with Serilog
app.UseSerilogRequestLogging();

// Enable Swagger UI in development
if (app.Environment.IsDevelopment()) // To get it on prod server change web config env to Development
{
   app.MapOpenApi();
   app.UseSwaggerUI();

   // Compatibility shim: redirect Swagger UI's default discovery path to OpenAPI endpoint
   // Swagger UI expects /swagger/v1/swagger.json but .NET 10 uses /openapi/v1.json
   app.MapGet("/swagger/v1/swagger.json", (HttpContext ctx) =>
      Results.Redirect($"{ctx.Request.PathBase}/openapi/v1.json"))
      .RequireAuthorization("SameHostPolicy") // and add your development pc to allowed IPs, not needed when using only locally
   ;
}

app.UseHttpsRedirection();

// Enable authentication and authorization middleware (order matters!)
app.UseAuthentication();
app.UseAuthorization();

// Map endpoint groups with different security requirements
app.MapPublicWeatherEndpoints();         // No authentication required
app.MapPrivateWeatherEndpoints();        // Requires valid bearer token
app.MapPrivateClient1WeatherEndpoints(); // Requires Partner1 role + IP whitelist
app.MapSameHostWeatherEndpoints();       // Only accessible from same machine

// Log application lifecycle events
app.Lifetime.ApplicationStarted.Register(() =>
    logger.LogInformation("Application started and listening"));
app.Lifetime.ApplicationStopping.Register(() =>
    logger.LogInformation("Application stopping"));
app.Lifetime.ApplicationStopped.Register(() =>
    logger.LogInformation("Application stopped"));

app.Run();