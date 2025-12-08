using Contracts.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
// using Contracts.Models; // <-- Ensure you have the correct using for Enrollment

[Route("api/[controller]")]  // <-- ADD THIS LINE
[ApiController]              // <-- ADD THIS LINE
public class EnrollmentController : ControllerBase
{
    private readonly IBus _bus;

    public EnrollmentController(IBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Enrollment en)
    {
        // Publish the event. MassTransit automatically handles serialization.
        await _bus.Publish(en);

        return Ok();
    }
}