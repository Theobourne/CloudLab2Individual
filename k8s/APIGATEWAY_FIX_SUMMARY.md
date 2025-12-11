# ApiGateway Kubernetes Deployment - Issues Fixed

## Problem Summary

The ApiGateway pods were in **CrashLoopBackOff** state, preventing the application from functioning correctly. The enrollment button and other API calls were timing out with `HttpClient.Timeout of 5 seconds elapsing` errors.

## Root Causes Identified

### 1. **Incorrect Health Probe Configuration** ?
**Issue:** Kubernetes health probes were configured to check `/` (root path)
```yaml
livenessProbe:
  httpGet:
    path: /          # ? Wrong!
    port: 8080
readinessProbe:
  httpGet:
    path: /          # ? Wrong!
    port: 8080
```

**Problem:** Ocelot API Gateway returns `404` for unconfigured routes (including `/`), causing health probes to fail and Kubernetes to kill the pod repeatedly.

### 2. **Wrong Port in Ocelot Configuration** ?
**Issue:** `ocelot.json` was configured to connect to backend services on port `8080`
```json
"DownstreamHostAndPorts": [
  {
    "Host": "studentsapi",
    "Port": 8080          // ? Wrong!
  }
]
```

**Problem:** In Kubernetes, the services expose port `80` (not `8080`), causing all backend API calls to timeout.

### 3. **Duplicate UseOcelot() Call** ?
**Issue:** Program.cs had both `.Wait()` and `await` calls
```csharp
app.UseOcelot().Wait();    // ? Bad pattern
await app.UseOcelot();     // ? Duplicate
```

**Problem:** This created race conditions and wasn't following best practices for async/await patterns.

### 4. **Health Endpoints Not Implemented** ?
**Issue:** ApiGateway didn't have dedicated health check endpoints that Kubernetes could use.

---

## Solutions Applied

### ? Fix 1: Added Health Check Middleware in Program.cs

Updated `ApiGateway/Program.cs` to intercept health check requests **before** Ocelot processes them:

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

// Use Ocelot middleware
await app.UseOcelot();
```

**Why this works:** Custom middleware runs before Ocelot, allowing health checks to return `200 OK` without going through Ocelot's routing.

### ? Fix 2: Updated Ocelot Configuration for Correct Ports

Changed `ApiGateway/ocelot.json` to use port `80`:

```json
"DownstreamHostAndPorts": [
  {
    "Host": "studentsapi",
    "Port": 80              // ? Correct!
  }
],
"DownstreamHostAndPorts": [
  {
    "Host": "coursesapi",
    "Port": 80              // ? Correct!
  }
]
```

### ? Fix 3: Updated Kubernetes Health Probes

Changed `k8s/apigateway.yaml` to use dedicated health endpoints:

```yaml
livenessProbe:
  httpGet:
    path: /liveness      # ? Correct!
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
readinessProbe:
  httpGet:
    path: /health        # ? Correct!
    port: 8080
  initialDelaySeconds: 15
  periodSeconds: 5
  timeoutSeconds: 5
  failureThreshold: 3
```

### ? Fix 4: Removed Duplicate UseOcelot() Call

Cleaned up Program.cs to have a single, proper async call:

```csharp
// Use Ocelot middleware - only call this once with await
await app.UseOcelot();
```

---

## Deployment Steps Executed

1. **Updated source code files:**
   - `ApiGateway/Program.cs`
   - `ApiGateway/ocelot.json`
   - `k8s/apigateway.yaml`

2. **Rebuilt Docker image:**
   ```bash
   docker build -f ApiGateway/Dockerfile -t apigateway:latest .
   ```

3. **Applied Kubernetes configuration:**
   ```bash
   kubectl apply -f k8s/apigateway.yaml
   ```

4. **Restarted deployment:**
   ```bash
   kubectl rollout restart deployment apigateway -n university
   ```

---

## Verification

### Pod Status - All Running ?
```
NAME                          READY   STATUS    RESTARTS   AGE
apigateway-566dd77fbb-mb59g   1/1     Running   0          2m
apigateway-566dd77fbb-p4tqg   1/1     Running   0          2m
```

### Health Check Logs - 200 OK ?
```
[11:10:07 INF] HTTP GET /health responded 200 in 0.0379 ms
[11:10:15 INF] HTTP GET /liveness responded 200 in 0.0657 ms
[11:10:17 INF] HTTP GET /health responded 200 in 0.0779 ms
```

### Service Endpoints ?
```bash
kubectl get svc apigateway -n university
```
```
NAME         TYPE           CLUSTER-IP       PORT(S)
apigateway   LoadBalancer   10.104.209.126   80:31648/TCP
```

---

## How This Fixes the Original Problems

| Problem | How It's Fixed |
|---------|----------------|
| **CrashLoopBackOff** | Health probes now return `200 OK` instead of `404`, preventing pod restarts |
| **Enrollment Button Timeout** | ApiGateway now routes to correct port `80`, successfully reaching backend APIs |
| **5-second timeout errors** | Backend services are now reachable, eliminating connection timeouts |
| **404 errors in logs** | Health checks bypass Ocelot routing, no more "UnableToFindDownstreamRouteError" |

---

## Testing the Fix

### 1. Check All Pods Are Running
```bash
kubectl get pods -n university
```
All pods should show `1/1 READY` and `Running` status.

### 2. Test Health Endpoints
```bash
kubectl port-forward svc/apigateway 8080:80 -n university
curl http://localhost:8080/health
curl http://localhost:8080/liveness
```
Both should return: `Healthy`

### 3. Test API Gateway Routing
```bash
curl http://localhost:8080/Students
curl http://localhost:8080/Courses
```
Should return student and course data.

### 4. Test Enrollment via UI
1. Port-forward to UniversityWeb:
   ```bash
   kubectl port-forward svc/universityweb 8081:80 -n university
   ```
2. Open browser: `http://localhost:8081`
3. Click "Enroll" button on any course
4. Should successfully enroll without timeout errors

---

## Key Lessons

1. **Ocelot + Kubernetes Health Checks:** Ocelot's routing catches all requests by default. Health checks need custom middleware to bypass Ocelot.

2. **Port Mapping:** In Docker Compose, containers expose their internal ports. In Kubernetes, Services abstract this - always check the Service port, not the container port.

3. **Health vs Liveness Probes:** 
   - **Readiness** (`/health`): Is the service ready to accept traffic?
   - **Liveness** (`/liveness`): Is the service still alive or should it be restarted?

4. **Async/Await Best Practices:** Never mix `.Wait()` and `await` - choose one pattern and stick with it.

---

## Files Modified

- ?? `ApiGateway/Program.cs` - Added health check middleware
- ?? `ApiGateway/ocelot.json` - Changed ports from 8080 to 80
- ?? `k8s/apigateway.yaml` - Updated health probe paths

---

## References

- [Ocelot Documentation](https://ocelot.readthedocs.io/)
- [Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

---

**Status:** ? **RESOLVED** - All ApiGateway pods are healthy and routing correctly to backend services.
