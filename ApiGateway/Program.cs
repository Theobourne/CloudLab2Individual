using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ApiGateway")
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("Seq__ServerUrl") ?? "http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting ApiGateway web host");

    var builder = WebApplication.CreateBuilder(args);
    
    // Use Serilog for logging
    builder.Host.UseSerilog();
    
    builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
     .AddJsonFile("ocelot.json");
    builder.Services.AddOcelot();
    
    var app = builder.Build();
    
    // Add Serilog request logging
    app.UseSerilogRequestLogging();
    
    Log.Information("ApiGateway web host started successfully");
    app.UseOcelot().Wait();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ApiGateway web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
