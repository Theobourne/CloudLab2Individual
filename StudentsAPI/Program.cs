using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentsAPI.Data;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
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
    .Enrich.WithProperty("Application", "StudentsAPI")
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("Seq__ServerUrl") ?? "http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Starting StudentsAPI web host");

    var builder = WebApplication.CreateBuilder(args);
    
    // Use Serilog for logging
    builder.Host.UseSerilog();

    builder.Services.AddDbContext<StudentsAPIContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("StudentsAPIContext") ?? throw new InvalidOperationException("Connection string 'StudentsAPIContext' not found.")));

    // Add services to the container.

    //builder.Services.AddControllers();
    builder.Services.AddControllers().AddJsonOptions(x =>
                    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Add Health Checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("StudentsAPIContext") ?? "",
            name: "StudentsDB",
            tags: new[] { "db", "sql", "sqlserver" })
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // 2. Add MassTransit
    builder.Services.AddMassTransit(x =>
    {
        // Register the consumer class
        x.AddConsumer<EnrollmentDataCollector>();

        // Configure RabbitMQ as the transport
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("amqp://guest:guest@rabbitmq:5672");

            // Receive endpoint configuration: binds the consumer to a queue
            cfg.ReceiveEndpoint("enrollment_queue", e =>
            {
                // Attach the consumer to this endpoint/queue
                e.ConfigureConsumer<EnrollmentDataCollector>(context);
            });
        });
    });

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            Log.Information("Seeding database for StudentsAPI");
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

    Log.Information("StudentsAPI web host started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "StudentsAPI web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
