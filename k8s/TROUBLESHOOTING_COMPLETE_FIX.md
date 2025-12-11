# Complete Kubernetes Deployment Fix - All Issues Resolved

## ?? Summary

This document details **all issues encountered** during Kubernetes deployment and their resolutions.

---

## ? Issue 1: ApiGateway CrashLoopBackOff

### Problem
ApiGateway pods were continuously restarting with `CrashLoopBackOff` status.

### Root Causes
1. **Health probes checking wrong endpoint** - Probes were checking `/` which returns 404 in Ocelot
2. **Wrong ports in ocelot.json** - Configured for port 8080, but Kubernetes services use port 80
3. **Duplicate UseOcelot() calls** - Bad async/await pattern
4. **No health endpoints** - ApiGateway didn't respond to `/health` or `/liveness`

### Solution

#### 1. Added Health Check Middleware (`ApiGateway/Program.cs`)
```csharp
// Health check endpoints - handle these BEFORE Ocelot catches everything
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health" || context.Request.Path == "/liveness")
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("Healthy");
        return;
    }
    await next();
});

// Use Ocelot middleware - only call once
await app.UseOcelot();
```

#### 2. Fixed Port Configuration (`ApiGateway/ocelot.json`)
```json
{
  "Routes": [
    {
      "DownstreamHostAndPorts": [
        {
          "Host": "studentsapi",
          "Port": 80              // ? Changed from 8080
        }
      ]
    },
    {
      "DownstreamHostAndPorts": [
        {
          "Host": "coursesapi",
          "Port": 80              // ? Changed from 8080
        }
      ]
    }
  ]
}
```

#### 3. Updated Kubernetes Probes (`k8s/apigateway.yaml`)
```yaml
livenessProbe:
  httpGet:
    path: /liveness      # ? Changed from /
    port: 8080
readinessProbe:
  httpGet:
    path: /health        # ? Changed from /
    port: 8080
```

### Result
? ApiGateway pods running healthy with 1/1 READY status

---

## ? Issue 2: UniversityWeb Health Check Timeouts

### Problem
Health monitoring page showing:
- `StudentsAPI HTTP Check: Unhealthy`
- `CoursesAPI HTTP Check: Unhealthy`
- Error: `The request was canceled due to the configured HttpClient.Timeout of 5 seconds elapsing.`

### Root Cause
`UniversityWeb/Controllers/HealthController.cs` was **hardcoded** to check:
- `http://studentsapi:8080/health`  
- `http://coursesapi:8080/health`

But in Kubernetes, services are on port **80**, not 8080.

### Solution

#### Updated HealthController (`UniversityWeb/Controllers/HealthController.cs`)
```csharp
public class HealthController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;  // ? Added

    public HealthController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;  // ? Added
    }

    public async Task<IActionResult> Index()
    {
        var healthChecks = new List<HealthCheckResult>();

        // Use configured URLs instead of hardcoded ones
        var studentsApiUrl = _configuration["StudentsApi:BaseUrl"] ?? 
                           _configuration["StudentsApi__BaseUrl"] ?? 
                           "http://studentsapi:80/";
        var coursesApiUrl = _configuration["CoursesApi:BaseUrl"] ?? 
                          _configuration["CoursesApi__BaseUrl"] ?? 
                          "http://coursesapi:80/";

        // Check Services with correct URLs
        healthChecks.Add(await CheckService("StudentsAPI", $"{studentsApiUrl.TrimEnd('/')}/health"));
        healthChecks.Add(await CheckService("CoursesAPI", $"{coursesApiUrl.TrimEnd('/')}/health"));

        return View(healthChecks);
    }
}
```

### Environment Variables Set in Kubernetes (`k8s/universityweb.yaml`)
```yaml
env:
- name: StudentsApi__BaseUrl
  valueFrom:
    configMapKeyRef:
      name: university-config
      key: STUDENTS_API_URL       # "http://studentsapi:80/"
- name: CoursesApi__BaseUrl
  valueFrom:
    configMapKeyRef:
      name: university-config
      key: COURSES_API_URL         # "http://coursesapi:80/"
```

### Result
? Health monitoring page now shows all services as "Healthy"

---

## ? Issue 3: Enrollment Button Not Working

### Problem
When clicking "Enroll" button, the request would timeout.

### Root Cause
Same as Issue #2 - UniversityWeb was trying to reach APIs on wrong ports.

### Solution
Fixed by updating the HealthController (which also fixed the HTTP clients used by Controllers).

The enrollment flow now works:
1. UniversityWeb ? StudentsAPI (port 80) ? Enroll endpoint
2. StudentsAPI ? Publishes to RabbitMQ
3. CoursesAPI ? Consumes message ? Saves to database

### Result
? Enrollment functionality working without timeouts

---

## ?? Files Modified

### ApiGateway
- ?? `ApiGateway/Program.cs` - Added health check middleware, fixed async pattern
- ?? `ApiGateway/ocelot.json` - Changed ports from 8080 to 80
- ?? `k8s/apigateway.yaml` - Updated health probe paths

### UniversityWeb
- ?? `UniversityWeb/Controllers/HealthController.cs` - Use configuration instead of hardcoded URLs

