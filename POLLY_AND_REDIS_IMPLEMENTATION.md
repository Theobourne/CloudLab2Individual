# Polly HTTP Resilience and Redis Distributed Caching Implementation

This document describes the implementation of **Polly for HTTP call resilience** and **Redis distributed caching** in the University microservices application.

## 1. Polly HTTP Resilience (UniversityWeb)

### What is Polly?
Polly is a .NET library that provides resilience and transient fault handling for HTTP calls. It helps make your microservices more robust by automatically handling failures.

### Implementation Location
- **Project**: `UniversityWeb`
- **File**: `UniversityWeb/Program.cs`

### Policies Implemented

#### 1. **Retry Policy**
- **Purpose**: Automatically retry failed HTTP requests
- **Configuration**: 
  - Retries: 3 attempts
  - Backoff: Exponential (2^retry seconds: 2s, 4s, 8s)
- **Handles**: HTTP 5xx errors and 408 (Request Timeout)
- **Logging**: Logs each retry attempt with the delay duration

#### 2. **Circuit Breaker Policy**
- **Purpose**: Prevent cascading failures by "opening the circuit" after repeated failures
- **Configuration**:
  - Opens after: 5 consecutive failures
  - Break duration: 30 seconds
  - After 30 seconds, the circuit closes and retries
- **Logging**: Logs when circuit opens and resets

#### 3. **Timeout Policy**
- **Purpose**: Prevent hanging requests
- **Configuration**: 10 seconds per request
- **Prevents**: Long-running requests from blocking the application

### Applied To
- **StudentsApi** HTTP client
- **CoursesApi** HTTP client

### Benefits
- ? Handles transient network failures automatically
- ? Prevents system overload during outages
- ? Improves user experience with automatic retries
- ? Detailed logging for debugging

---

## 2. Redis Distributed Caching

### What is Redis?
Redis is an in-memory data store used for distributed caching. It reduces database load and improves performance by caching frequently accessed data.

### Docker Configuration
Added Redis service to `docker-compose.yml`:
```yaml
redis:
  image: redis:7-alpine
  container_name: redis
  ports:
    - "6379:6379"
  volumes:
    - redis-data:/data
  restart: unless-stopped
  healthcheck:
    test: ["CMD", "redis-cli", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### Implementation Locations

#### StudentsAPI
- **File**: `StudentsAPI/Program.cs` - Redis configuration and health check
- **File**: `StudentsAPI/Controllers/StudentsController.cs` - Caching logic

#### CoursesAPI
- **File**: `CoursesAPI/Program.cs` - Redis configuration and health check
- **File**: `CoursesAPI/Controllers/CoursesController.cs` - Caching logic

### Caching Strategy

#### Cache Keys
- **All Students**: `students_all`
- **Individual Student**: `student_{id}`
- **All Courses**: `courses_all`
- **Individual Course**: `course_{id}`

#### Cache Duration
- **TTL (Time To Live)**: 5 minutes
- After 5 minutes, cache expires and data is re-fetched from database

#### Cache Invalidation
Cache is invalidated (removed) on:
- **CREATE**: New student/course added ? invalidate "all" list
- **UPDATE**: Student/course modified ? invalidate specific item + "all" list
- **DELETE**: Student/course removed ? invalidate specific item + "all" list
- **ENROLL**: Student enrolls in course ? invalidate specific student + "all" students

### Cached Endpoints

#### StudentsAPI
- `GET /api/Students` - Caches all students with enrollments
- `GET /api/Students/{id}` - Caches individual student with enrollments

#### CoursesAPI
- `GET /api/Courses` - Caches all courses
- `GET /api/Courses/{id}` - Caches individual course

### Benefits
- ? Reduces database load (especially for read-heavy operations)
- ? Improves response times (in-memory access is faster than database)
- ? Scales horizontally (multiple instances share the same cache)
- ? Health checks ensure Redis availability

---

## 3. Health Checks

Both StudentsAPI and CoursesAPI now include health checks for:
- **Database** (SQL Server)
- **Redis** (Cache availability)
- **Self** (API is alive)

### Endpoints
- `/health` - Full health check (all services)
- `/liveness` - Simple liveness probe (API is running)

### Health Check Response Example
```json
{
  "status": "Healthy",
  "results": {
    "StudentsDB": { "status": "Healthy" },
    "Redis": { "status": "Healthy" },
    "self": { "status": "Healthy" }
  }
}
```

---

## 4. NuGet Packages Added

### UniversityWeb
- `Microsoft.Extensions.Http.Polly` - Polly integration with HttpClient

### StudentsAPI & CoursesAPI
- `Microsoft.Extensions.Caching.StackExchangeRedis` - Redis cache client
- `AspNetCore.HealthChecks.Redis` - Redis health checks

---

## 5. Testing the Implementation

### Test Polly Resilience
1. Stop StudentsAPI or CoursesAPI
2. Try to access UniversityWeb
3. Check logs - you should see retry attempts
4. After 5 failures, circuit breaker opens
5. Restart the API - circuit breaker closes

### Test Redis Caching
1. Start all services with `docker-compose up`
2. Access `http://localhost:5001/api/Students` (first time - slow, cache miss)
3. Access again - faster (cache hit)
4. Check logs for "Retrieved students from Redis cache"
5. Wait 5 minutes or modify a student - cache invalidated

### View Redis Data
Connect to Redis CLI:
```bash
docker exec -it redis redis-cli
KEYS *
GET StudentsAPI_students_all
```

---

## 6. Configuration

### Environment Variables
If using different Redis connection string:
```bash
Redis__ConnectionString=your-redis-host:6379
```

### Docker Compose
Redis is automatically configured when using `docker-compose up`:
- Connection string: `redis:6379`
- Health check ensures services wait for Redis

---

## 7. Logging

All operations are logged with Serilog to SEQ:
- Cache hits/misses
- Retry attempts
- Circuit breaker state changes
- Cache invalidation events

View logs at: `http://localhost:5341`

---

## Summary

? **Polly HTTP Resilience**: Implemented retry, circuit breaker, and timeout policies in UniversityWeb  
? **Redis Distributed Caching**: Implemented in StudentsAPI and CoursesAPI with 5-minute TTL  
? **Health Checks**: Added Redis health checks to monitor cache availability  
? **Cache Invalidation**: Automatic cache clearing on data modifications  
? **Logging**: Comprehensive logging for debugging and monitoring  

These implementations make the application more robust, performant, and production-ready!
