// EnrollmentDataCollector.cs (MassTransit Consumer)
using MassTransit;
using StudentsAPI.Data;
using Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

public class EnrollmentDataCollector : IConsumer<Enrollment>
{
    private readonly IServiceProvider _serviceProvider; // Keep for scoping
    private readonly ILogger<EnrollmentDataCollector> _logger;

    public EnrollmentDataCollector(IServiceProvider serviceProvider, ILogger<EnrollmentDataCollector> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Enrollment> context)
    {
        var enrollment = context.Message; // The deserialized message is here
        
        _logger.LogInformation("Received enrollment message from queue: StudentID={StudentId}, CourseID={CourseId}, Title={CourseTitle}", 
            enrollment.StudentID, enrollment.CourseID, enrollment.Title);

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                // Database interaction logic
                var _context = scope.ServiceProvider.GetRequiredService<StudentsAPIContext>();
                
                // Check if already exists
                var existing = await _context.Enrollment
                    .FirstOrDefaultAsync(e => e.StudentID == enrollment.StudentID && e.CourseID == enrollment.CourseID);
                
                if (existing != null)
                {
                    _logger.LogWarning("Enrollment already exists: StudentID={StudentId}, CourseID={CourseId}. Skipping.", 
                        enrollment.StudentID, enrollment.CourseID);
                    return;
                }
                
                _context.Enrollment.Add(enrollment);
                await _context.SaveChangesAsync(); // Use async SaveChanges
                
                _logger.LogInformation("Successfully saved enrollment to database: StudentID={StudentId}, CourseID={CourseId}", 
                    enrollment.StudentID, enrollment.CourseID);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing enrollment message: StudentID={StudentId}, CourseID={CourseId}", 
                enrollment.StudentID, enrollment.CourseID);
            throw; // Re-throw to let MassTransit handle retry logic
        }
        // MassTransit handles the 'return true' (ACK) automatically
    }
}