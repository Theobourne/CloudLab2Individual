using Microsoft.EntityFrameworkCore;
using CoursesAPI.Data; // <--- Change to your CoursesAPI project namespace
using System.Text.Json.Serialization;
using RabbitMQ.Client; // This is the standard RabbitMQ client library for ExchangeType
// For IConnectionProvider, ConnectionProvider, IPublisher, Publisher:
using EventBus.RabbitMQ;
using EventBus.RabbitMQ.Standard;
using Newtonsoft.Json; // Required for JsonConvert.SerializeObject in the controller
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add the database context registration
builder.Services.AddDbContext<CoursesAPIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CoursesAPIContext")
    ?? throw new InvalidOperationException("Connection string 'CoursesAPIContext' not found.")));

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

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

// Run the data seeding logic for Courses
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // Assuming you have a SeedData class for courses
    SeedData.Initialize(services);
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
app.Run();