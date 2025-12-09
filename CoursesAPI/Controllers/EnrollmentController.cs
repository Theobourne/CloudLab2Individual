using Contracts.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // <-- ADD USING FOR ILogger

// using Contracts.Models; // <-- Ensure you have the correct using for Enrollment

[Route("api/[controller]")]  // <-- ADD THIS LINE
[ApiController]              // <-- ADD THIS LINE
public class EnrollmentController : ControllerBase
{
    private readonly IBus _bus;
    private readonly ILogger<EnrollmentController> _logger; // <-- ADD LOGGER

    public EnrollmentController(IBus bus, ILogger<EnrollmentController> logger) // <-- ADD LOGGER TO CONSTRUCTOR
    {
        _bus = bus;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Enrollment en)
    {
        _logger.LogInformation("Publishing enrollment event: StudentID={StudentId}, CourseID={CourseId}, Title={CourseTitle}", 
            en.StudentID, en.CourseID, en.Title);
        
        try
        {
            // Publish the event. MassTransit automatically handles serialization.
            await _bus.Publish(en);

            _logger.LogInformation("Successfully published enrollment event for StudentID={StudentId}, CourseID={CourseId}", 
                en.StudentID, en.CourseID);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing enrollment event for StudentID={StudentId}, CourseID={CourseId}", 
                en.StudentID, en.CourseID);
            throw;
        }
    }
}