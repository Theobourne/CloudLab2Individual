// EnrollmentDataCollector.cs (MassTransit Consumer)
using MassTransit;
using StudentsAPI.Data;
using Contracts.Models;

public class EnrollmentDataCollector : IConsumer<Enrollment>
{
    private readonly IServiceProvider _serviceProvider; // Keep for scoping

    public EnrollmentDataCollector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task Consume(ConsumeContext<Enrollment> context)
    {
        var enrollment = context.Message; // The deserialized message is here

        using (var scope = _serviceProvider.CreateScope())
        {
            // Database interaction logic
            var _context = scope.ServiceProvider.GetRequiredService<StudentsAPIContext>();
            _context.Enrollment.Add(enrollment);
            await _context.SaveChangesAsync(); // Use async SaveChanges
        }
        // MassTransit handles the 'return true' (ACK) automatically
    }
}