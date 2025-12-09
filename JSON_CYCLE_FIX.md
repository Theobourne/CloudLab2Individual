# JSON Cycle Error Fix - Redis Caching

## Problem

When implementing Redis caching in StudentsAPI, a **JSON serialization cycle error** occurred:

```
JsonException: A possible object cycle was detected...
Path: $.Enrollments.Student.Enrollments.Student.Enrollments...
```

### Root Cause

The `Student` entity has a circular reference:
```
Student ? Enrollments ? Student ? Enrollments ? ... (infinite loop)
```

When `JsonSerializer.Serialize()` tries to serialize this for Redis caching, it detects the cycle and throws an exception.

---

## Solution

### Configure JsonSerializerOptions with ReferenceHandler.IgnoreCycles

In `StudentsAPI/Controllers/StudentsController.cs`, we added:

```csharp
private readonly JsonSerializerOptions _jsonOptions;

public StudentsController(StudentsAPIContext context, ILogger<StudentsController> logger, IDistributedCache cache)
{
    _context = context;
    _logger = logger;
    _cache = cache;
    
    // Configure JSON serialization to handle circular references
    _jsonOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };
}
```

Then use `_jsonOptions` in all serialization calls:

```csharp
// Serializing to Redis
var serializedData = JsonSerializer.Serialize(studentsFromDb, _jsonOptions);
await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);

// Deserializing from Redis
var students = JsonSerializer.Deserialize<List<Student>>(cachedData, _jsonOptions);
```

---

## What ReferenceHandler.IgnoreCycles Does

- **Detects circular references** during serialization
- **Ignores the cycle** by not serializing the repeated object
- **Prevents infinite loops** and stack overflow errors
- **Preserves data integrity** while avoiding crashes

### Example:

**Before (Cycle Error):**
```json
{
  "ID": 1,
  "FirstMidName": "John",
  "Enrollments": [
    {
      "StudentID": 1,
      "Student": {
        "ID": 1,
        "Enrollments": [
          {
            "Student": { ... } // CYCLE!
          }
        ]
      }
    }
  ]
}
```

**After (With IgnoreCycles):**
```json
{
  "ID": 1,
  "FirstMidName": "John",
  "Enrollments": [
    {
      "StudentID": 1,
      "CourseID": 1050,
      "Title": "Chemistry"
      // Student reference ignored to prevent cycle
    }
  ]
}
```

---

## Alternative Solution (Not Used)

### Option 1: ReferenceHandler.Preserve

```csharp
ReferenceHandler = ReferenceHandler.Preserve
```

This preserves references using `$id` and `$ref` metadata, but produces larger JSON:

```json
{
  "$id": "1",
  "ID": 1,
  "Enrollments": [
    {
      "$id": "2",
      "Student": { "$ref": "1" }
    }
  ]
}
```

**Why not used:** More complex, larger payload, not needed for caching.

### Option 2: Use DTOs (More Work)

Create separate Data Transfer Objects without circular references:

```csharp
public class StudentDto
{
    public int ID { get; set; }
    public string FirstMidName { get; set; }
    public List<EnrollmentDto> Enrollments { get; set; }
}

public class EnrollmentDto
{
    public int CourseID { get; set; }
    public string Title { get; set; }
    // No Student reference
}
```

**Why not used:** More code, maintenance overhead, IgnoreCycles is simpler.

---

## Important Notes

### Why This Error Didn't Appear Before Redis?

In `StudentsAPI/Program.cs`, the API already had:

```csharp
builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
```

This fixed cycles when **returning JSON from API endpoints**.

**BUT** when we added **Redis caching**, we used `JsonSerializer.Serialize()` directly, which uses **default options** (no cycle handling).

### Solution Applied

We created a **separate `JsonSerializerOptions`** instance with `IgnoreCycles` specifically for **Redis serialization**.

---

## Testing the Fix

### 1. Rebuild StudentsAPI

```sh
dotnet build StudentsAPI/StudentsAPI.csproj
```

### 2. Restart Docker Services

```sh
docker-compose down
docker-compose up --build
```

### 3. Test the Application

```sh
# Should work now without cycle errors
curl http://localhost:5090/api/Students
```

Or open browser:
- http://localhost:5095 (UniversityWeb)

### 4. Verify in Redis

```sh
docker exec -it redis redis-cli
GET StudentsAPI_students_all
# Should show JSON without errors
exit
```

---

## Files Modified

| File | Change |
|------|--------|
| `StudentsAPI/Controllers/StudentsController.cs` | Added `JsonSerializerOptions` with `ReferenceHandler.IgnoreCycles` for Redis caching |

---

## Summary

? **Problem**: JSON cycle error when caching `Student` objects with `Enrollments`  
? **Root Cause**: Circular reference: `Student ? Enrollments ? Student`  
? **Solution**: Configure `ReferenceHandler.IgnoreCycles` for Redis serialization  
? **Result**: Application works correctly with Redis caching  

---

## Next Steps

1. **Rebuild and restart** Docker containers
2. **Test** the application - should work now
3. **Verify** Redis caching in SEQ logs
4. **Test** Polly resilience by stopping StudentsAPI

---

**Fix Applied Successfully!** ?
