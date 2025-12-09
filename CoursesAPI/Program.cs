using Microsoft.EntityFrameworkCore;
using CoursesAPI.Data; // <--- Change to your CoursesAPI project namespace
using System.Text.Json.Serialization;
using RabbitMQ.Client; // This is the standard RabbitMQ client library for ExchangeType
// For IConnectionProvider, ConnectionProvider, IPublisher, Publisher:
using EventBus.RabbitMQ;
using EventBus.RabbitMQ.Standard;
using Newtonsoft.Json; // Required for JsonConvert.SerializeObject in the controller
using MassTransit;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CoursesAPI")
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("Seq__ServerUrl") ?? "http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting CoursesAPI web host");

    var builder = WebApplication.CreateBuilder(args);
    
    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add the database context registration
    builder.Services.AddDbContext<CoursesAPIContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("CoursesAPIContext")
        ?? throw new InvalidOperationException("Connection string 'CoursesAPIContext' not found.")));

    // Add services to the container.
    builder.Services.AddControllers().AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("CoursesAPIContext") ?? "",
            name: "CoursesDB",
            tags: new[] { "db", "sql", "sqlserver" })
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    builder.Services.AddMassTransit(x =>
    {
        // Configure RabbitMQ as the transport
        x.UsingRabbitMq((context, cfg) =>
        {
            // Replace 'rabbitmq' with the host name of your RabbitMQ container
            cfg.Host("amqp://guest:guest@rabbitmq:5672");

            // MassTransit doesn't use the custom 'IPublisher'/'Publisher' classes,
            // it uses IBus and IMessageScheduler services for publishing.
        });
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    // Run the data seeding logic for Courses
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            Log.Information("Seeding database for CoursesAPI");
            SeedData.Initialize(services);
            Log.Information("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database");
        }
    }


    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    // ... (rest of the file remains the same)

    app.UseAuthorization();
    app.MapControllers();

    // Map health check endpoints
    app.MapHealthChecks("/health", new HealthCheckOptions()
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/liveness", new HealthCheckOptions
    {
        Predicate = r => r.Name.Contains("self")
    });

    Log.Information("CoursesAPI web host started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CoursesAPI web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}