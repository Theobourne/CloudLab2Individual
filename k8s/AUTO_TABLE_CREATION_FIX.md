# Database Table Auto-Creation Fix

## Problem Statement

When deploying to a new environment, if the database already exists but is missing specific tables (e.g., `Course` table), the application would fail with SQL errors like:

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid object name 'Course'.
```

### Why This Happens

`EnsureCreated()` only creates the database and its schema if the **database doesn't exist**. If the database already exists (created by another API), it does nothing, leaving tables missing.

Example scenario:
1. **StudentsAPI starts first** ? Creates `ContosoUniversity1` database with `Student` and `Enrollment` tables
2. **CoursesAPI starts second** ? Database exists, so `EnsureCreated()` does nothing
3. **Course table is missing** ? Application crashes when trying to query it

---

## Solution Applied

### CoursesAPI - Robust Table Creation

**Updated: `CoursesAPI/Data/SeedData.cs`**

Added three layers of protection:

#### 1. Check if Table Exists
```csharp
private static bool CheckIfCourseTableExists(CoursesAPIContext context, ILogger logger)
{
    try
    {
        // Try to query the Course table
        _ = context.Course.Any();
        return true;
    }
    catch (SqlException ex) when (ex.Number == 208) // Invalid object name
    {
        logger.LogWarning("Course table does not exist (SQL Error 208).");
        return false;
    }
}
```

**How it works:**
- Attempts to query `Course.Any()`
- If table exists ? Returns `true`
- If SQL error 208 (Invalid object name) ? Returns `false`
- Logs warning for debugging

#### 2. Create Table if Missing
```csharp
private static void CreateCourseTable(CoursesAPIContext context, ILogger logger)
{
    var createTableSql = @"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Course')
        BEGIN
            CREATE TABLE Course (
                CourseID INT PRIMARY KEY NOT NULL,
                Title NVARCHAR(50) NOT NULL,
                Credits INT NOT NULL
            );
        END";
    
    context.Database.ExecuteSqlRaw(createTableSql);
    logger.LogInformation("Course table created successfully.");
}
```

**Safety features:**
- `IF NOT EXISTS` check prevents errors if table already exists
- Uses raw SQL for precise control
- Matches EF Core model exactly
- Logs creation for audit trail

#### 3. Initialize Flow
```csharp
public static void Initialize(IServiceProvider serviceProvider)
{
    // 1. Ensure database exists
    context.Database.EnsureCreated();
    
    // 2. Check if Course table exists
    bool courseTableExists = CheckIfCourseTableExists(context, logger);
    
    // 3. Create table if missing
    if (!courseTableExists)
    {
        CreateCourseTable(context, logger);
    }
    
    // 4. Seed data if table is empty
    if (!context.Course.Any())
    {
        // Add seed data...
    }
}
```

---

### StudentsAPI - Robust Table Creation

**Updated: `StudentsAPI/Data/SeedData.cs`**

Same pattern applied for `Student` and `Enrollment` tables:

#### Generic Table Check
```csharp
private static bool CheckIfTableExists(StudentsAPIContext context, string tableName, ILogger logger)
{
    var sql = $"SELECT CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}') THEN 1 ELSE 0 END";
    var exists = context.Database.SqlQueryRaw<int>(sql).ToList().FirstOrDefault() == 1;
    return exists;
}
```

**Why this approach:**
- Queries SQL Server system tables directly
- More reliable than catching exceptions
- Reusable for multiple tables

#### Create Multiple Tables
```csharp
// Check and create Student table
if (!CheckIfTableExists(context, "Student", logger))
{
    CreateStudentTable(context, logger);
}

// Check and create Enrollment table
if (!CheckIfTableExists(context, "Enrollment", logger))
{
    CreateEnrollmentTable(context, logger);
}
```

---

## Benefits

### ? 1. Environment Portability
Works correctly on any machine/cluster without manual intervention:
- New developer machines
- Different Kubernetes clusters
- Fresh deployments
- After database resets

### ? 2. Startup Order Independence
Tables are created regardless of which API starts first:
- StudentsAPI starts first ? Creates its tables
- CoursesAPI starts second ? Creates its tables
- Order doesn't matter anymore!

### ? 3. Resilient to Partial Failures
If table creation failed previously:
- Next startup detects missing table
- Automatically creates it
- Application self-heals

### ? 4. Better Logging
Clear visibility into what's happening:
```
[INFO] Course table exists.
[WARN] Course table does not exist. Creating table...
[INFO] Course table created successfully.
[INFO] Seeding Course table with initial data...
[INFO] Course table seeded successfully with 7 courses.
```

### ? 5. Production Safe
Multiple safety mechanisms:
- `IF NOT EXISTS` in SQL prevents duplicate creation errors
- Try-catch blocks handle unexpected errors
- Logger captures all operations for debugging
- No breaking changes to existing data

---

## Testing the Fix

### Test Scenario 1: Fresh Database
```bash
# Delete namespace (wipes all data)
kubectl delete namespace university

