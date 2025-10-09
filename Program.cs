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

// Bind configuration options
builder.Services.Configure<IpAuthorizationOptions>(
    builder.Configuration.GetSection("Authorization:IpPolicies"));

// Register our custom token validator (reads tokens from appsettings.json)
builder.Services.AddSingleton<IOpaqueTokenValidator, ConfigurationOpaqueTokenValidator>();

// Register custom authentication scheme for opaque bearer tokens
builder.Services.AddAuthentication("OpaqueToken")
    .AddScheme<OpaqueTokenAuthenticationOptions, OpaqueTokenAuthenticationHandler>(
        "OpaqueToken",
        options => { });

// Configure authorization policies
builder.Services.AddAuthorizationBuilder()
    // Basic authentication - any valid token (Identity only)
    .AddPolicy("BearerTokenPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
    })
    // Same-host only - request must come from same machine (IP only, auto-populated)
    .AddPolicy("SameHostPolicy", policy =>
    {
       policy.Requirements.Add(new IpAuthorizationRequirement("SameHost"));
    })
    // Partner1 - requires specific role + IP whitelist (Identity AND IP)
    .AddPolicy("Partner1Policy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("Partner1");
       policy.Requirements.Add(new IpAuthorizationRequirement("Partner1"));
    })
    // DataReader - requires role only (Identity only, no IP restriction)
    .AddPolicy("DataReaderPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("DataReader");
    })
    // DataWriter - requires role + IP whitelist (Identity AND IP)
    .AddPolicy("DataWriterPolicy", policy =>
    {
       policy.AuthenticationSchemes.Add("OpaqueToken");
       policy.RequireAuthenticatedUser();
       policy.RequireRole("DataWriter");
       policy.Requirements.Add(new IpAuthorizationRequirement("DataWriter"));
    })
    // OpenAPI access - flexible based on configuration
    .AddPolicy("OpenApiPolicy", policy =>
    {
       policy.Requirements.Add(new IpAuthorizationRequirement("OpenApi"));
    });

// Register custom authorization handlers
builder.Services.AddSingleton<IpAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler>(sp => sp.GetRequiredService<IpAuthorizationHandler>());
builder.Services.AddSingleton<IAuthorizationHandler, IpOrIdentityAuthorizationHandler>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("-----------------------------");
logger.LogInformation("Starting application in {Environment}", app.Environment.EnvironmentName);

// Log HTTP requests with Serilog
app.UseSerilogRequestLogging();

// Enable Swagger UI in development or if explicitly enabled in config
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
   var openApiBuilder = app.MapOpenApi();
   app.UseSwaggerUI();

   // Apply OpenAPI protection
   openApiBuilder.RequireAuthorization("OpenApiPolicy");
   logger.LogInformation("OpenAPI endpoint protected with OpenApiPolicy");

   // Compatibility shim: redirect Swagger UI's default discovery path to OpenAPI endpoint
   // Swagger UI expects /swagger/v1/swagger.json but .NET 9 uses /openapi/v1.json
   app.MapGet("/swagger/v1/swagger.json", (HttpContext ctx) =>
      Results.Redirect($"{ctx.Request.PathBase}/openapi/v1.json"));
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