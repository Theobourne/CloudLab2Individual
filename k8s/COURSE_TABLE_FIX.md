# Course Table Missing - Final Fix

## Problem
Courses page and enrollment functionality were not working, showing HTTP 500 errors.

## Root Cause
The `Course` table was **missing** from the database!

### Investigation Steps
1. Checked CoursesAPI logs ? Found SQL error: `Invalid object name 'Course'`
2. Connected to SQL Server ? Found only `Student` and `Enrollment` tables
3. CoursesAPI's `SeedData.Initialize()` was failing silently during startup

## Why Did This Happen?
Both `StudentsAPI` and `CoursesAPI` use the **same database** (`ContosoUniversity1`), but:
- StudentsAPI's `SeedData` creates `Student` and `Enrollment` tables
- CoursesAPI's `SeedData` should create `Course` table
- The `context.Database.EnsureCreated()` in CoursesAPI's SeedData **didn't create the table** because the database already existed (created by StudentsAPI)

### From EntityFrameworkCore Documentation:
> `EnsureCreated()` - Ensures that the database for the context exists. If it exists, no action is taken. If it does not exist then the database and all its schema are created.

**Key point:** If the database already exists, it does **NOT** create missing tables!

## Solution Applied

### Manual Fix (What Was Done)
```sql
-- 1. Create Course table
USE ContosoUniversity1;
CREATE TABLE Course (
    CourseID INT PRIMARY KEY NOT NULL,
    Title NVARCHAR(50) NOT NULL,
    Credits INT NOT NULL
);

-- 2. Insert seed data
INSERT INTO Course (CourseID, Title, Credits) VALUES 
(1050, 'Chemistry', 3),
(4022, 'Microeconomics', 3),
(4041, 'Macroeconomics', 3),
(1045, 'Calculus', 4),
(3141, 'Trigonometry', 4),
(2021, 'Composition', 3),
(2042, 'Literature', 4);
```

### Commands Used
```powershell
# Get SQL pod
$pod = kubectl get pod -n university -l app=sqldata -o jsonpath='{.items[0].metadata.name}'

# Create table
kubectl exec -it $pod -n university -- /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'My!P@ssword1' -Q "USE ContosoUniversity1; CREATE TABLE Course (CourseID INT PRIMARY KEY NOT NULL, Title NVARCHAR(50) NOT NULL, Credits INT NOT NULL);"

# Insert data
kubectl exec -it $pod -n university -- /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'My!P@ssword1' -Q "USE ContosoUniversity1; INSERT INTO Course (CourseID, Title, Credits) VALUES (1050, 'Chemistry', 3), (4022, 'Microeconomics', 3), (4041, 'Macroeconomics', 3), (1045, 'Calculus', 4), (3141, 'Trigonometry', 4), (2021, 'Composition', 3), (2042, 'Literature', 4);"
```

## Better Long-Term Solutions

### Option 1: Use Migrations (Recommended)
Replace `context.Database.EnsureCreated()` with proper EF Core migrations:

```powershell
# In CoursesAPI directory
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Update `Program.cs`:
```csharp
// Replace EnsureCreated with migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CoursesAPIContext>();
    context.Database.Migrate(); // ? Use Migrate() instead of EnsureCreated()
    SeedData.Initialize(scope.ServiceProvider);
}
```

### Option 2: Separate Databases
Give each API its own database:

**StudentsAPI:**
```
Server=sqldata;Database=StudentsDB;...
```

**CoursesAPI:**
```
Server=sqldata;Database=CoursesDB;...
```

### Option 3: Shared Schema with Migrations
Use a shared database but coordinate migrations between both APIs.

## Verification

### Test CoursesAPI Directly
```powershell
kubectl port-forward svc/coursesapi 5002:80 -n university
curl http://localhost:5002/api/Courses
```

**Expected Response:**
```json
[
  {"courseID":1045,"title":"Calculus","credits":4},
  {"courseID":1050,"title":"Chemistry","credits":3},
  {"courseID":2021,"title":"Composition","credits":3},
  {"courseID":2042,"title":"Literature","credits":4},
  {"courseID":3141,"title":"Trigonometry","credits":4},
  {"courseID":4022,"title":"Microeconomics","credits":3},
  {"courseID":4041,"title":"Macroeconomics","credits":3}
]
```

### Test Web UI
1. Navigate to http://localhost:8081/Courses
2. Should see all 7 courses displayed
3. Click "Enroll" on any student
4. Should see all courses in the dropdown
5. Successfully enroll student

## Status
? **RESOLVED** - Course table created and seeded, all functionality working

## Files That Need Long-Term Fix
- ? `CoursesAPI/Program.cs` - Still using `EnsureCreated()`
- ? `StudentsAPI/Program.cs` - Still using `EnsureCreated()`

**Recommendation:** Implement EF Core Migrations for production deployments.

---

**Date Fixed:** December 11, 2025  
**Method:** Manual SQL table creation via kubectl exec
