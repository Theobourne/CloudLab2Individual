using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Polly;
using Polly.Extensions.Http;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "UniversityWeb")
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("Seq__ServerUrl") ?? "http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting UniversityWeb web host");

    var builder = WebApplication.CreateBuilder(args);
    
    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add MVC services with runtime compilation
    builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

    // Configure HTTP clients for APIs with Polly resilience policies
    builder.Services.AddHttpClient("StudentsApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["StudentsApi:BaseUrl"] ?? builder.Configuration["StudentsApi__BaseUrl"] ?? "http://localhost:5001/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());

    builder.Services.AddHttpClient("CoursesApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["CoursesApi:BaseUrl"] ?? builder.Configuration["CoursesApi__BaseUrl"] ?? "http://localhost:5002/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("UniversityWeb web host started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UniversityWeb web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Polly Retry Policy: Retry 3 times with exponential backoff
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // Handles 5xx and 408
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Log.Warning("Retry {RetryCount} after {Delay}s due to: {Result}", 
                    retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
            });
}

// Polly Circuit Breaker Policy: Break circuit after 5 consecutive failures for 30 seconds
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Log.Error("Circuit breaker opened for {Duration}s due to: {Result}", 
                    duration.TotalSeconds, outcome.Result?.StatusCode);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reset");
            });
}

// Polly Timeout Policy: 10 seconds per request
static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
}