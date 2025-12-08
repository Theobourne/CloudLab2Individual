using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentsAPI.Data;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StudentsAPIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StudentsAPIContext") ?? throw new InvalidOperationException("Connection string 'StudentsAPIContext' not found.")));

// Add services to the container.

//builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x =>
                x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    SeedData.Initialize(services);
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
