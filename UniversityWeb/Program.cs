using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

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

    // Configure HTTP clients for APIs
    builder.Services.AddHttpClient("StudentsApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["StudentsApi:BaseUrl"] ?? builder.Configuration["StudentsApi__BaseUrl"] ?? "http://localhost:5001/");
    });
    builder.Services.AddHttpClient("CoursesApi", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["CoursesApi:BaseUrl"] ?? builder.Configuration["CoursesApi__BaseUrl"] ?? "http://localhost:5002/");
    });

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