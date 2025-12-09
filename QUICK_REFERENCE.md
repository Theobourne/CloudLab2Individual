# Quick Reference Card - Polly & Redis

## ?? Start Application
```bash
docker-compose up --build
```

## ?? Access URLs
| Service | URL |
|---------|-----|
| Frontend | http://localhost:5000 |
| StudentsAPI | http://localhost:5001 |
| CoursesAPI | http://localhost:5002 |
| SEQ Logs | http://localhost:5341 |
| RabbitMQ | http://localhost:15672 |

## ?? Health Checks
```bash
curl http://localhost:5001/health
curl http://localhost:5002/health
```

## ?? Redis Commands
```bash
# Connect to Redis
docker exec -it redis redis-cli

# View all cached keys
KEYS *

# View specific cache
GET StudentsAPI_students_all
GET CoursesAPI_courses_all

# Delete cache (force refresh)
DEL StudentsAPI_students_all

# Exit
exit
```

## ?? Test Polly Resilience
```bash
# Stop StudentsAPI to trigger retries
docker-compose stop studentsapi

# Try accessing frontend
# Open http://localhost:5000

# Check SEQ logs for retry attempts
# http://localhost:5341

# Restart StudentsAPI
docker-compose start studentsapi
```

## ?? Performance Comparison
```bash
# First request (cache miss ~100-200ms)
time curl http://localhost:5001/api/Students

# Second request (cache hit ~10-30ms)
time curl http://localhost:5001/api/Students
```

## ?? SEQ Log Queries
- `@Application = "StudentsAPI"` - StudentsAPI logs
- `@Application = "UniversityWeb"` - Frontend logs
- `cache` - Cache operations
- `Retry` - Polly retry events
- `Circuit breaker` - Circuit breaker events

## ?? Stop Application
```bash
docker-compose down

# With volume cleanup
docker-compose down -v
```

## ?? Key Implementation Details

### Polly Policies (UniversityWeb)
- **Retry**: 3 attempts, exponential backoff (2s, 4s, 8s)
- **Circuit Breaker**: Opens after 5 failures, breaks for 30s
- **Timeout**: 10 seconds per request

### Redis Caching (StudentsAPI & CoursesAPI)
- **TTL**: 5 minutes
- **Cache Keys**: 
  - `students_all`, `student_{id}`
  - `courses_all`, `course_{id}`
- **Invalidation**: On CREATE, UPDATE, DELETE, ENROLL

### Cache Hit Ratio
- First request: ? Cache miss ? Database query
- Subsequent requests (within 5 min): ? Cache hit ? Redis
- After 5 minutes: ? Cache expired ? Database query

## ? Success Indicators
- ? Redis reduces response time by 70-90%
- ? Polly retries visible in SEQ logs
- ? Circuit breaker opens after 5 failures
- ? Health checks show Redis status
- ? Cache invalidates on data changes

## ?? NuGet Packages Added
```
UniversityWeb:
  - Microsoft.Extensions.Http.Polly

StudentsAPI & CoursesAPI:
  - Microsoft.Extensions.Caching.StackExchangeRedis
  - AspNetCore.HealthChecks.Redis
```

## ?? Troubleshooting
```bash
# Check service status
docker-compose ps

# View logs for specific service
docker logs studentsapi
docker logs redis

# Restart specific service
docker-compose restart studentsapi

# Rebuild and restart
docker-compose up --build -d
```

---
**Documentation**: See `POLLY_AND_REDIS_IMPLEMENTATION.md` and `TESTING_GUIDE.md` for details.
