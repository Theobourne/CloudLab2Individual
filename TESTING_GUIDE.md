# Quick Testing Guide - Polly & Redis

## Prerequisites
Ensure Docker Desktop is running.

## 1. Start the Application

```bash
docker-compose up --build
```

Wait for all services to be healthy (check logs).

---

## 2. Test Redis Caching

### Access StudentsAPI directly:
```bash
# First request (cache miss - slower)
curl http://localhost:5001/api/Students

# Second request (cache hit - faster)
curl http://localhost:5001/api/Students
```

### Check Redis cache:
```bash
# Connect to Redis container
docker exec -it redis redis-cli

# View all keys
KEYS *

# View cached students data
GET StudentsAPI_students_all

# View cached course data
GET CoursesAPI_courses_all

# Exit Redis CLI
exit
```

### Verify in Logs (SEQ):
1. Go to http://localhost:5341
2. Search for: `Retrieved students from Redis cache`
3. You should see cache hit logs on subsequent requests

---

## 3. Test Polly Resilience

### Simulate API Failure:
```bash
# Stop StudentsAPI
docker-compose stop studentsapi

# Try accessing UniversityWeb
# Open browser: http://localhost:5000
```

### Expected Behavior:
1. **Retry Policy**: Polly will retry 3 times (2s, 4s, 8s delays)
2. **Circuit Breaker**: After 5 consecutive failures, circuit opens
3. **Timeout**: Each request times out after 10 seconds

### View in Logs (SEQ):
1. Go to http://localhost:5341
2. Search for: `Retry` or `Circuit breaker`
3. You'll see retry attempts and circuit breaker state changes

### Restore Service:
```bash
# Restart StudentsAPI
docker-compose start studentsapi

# Circuit breaker will close after successful requests
```

---

## 4. Test Cache Invalidation

### Enroll a Student in a Course:
1. Open browser: http://localhost:5000
2. Click "Enroll" button on any student
3. Select a course and enroll

### Verify Cache Invalidation:
```bash
# In Redis CLI
docker exec -it redis redis-cli
GET StudentsAPI_student_1
# Should be empty (invalidated)

GET StudentsAPI_students_all
# Should be empty (invalidated)
```

Next request will be a cache miss and refresh the cache.

---

## 5. Test Health Checks

### Check StudentsAPI Health:
```bash
curl http://localhost:5001/health
```

**Expected Response:**
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

### Check CoursesAPI Health:
```bash
curl http://localhost:5002/health
```

### Simulate Redis Failure:
```bash
# Stop Redis
docker-compose stop redis

# Check health again
curl http://localhost:5001/health
```

**Expected Response:**
```json
{
  "status": "Unhealthy",
  "results": {
    "StudentsDB": { "status": "Healthy" },
    "Redis": { "status": "Unhealthy" },
    "self": { "status": "Healthy" }
  }
}
```

```bash
# Restart Redis
docker-compose start redis
```

---

## 6. Performance Testing

### Without Cache (First Request):
```bash
time curl http://localhost:5001/api/Students
# Response time: ~100-200ms (database query)
```

### With Cache (Second Request):
```bash
time curl http://localhost:5001/api/Students
# Response time: ~10-30ms (Redis cache)
```

### Cache Expiration (After 5 minutes):
Wait 5 minutes, then request again:
```bash
time curl http://localhost:5001/api/Students
# Response time: Back to ~100-200ms (cache expired, database query)
```

---

## 7. View All Logs in SEQ

1. Open http://localhost:5341
2. Useful queries:
   - `@Application = "StudentsAPI"` - StudentsAPI logs only
   - `@Application = "UniversityWeb"` - Frontend logs
   - `cache` - All cache-related logs
   - `Retry` - Polly retry logs
   - `Circuit breaker` - Circuit breaker events

---

## 8. Cleanup

```bash
# Stop all services
docker-compose down

# Remove volumes (clears data)
docker-compose down -v
```

---

## Common Issues

### Issue: Redis connection failed
**Solution**: Ensure Redis container is healthy
```bash
docker-compose ps
docker logs redis
```

### Issue: Polly not retrying
**Solution**: Check SEQ logs for error details
- Ensure StudentsAPI/CoursesAPI are stopped to trigger retries
- Look for HTTP status codes in logs

### Issue: Cache not invalidating
**Solution**: Check Redis keys
```bash
docker exec -it redis redis-cli
KEYS *
# Manually delete a key
DEL StudentsAPI_students_all
```

---

## Success Indicators

? Redis cache reduces response time by 70-90%  
? Polly retries appear in logs when services fail  
? Circuit breaker opens after 5 consecutive failures  
? Health checks show Redis status  
? Cache invalidates on data modifications  
? SEQ shows all logging events correctly  

Happy Testing! ??