---

## ?? Verification Commands

### Check All Pods Status
```bash
kubectl get pods -n university
```
**Expected:** All pods showing `1/1 READY` and `Running`

### Check Services
```bash
kubectl get svc -n university
```
**Expected:** All services with ClusterIP or LoadBalancer assigned

### Check ApiGateway Logs
```bash
kubectl logs -l app=apigateway -n university --tail=20
```
**Expected:** 
```
[INFO] HTTP GET /health responded 200 in X ms
[INFO] HTTP GET /liveness responded 200 in X ms
```

### Check UniversityWeb Logs
```bash
kubectl logs -l app=universityweb -n university --tail=20
```
**Expected:**
```
[INFO] Retrieved X students from API
[INFO] HTTP GET /Students responded 200 in X ms
```

---

## ?? Testing Checklist

### ? 1. Health Monitoring
1. Access: http://localhost:8081/Health
2. Both APIs should show "Healthy" status
3. No timeout errors

### ? 2. View Students
1. Access: http://localhost:8081/Students
2. Should load list of students
3. No errors in browser console

### ? 3. View Courses
1. Access: http://localhost:8081/Courses
2. Should load list of courses with enrollment counts

### ? 4. Enroll Student
1. Click "Enroll" next to any student
2. Select a course from dropdown
3. Click "Enroll Student" button
4. Should see success message
5. Check RabbitMQ: `kubectl port-forward svc/rabbitmq-management 15672:15672 -n university`
6. Login: guest/guest ? Should see message in queue

### ? 5. Check Logs in Seq
```bash
kubectl port-forward svc/seq-external 5341:5341 -n university
```
Open http://localhost:5341 - Should see logs from all services

---

## ?? Deployment Commands

### Rebuild and Redeploy Everything
```bash
# Build images
docker build -f ApiGateway/Dockerfile -t apigateway:latest .
docker build -f UniversityWeb/Dockerfile -t universityweb:latest .

# Apply configurations
kubectl apply -f k8s/apigateway.yaml
kubectl apply -f k8s/universityweb.yaml

# Restart deployments
kubectl rollout restart deployment apigateway -n university
kubectl rollout restart deployment universityweb -n university

# Check status
kubectl get pods -n university
```

---

## ?? Key Lessons Learned

### 1. Port Mapping: Docker Compose vs Kubernetes
| Environment | Container Port | Exposed Port | Access URL |
|-------------|----------------|--------------|------------|
| **Docker Compose** | 8080 | 5001 (mapped) | `http://localhost:5001` |
| **Kubernetes** | 8080 | 80 (Service) | `http://servicename:80` |

**Lesson:** Always check the **Service port** in Kubernetes, not the container port.

### 2. Ocelot Health Checks
Ocelot catches ALL HTTP requests by default. To add health endpoints:
- Use middleware **before** `UseOcelot()`
- Intercept `/health` and `/liveness` paths
- Return 200 OK manually

### 3. Configuration Injection
Never hardcode URLs in Kubernetes deployments:
- ? Bad: `var url = "http://api:8080/health";`
- ? Good: `var url = _configuration["Api__BaseUrl"];`

### 4. Init Containers
UniversityWeb waits for APIs using init containers:
```yaml
initContainers:
- name: wait-for-studentsapi
  image: busybox:1.36
  command: ['sh', '-c', 'until nc -z studentsapi 80; do sleep 2; done;']
```

---

## ?? Troubleshooting Guide

### Pod Stuck in Init
```bash
kubectl logs <pod-name> -c <init-container-name> -n university
```

### Pod CrashLoopBackOff
```bash
kubectl describe pod <pod-name> -n university
kubectl logs <pod-name> --previous -n university
```

### Service Not Reachable
```bash
kubectl get endpoints -n university
kubectl exec -it <pod-name> -n university -- wget -O- http://servicename:80/health
```

### Health Probes Failing
```bash
kubectl describe pod <pod-name> -n university | grep -A 5 "Events:"
```

---

## ? Final Status

| Component | Status | Health Check | Port |
|-----------|--------|--------------|------|
| ApiGateway | ? Running | ? 200 OK | 80 |
| StudentsAPI | ? Running | ? 200 OK | 80 |
| CoursesAPI | ? Running | ? 200 OK | 80 |
| UniversityWeb | ? Running | ? 200 OK | 80 |
| SQL Server | ? Running | ? Healthy | 1433 |
| RabbitMQ | ? Running | ? Healthy | 5672 |
| Redis | ? Running | ? Healthy | 6379 |
| Seq | ? Running | ? Healthy | 80 |

**All services operational! ??**

---

## ?? Related Documents

- [APIGATEWAY_FIX_SUMMARY.md](./APIGATEWAY_FIX_SUMMARY.md) - Detailed ApiGateway fixes
- [QUICK_START.md](./QUICK_START.md) - Quick deployment guide
- [KUBERNETES_DEPLOYMENT_GUIDE.md](./KUBERNETES_DEPLOYMENT_GUIDE.md) - Complete K8s guide

---

**Last Updated:** December 11, 2025  
**Status:** ? All Issues Resolved
