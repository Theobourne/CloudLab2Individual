# Docker Compose vs Kubernetes - Quick Migration Reference

## ?? Key Concept Mappings

| Docker Compose | Kubernetes | Notes |
|----------------|------------|-------|
| `depends_on` | Init Containers + Readiness Probes | K8s doesn't have native dependency management |
| `container_name` | Service DNS Name | Services get DNS names like `servicename.namespace.svc.cluster.local` |
| `volumes` | PersistentVolumeClaim (PVC) | Dynamic provisioning or pre-created PVs |
| `ports` | Service + containerPort | Service exposes pods |
| `environment` | ConfigMap + Secret | Separate sensitive from non-sensitive data |
| `restart: unless-stopped` | Deployment replicas | Controllers ensure desired state |
| `healthcheck` | livenessProbe + readinessProbe | More granular control |
| `networks` | Network Policies | Default: all pods in namespace can communicate |

## ?? What Doesn't Work in Kubernetes

### 1. `depends_on` - NO NATIVE SUPPORT

**Docker Compose:**
```yaml
coursesapi:
  depends_on:
    sqldata:
      condition: service_healthy
    rabbitmq:
      condition: service_healthy
```

**Kubernetes Solution - Use Init Containers:**
```yaml
initContainers:
- name: wait-for-sqldata
  image: busybox:1.36
  command: ['sh', '-c', 'until nc -z sqldata 1433; do echo waiting; sleep 2; done;']
- name: wait-for-rabbitmq
  image: busybox:1.36
  command: ['sh', '-c', 'until nc -z rabbitmq 5672; do echo waiting; sleep 2; done;']
```

**Why Init Containers?**
- ? Run before main container starts
- ? Must complete successfully
- ? Simple dependency waiting
- ? No code changes needed

### 2. Container Names - USE SERVICE NAMES

**Docker Compose:**
```yaml
redis:
  container_name: redis  # Direct reference
```

**Kubernetes:**
```yaml
# In connection strings, use service name
REDIS_CONNECTION: "redis:6379"  # Not container ID
```

## ? What You Already Have (No Changes Needed!)

### 1. Health Check Endpoints ?
Your APIs already implement:
- `/health` - Full health check
- `/liveness` - Simple alive check

Kubernetes can use these directly!

### 2. Polly Resilience Policies ?
Your existing retry logic handles startup race conditions:
- **Retry Policy**: 3 attempts with exponential backoff
- **Circuit Breaker**: Opens after 5 failures  
- **Timeout Policy**: 10 seconds per request

**This is BETTER than `depends_on`!** Even if a service starts before its dependencies are ready, Polly will retry automatically.

### 3. Connection String Configuration ?
You already use environment variables:
```csharp
builder.Configuration.GetConnectionString("Redis") ?? 
Environment.GetEnvironmentVariable("Redis__ConnectionString")
```

Perfect for Kubernetes ConfigMaps/Secrets!

## ?? Configuration Changes Needed

### Docker Compose Connection Strings:
```yaml
environment:
  - ConnectionStrings__StudentsAPIContext=Server=sqldata;Database=...
```

### Kubernetes ConfigMap/Secret:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: university-secrets
stringData:
  SQLSERVER_CONNECTION: "Server=sqldata;Database=..."
```

Then inject into pod:
```yaml
env:
- name: ConnectionStrings__StudentsAPIContext
  valueFrom:
    secretKeyRef:
      name: university-secrets
      key: SQLSERVER_CONNECTION
```

## ?? Service Hostname Changes

| Docker Compose | Kubernetes (short) | Kubernetes (FQDN) |
|----------------|-------------------|-------------------|
| `rabbitmq` | `rabbitmq` | `rabbitmq.university.svc.cluster.local` |
| `redis` | `redis` | `redis.university.svc.cluster.local` |
| `sqldata` | `sqldata` | `sqldata.university.svc.cluster.local` |
| `studentsapi` | `studentsapi` | `studentsapi.university.svc.cluster.local` |
| `coursesapi` | `coursesapi` | `coursesapi.university.svc.cluster.local` |

**Good News:** Short names work within the same namespace! No changes needed to your connection strings.

## ?? Startup Order Strategy

### Docker Compose Approach:
```yaml
depends_on:
  sqldata:
    condition: service_healthy