# Redeploy
kubectl apply -f k8s/

# Check logs
kubectl logs -l app=coursesapi -n university | grep "Course table"
```

**Expected Output:**
```
[INFO] Course table does not exist. Creating table...
[INFO] Course table created successfully.
[INFO] Seeding Course table with initial data...
[INFO] Course table seeded successfully with 7 courses.
```

### Test Scenario 2: Existing Database, Missing Table
```bash
# Manually drop Course table
kubectl exec -it $(kubectl get pod -n university -l app=sqldata -o jsonpath='{.items[0].metadata.name}') -n university -- \
  /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'My!P@ssword1' -Q "USE ContosoUniversity1; DROP TABLE Course;"

# Restart CoursesAPI
kubectl rollout restart deployment coursesapi -n university

# Check logs
kubectl logs -l app=coursesapi -n university | grep "Course table"
```

**Expected Output:**
```
[WARN] Course table does not exist (SQL Error 208).
[WARN] Course table does not exist. Creating table...
[INFO] Course table created successfully.
```

### Test Scenario 3: Table Exists, Has Data
```bash
# Restart CoursesAPI
kubectl rollout restart deployment coursesapi -n university

# Check logs
kubectl logs -l app=coursesapi -n university | grep "Course"
```

**Expected Output:**
```
[INFO] Course table exists.
[INFO] Course table already has data. Skipping seed.
```

---

## Comparison: Before vs After

| Scenario | Before | After |
|----------|--------|-------|
| **Fresh deployment** | ? Course table missing ? App crashes | ? Table auto-created ? App works |
| **StudentsAPI starts first** | ? CoursesAPI fails | ? CoursesAPI creates own tables |
| **Database reset** | ? Manual table creation needed | ? Auto-recreates on next start |
| **Different PC** | ? Must manually fix | ? Works immediately |
| **Logging** | ? Silent failure | ? Clear logs explain what happened |

---

## Migration from EnsureCreated() to Migrations (Future Enhancement)

While this fix solves the immediate problem, **EF Core Migrations** are the long-term solution:

### Current Approach (SeedData with Table Creation)
```csharp
// Current: Check and create tables manually
context.Database.EnsureCreated(); // Limited
CreateCourseTable(context, logger); // Manual SQL
```

**Pros:**
- ? Simple to understand
- ? Works immediately
- ? No migration files to manage

**Cons:**
- ?? Can't handle schema changes
- ?? No version control of schema
- ?? Manual SQL for table creation

### Recommended Approach (Migrations)
```csharp
// Future: Use migrations
context.Database.Migrate(); // Applies all pending migrations
```

**To implement:**
```bash
# Create initial migration
cd CoursesAPI
dotnet ef migrations add InitialCreate

# This generates migration files in Migrations/ folder
# Deploy: context.Database.Migrate() applies them
```

**Pros:**
- ? Handles schema updates automatically
- ? Version controlled schema
- ? Rollback capability
- ? Team collaboration friendly

---

## Files Modified

- ?? **CoursesAPI/Data/SeedData.cs**
  - Added `CheckIfCourseTableExists()` method
  - Added `CreateCourseTable()` method
  - Enhanced `Initialize()` with table creation logic
  - Added comprehensive logging

- ?? **StudentsAPI/Data/SeedData.cs**
  - Added `CheckIfTableExists()` method (generic)
  - Added `CreateStudentTable()` method
  - Added `CreateEnrollmentTable()` method
  - Enhanced `Initialize()` with table creation logic
  - Added comprehensive logging

---

## Related Issues Fixed

This fix resolves:
1. ? **Issue:** `Invalid object name 'Course'` on fresh deployments
2. ? **Issue:** CoursesAPI crashes if StudentsAPI starts first
3. ? **Issue:** Manual SQL execution needed on new machines
4. ? **Issue:** Database reset breaks application

---

## Next Steps (Optional Improvements)

1. **Add Persistent Volumes** - Ensure data survives pod restarts
   - See: `k8s/COURSE_TABLE_FIX.md` for PVC configuration

2. **Migrate to EF Core Migrations** - Production-ready schema management
   ```bash
   dotnet ef migrations add InitialCreate
   # Replace EnsureCreated() with Migrate()
   ```

3. **Add Database Health Checks** - Verify tables exist
   ```csharp
   builder.Services.AddHealthChecks()
       .AddCheck("course_table", () => 
           context.Database.CanConnect() && context.Course.Any() 
               ? HealthCheckResult.Healthy() 
               : HealthCheckResult.Unhealthy());
   ```

4. **Add Integration Tests** - Verify table creation logic
   ```csharp
   [Fact]
   public async Task SeedData_Creates_Course_Table_If_Missing()
   {
       // Test the table creation logic
   }
   ```

---

## References

- [EF Core EnsureCreated](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [SQL Server Error Codes](https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors)

---

**Status:** ? **IMPLEMENTED** - Automatic table creation now works on any environment

**Date:** December 11, 2025
