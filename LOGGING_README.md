# Centralized Logging with Seq

This project implements centralized logging using Seq for all microservices in the University application.

## Overview

Seq is an intelligent search, analysis, and alerting server built for modern structured logs and traces. All services in this application are configured to send logs to a centralized Seq instance.

## Architecture

The following services are configured with Serilog and send logs to Seq:

1. **StudentsAPI** - Student management API
2. **CoursesAPI** - Course management API
3. **ApiGateway** - Ocelot API Gateway
4. **UniversityWeb** - Web frontend

## Accessing Seq

Once the application is running via Docker Compose, access the Seq UI at:

```
http://localhost:5341
```

## Log Structure

All logs include the following enrichment properties:

- **Application** - The name of the service (e.g., "StudentsAPI", "CoursesAPI")
- **MachineName** - The container/machine name where the service is running
- **EnvironmentName** - The environment (Development, Production, etc.)

## Structured Logging Examples

### API Controllers

Controllers use structured logging with named parameters:

```csharp
_logger.LogInformation("Retrieving student with ID: {StudentId}", id);
_logger.LogWarning("Student with ID: {StudentId} not found", id);
_logger.LogError(ex, "Error retrieving students");
```

### Database Operations

Database operations are logged with relevant entity information:

```csharp
_logger.LogInformation("Successfully created course with ID: {CourseId}", course.CourseID);
_logger.LogInformation("Retrieved {StudentCount} students", students.Count);
```

### Message Queue Operations

RabbitMQ message processing is logged:

```csharp
_logger.LogInformation("Received enrollment message from queue: StudentID={StudentId}, CourseID={CourseId}", 
    enrollment.StudentID, enrollment.CourseID);
```

## Configuration

### Environment Variables

Each service uses the following environment variable to configure the Seq server URL:

```yaml
Seq__ServerUrl: http://seq:80
```

This is configured in `docker-compose.override.yml`.

### Log Levels

Default log levels:
- **Information** - General application flow
- **Warning** - Unexpected but handled situations (e.g., not found, already exists)
- **Error** - Exceptions and errors
- **Fatal** - Application startup/shutdown failures

Microsoft framework logs are set to Warning level to reduce noise.

## Querying Logs in Seq

### Example Queries

Find all errors:
```
@Level = 'Error'
```

Find logs from specific service:
```
Application = 'StudentsAPI'
```

Find student-related operations:
```
StudentId is not null
```

Find enrollment operations:
```
CourseId is not null and StudentId is not null
```

Find operations for specific student:
```
StudentId = 1
```

Combine conditions:
```
Application = 'CoursesAPI' and @Level = 'Information'
```

## Docker Compose Configuration

### Seq Service

```yaml
seq:
  image: datalust/seq:latest
  container_name: seq
  environment:
    - ACCEPT_EULA=Y
  ports:
    - "5341:80"
  volumes:
    - seq-data:/data
```

### Service Dependencies

All application services depend on Seq to ensure it starts first:

```yaml
depends_on:
  seq:
    condition: service_started
```

## Benefits

1. **Centralized View** - All logs from all services in one place
2. **Structured Logs** - Easy to query and filter with structured properties
3. **Real-time Monitoring** - View logs as they happen
4. **Correlation** - Track requests across services using correlation IDs
5. **Alerting** - Configure alerts for specific log patterns (Seq feature)
6. **Performance Analysis** - HTTP request logging with timing information

## Request Logging

All services include Serilog request logging middleware which automatically logs:
- HTTP method and path
- Response status code
- Response time
- Additional context

Example log:
```
HTTP GET /api/Students responded 200 in 45.2ms
```

## Best Practices

1. **Use Structured Logging** - Always use named parameters: `{StudentId}` not string interpolation
2. **Log at Appropriate Levels** - Info for normal flow, Warning for unexpected, Error for exceptions
3. **Include Context** - Add relevant IDs and properties to make logs searchable
4. **Avoid Sensitive Data** - Don't log passwords, tokens, or personal information
5. **Use Correlation IDs** - Serilog automatically enriches logs with correlation context

## Troubleshooting

### Service can't connect to Seq

1. Ensure Seq container is running: `docker ps | grep seq`
2. Check Seq logs: `docker logs seq`
3. Verify network connectivity between containers

### No logs appearing in Seq

1. Check service logs: `docker logs <service-name>`
2. Verify Seq URL is correct in environment variables
3. Ensure Serilog packages are installed
4. Check that `UseSerilog()` is called in Program.cs

### Logs not structured properly

Ensure you're using named parameters in log statements:
```csharp
// Good ?
_logger.LogInformation("Student {StudentId} enrolled", studentId);

// Bad ?
_logger.LogInformation($"Student {studentId} enrolled");
```

## NuGet Packages Used

All services include:
- **Serilog.AspNetCore** (8.0.1) - Core Serilog integration
- **Serilog.Sinks.Seq** (8.0.0) - Seq sink for sending logs
- **Serilog.Enrichers.Environment** (3.0.1) - Environment enrichers

## Additional Resources

- [Seq Documentation](https://docs.datalust.co/docs)
- [Serilog Documentation](https://serilog.net/)
- [Structured Logging Best Practices](https://serilog.net/docs/structured-data)
