# Seq Troubleshooting Guide

## Common Issues and Solutions

### 1. Autofac Dependency Injection Error

**Error Message:**
```
An exception was thrown while activating ?:Autofac.Extensions.DependencyInjection.AutofacServiceProvider
```

**Cause:** This error typically occurs when:
- Seq data volume is corrupted
- Seq container is not fully initialized before applications try to connect
- Port configuration mismatch

**Solutions:**

#### Solution A: Clean Seq Data Volume (Recommended First Step)
```powershell
# Stop all containers
docker-compose down

# Remove the Seq data volume
docker volume rm lab21-university-8_seq-data

# Or remove all volumes if you want a complete fresh start
docker-compose down -v

# Restart services
docker-compose up -d
```

#### Solution B: Verify Seq is Running
```powershell
# Check if Seq container is running
docker ps | findstr seq

# Check Seq logs
docker logs seq

# Check Seq health
docker inspect seq --format='{{.State.Health.Status}}'
```

#### Solution C: Test Seq Connectivity
```powershell
# From host machine
curl http://localhost:5341

# From inside a container (e.g., studentsapi)
docker exec studentsapi curl http://seq:80
```

### 2. Connection Configuration

**Correct Configuration:**

In `docker-compose.override.yml`:
```yaml
services:
  studentsapi:
    environment:
      - Seq__ServerUrl=http://seq:80  # Use container name and internal port
```

In `Program.cs`:
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("Seq__ServerUrl") ?? "http://localhost:5341")
    .CreateLogger();
```

**Important Notes:**
- Inside Docker network: Use `http://seq:80`
- From host machine: Use `http://localhost:5341`
- The port mapping `5341:80` means external port 5341 maps to internal port 80

### 3. Startup Order Issues

Make sure services wait for Seq to be healthy before starting:

```yaml
studentsapi:
  depends_on:
    seq:
      condition: service_healthy  # Wait for health check to pass
```

### 4. Verification Steps

After fixing issues, verify Seq is working:

1. **Access Seq UI:**
   - Open browser: http://localhost:5341
   - You should see the Seq dashboard

2. **Check for Logs:**
   - In Seq UI, you should see logs from:
     - StudentsAPI
     - CoursesAPI
     - ApiGateway
     - UniversityWeb

3. **Verify Log Properties:**
   - Each application should have an "Application" property with its name
   - MachineName should show the container ID
   - Environment should show "Development"

### 5. Complete Reset Procedure

If all else fails, do a complete reset:

```powershell
# Stop all services
docker-compose down

# Remove all volumes (WARNING: This will delete all data)
docker-compose down -v

# Remove all related images
docker images | findstr "studentsapi\|coursesapi\|apigateway\|universityweb" | ForEach-Object { docker rmi ($_ -split '\s+')[2] }

# Rebuild and start fresh
docker-compose build --no-cache
docker-compose up -d

# Monitor the logs
docker-compose logs -f seq
```

### 6. Common Serilog Configuration Issues

**Problem:** Logs not appearing in Seq

**Checklist:**
- [ ] Serilog package installed: `Serilog.Sinks.Seq`
- [ ] Environment variable set correctly in docker-compose.override.yml
- [ ] UseSerilog() called on the builder.Host
- [ ] UseSerilogRequestLogging() added to middleware pipeline
- [ ] Log.CloseAndFlush() called in finally block

### 7. Network Connectivity Test

Test if services can reach Seq:

```powershell
# Test from studentsapi container
docker exec studentsapi curl -v http://seq:80/health

# Test from coursesapi container
docker exec coursesapi curl -v http://seq:80/health

# Expected response: HTTP 200 with JSON health status
```

### 8. Debugging Tips

**Enable Serilog Self-Logging:**

Add to Program.cs before creating the logger:

```csharp
using Serilog.Debugging;

// Enable Serilog's internal logging
SelfLog.Enable(msg => Console.WriteLine($"SERILOG: {msg}"));

Log.Logger = new LoggerConfiguration()
    // ... rest of configuration
```

This will show any Serilog-specific errors in the console output.

## Quick Reference Commands

```powershell
# View Seq logs
docker logs seq -f

# Restart just Seq
docker-compose restart seq

# Check Seq health endpoint
curl http://localhost:5341/health

# View all container logs
docker-compose logs -f

# Check which containers are healthy
docker-compose ps
```