```
? Not available in Kubernetes

### Kubernetes Approach - Multi-Layer Defense:

#### Layer 1: Init Containers (Startup Wait)
```yaml
initContainers:
- name: wait-for-sqldata
  command: ['sh', '-c', 'until nc -z sqldata 1433; do sleep 2; done;']
```
? Ensures service is reachable before starting

#### Layer 2: Readiness Probes (Traffic Control)
```yaml
readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 15
```
? Service only receives traffic when ready

#### Layer 3: Liveness Probes (Recovery)
```yaml
livenessProbe:
  httpGet:
    path: /liveness
    port: 8080
  initialDelaySeconds: 30
```
? Restarts pod if it becomes unhealthy

#### Layer 4: Application Retry (Polly)
```csharp
.WaitAndRetryAsync(
    retryCount: 3,
    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
)
```
? Handles temporary failures automatically

## ?? Volume Mappings

### Docker Compose:
```yaml
volumes:
  - sqldata-volume:/var/opt/mssql
```

### Kubernetes:
```yaml
# PersistentVolumeClaim
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: sqldata-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 5Gi

# Reference in Deployment
volumeMounts:
- name: sqldata-storage
  mountPath: /var/opt/mssql
volumes:
- name: sqldata-storage
  persistentVolumeClaim:
    claimName: sqldata-pvc
```

## ?? Port Mappings

### Docker Compose:
```yaml
ports:
  - "5001:80"  # Host:Container
```

### Kubernetes Service:
```yaml
apiVersion: v1
kind: Service
spec:
  ports:
  - port: 80        # Service port
    targetPort: 8080 # Container port
```

Access via:
- **ClusterIP** (default): Internal only
- **LoadBalancer**: External access with cloud LB
- **NodePort**: External access via node IP
- **Ingress**: HTTP(S) routing with domain names

## ?? Secrets Management

### Docker Compose:
```yaml
environment:
  - SA_PASSWORD=My!P@ssword1
```
?? Plaintext in file

### Kubernetes:
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: university-secrets
type: Opaque
stringData:
  SA_PASSWORD: "My!P@ssword1"
```
? Base64 encoded, RBAC controlled

## ?? Deployment Commands

### Docker Compose:
```bash
docker-compose up -d
docker-compose down
docker-compose logs -f
```

### Kubernetes:
```bash
kubectl apply -f k8s/
kubectl delete namespace university
kubectl logs -f deployment/studentsapi -n university
```

## ?? Monitoring

### Docker Compose:
```bash
docker-compose ps
docker stats
```

### Kubernetes:
```bash
kubectl get pods -n university
kubectl top pods -n university
kubectl describe pod <name> -n university
```

## ?? Scaling

### Docker Compose:
```yaml
deploy:
  replicas: 3
```

### Kubernetes:
```yaml
spec:
  replicas: 3
```

```bash
# Dynamic scaling
kubectl scale deployment studentsapi --replicas=5 -n university

# Auto-scaling
kubectl autoscale deployment studentsapi --min=2 --max=10 --cpu-percent=70 -n university
```

## ?? Migration Checklist

- [x] Create Kubernetes manifests for all services
- [x] Replace `depends_on` with init containers
- [x] Configure health check endpoints (already done!)
- [x] Set up ConfigMaps and Secrets
- [x] Define PersistentVolumeClaims for stateful services
- [x] Add readiness and liveness probes
- [x] Configure service discovery (DNS names)
- [x] Set resource limits (CPU, memory)
- [ ] Build and tag Docker images
- [ ] Push images to registry (if using remote cluster)
- [ ] Deploy to cluster
- [ ] Test application functionality
- [ ] Monitor logs and metrics

## ?? Why This Works Better

1. **No Race Conditions**: Init containers + readiness probes + Polly retry = bulletproof startup
2. **Self-Healing**: Liveness probes automatically restart failed pods
3. **Scalability**: Easy horizontal scaling with replicas
4. **High Availability**: Multiple replicas across nodes
5. **Service Discovery**: Built-in DNS for service names
6. **Rolling Updates**: Zero-downtime deployments
7. **Resource Management**: CPU/memory limits and requests

## ?? Additional Resources

- [Kubernetes Init Containers](https://kubernetes.io/docs/concepts/workloads/pods/init-containers/)
- [Configure Liveness, Readiness Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Polly Resilience Patterns](https://github.com/App-vNext/Polly)
