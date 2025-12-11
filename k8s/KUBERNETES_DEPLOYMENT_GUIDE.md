# Kubernetes Deployment Guide for University Application

## Overview

This guide explains how to deploy the University microservices application to Kubernetes. The application has been adapted from Docker Compose to work in Kubernetes without the `depends_on` directive.

## Key Differences from Docker Compose

### 1. **No `depends_on` - Replaced with:**
   - **Init Containers**: Wait for dependent services to be reachable
   - **Readiness Probes**: Ensure services are ready before receiving traffic
   - **Liveness Probes**: Restart unhealthy containers
   - **Application Resilience**: Polly policies (retry, circuit breaker) handle temporary failures

### 2. **Service Discovery**
   - Docker Compose uses container names: `rabbitmq`, `redis`, `sqldata`
   - Kubernetes uses service names within namespace: `rabbitmq.university.svc.cluster.local` (short form: `rabbitmq`)

### 3. **Configuration Management**
   - ConfigMaps for non-sensitive data
   - Secrets for passwords and connection strings
   - Environment variables injected from ConfigMaps/Secrets

## Architecture

```
???????????????????????????????????????????????????????????????
?                      Kubernetes Cluster                      ?
?                                                               ?
?  ???????????????????????????????????????????????????????   ?
?  ?                    Namespace: university             ?   ?
?  ?                                                       ?   ?
?  ?  ????????????????      ????????????????            ?   ?
?  ?  ? UniversityWeb???????? LoadBalancer ? (External) ?   ?
?  ?  ?  (2 replicas)?      ????????????????            ?   ?
?  ?  ????????????????                                   ?   ?
?  ?          ?                                           ?   ?
?  ?          ??????????????????????????                 ?   ?
?  ?          ?          ?             ?                 ?   ?
?  ?   ????????????? ?????????????? ??????????????     ?   ?
?  ?   ?StudentsAPI? ?CoursesAPI  ? ?ApiGateway  ?     ?   ?
?  ?   ?(2 replicas)? ?(2 replicas)? ?(2 replicas)?     ?   ?
?  ?   ?????????????? ?????????????? ??????????????     ?   ?
?  ?         ?              ?                            ?   ?
?  ?         ????????????????                            ?   ?
?  ?                ?                                     ?   ?
?  ?    ?????????????????????????????????????????       ?   ?
?  ?    ?           ?                ?          ?       ?   ?
?  ? ???????  ???????????  ??????????????  ????????   ?   ?
?  ? ?Redis?  ?RabbitMQ ?  ?  SQL Server?  ? Seq  ?   ?   ?
?  ? ?     ?  ?         ?  ?            ?  ?      ?   ?   ?
?  ? ???????  ???????????  ??????????????  ????????   ?   ?
?  ?  (PVC)      (PVC)          (PVC)        (PVC)     ?   ?
?  ?????????????????????????????????????????????????????   ?
?????????????????????????????????????????????????????????????
```

## Prerequisites

1. **Kubernetes Cluster** (one of):
   - Local: Docker Desktop, Minikube, or Kind
   - Cloud: AKS, EKS, GKE
   
2. **kubectl** installed and configured

3. **Docker images built** for your application:
   ```bash
   docker build -f StudentsAPI/Dockerfile -t studentsapi:latest .
   docker build -f CoursesAPI/Dockerfile -t coursesapi:latest .
   docker build -f UniversityWeb/Dockerfile -t universityweb:latest .
   docker build -f ApiGateway/Dockerfile -t apigateway:latest .
   ```

## Deployment Steps

### 1. Create Namespace and Configuration
```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Apply secrets (passwords, connection strings)
kubectl apply -f k8s/secrets.yaml

# Apply configuration
kubectl apply -f k8s/configmap.yaml
```

### 2. Deploy Infrastructure Services
```bash
# Deploy in order (infrastructure first)
kubectl apply -f k8s/sqldata.yaml
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/rabbitmq.yaml
kubectl apply -f k8s/seq.yaml

# Wait for infrastructure to be ready
kubectl wait --for=condition=ready pod -l app=sqldata -n university --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n university --timeout=120s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n university --timeout=180s
kubectl wait --for=condition=ready pod -l app=seq -n university --timeout=120s
```

### 3. Deploy Application Services
```bash
# Deploy APIs
kubectl apply -f k8s/studentsapi.yaml
kubectl apply -f k8s/coursesapi.yaml

# Wait for APIs to be ready
kubectl wait --for=condition=ready pod -l app=studentsapi -n university --timeout=180s
kubectl wait --for=condition=ready pod -l app=coursesapi -n university --timeout=180s

# Deploy frontend and gateway
kubectl apply -f k8s/universityweb.yaml
kubectl apply -f k8s/apigateway.yaml
```

### 4. Quick Deploy All (Alternative)
```bash
# Deploy everything at once
kubectl apply -f k8s/

# Note: Init containers will wait for dependencies automatically
```

## Verification

### Check Pod Status
```bash
kubectl get pods -n university
```

Expected output:
```
NAME                            READY   STATUS    RESTARTS   AGE
coursesapi-xxxxx                1/1     Running   0          2m
studentsapi-xxxxx               1/1     Running   0          2m
universityweb-xxxxx             1/1     Running   0          1m
apigateway-xxxxx                1/1     Running   0          1m
sqldata-xxxxx                   1/1     Running   0          5m
redis-xxxxx                     1/1     Running   0          5m
rabbitmq-xxxxx                  1/1     Running   0          5m
seq-xxxxx                       1/1     Running   0          5m
```

### Check Services
```bash
kubectl get svc -n university
```

### Check Logs
```bash
# Check API logs
kubectl logs -f deployment/studentsapi -n university
kubectl logs -f deployment/coursesapi -n university

# Check web logs
kubectl logs -f deployment/universityweb -n university
```

### Test Health Endpoints
```bash
# Port forward to test locally
kubectl port-forward svc/studentsapi 5001:80 -n university
kubectl port-forward svc/coursesapi 5002:80 -n university

# Test health
curl http://localhost:5001/health
curl http://localhost:5002/health
```

## Accessing the Application

### UniversityWeb (Frontend)
```bash
# Get the LoadBalancer IP/hostname
kubectl get svc universityweb -n university

# Or port-forward for local access
kubectl port-forward svc/universityweb 8080:80 -n university
# Access at http://localhost:8080
```

### Seq Logging Dashboard
```bash
# Port-forward to access Seq UI
kubectl port-forward svc/seq-external 5341:5341 -n university
# Access at http://localhost:5341
```

### RabbitMQ Management UI
```bash
# Port-forward to access RabbitMQ management
kubectl port-forward svc/rabbitmq-management 15672:15672 -n university
# Access at http://localhost:15672
# Default credentials: guest/guest
```

## How Dependencies Are Handled

### 1. **Init Containers**
Each dependent service has init containers that wait for dependencies:

```yaml
initContainers:
- name: wait-for-sqldata
  image: busybox:1.36
  command: ['sh', '-c', 'until nc -z sqldata 1433; do echo waiting for sqldata; sleep 2; done;']
```

### 2. **Readiness Probes**
Services only receive traffic when ready:

```yaml
readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 15
  periodSeconds: 5
```

### 3. **Liveness Probes**
Unhealthy containers are automatically restarted:

```yaml
livenessProbe:
  httpGet:
    path: /liveness
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
```

### 4. **Application Resilience (Your Polly Policies)**
Your APIs already have retry and circuit breaker patterns:
- **Retry**: 3 attempts with exponential backoff
- **Circuit Breaker**: Opens after 5 failures
- **Timeout**: 10 seconds per request

This means even if a dependency is temporarily unavailable, your services will retry automatically!

## Scaling

### Manual Scaling
```bash
# Scale APIs
kubectl scale deployment studentsapi --replicas=3 -n university
kubectl scale deployment coursesapi --replicas=3 -n university

# Scale frontend
kubectl scale deployment universityweb --replicas=3 -n university
```

### Auto-scaling (HPA)
```bash
# Create horizontal pod autoscaler
kubectl autoscale deployment studentsapi \
  --cpu-percent=70 \
  --min=2 \
  --max=10 \
  -n university
```

## Storage

All stateful services use PersistentVolumeClaims (PVCs):
- **sqldata-pvc**: 5Gi for SQL Server data
- **redis-pvc**: 1Gi for Redis data
- **rabbitmq-pvc**: 2Gi for RabbitMQ data
- **seq-pvc**: 2Gi for Seq logs

```bash
# Check PVCs
kubectl get pvc -n university
```

## Troubleshooting

### Pods Not Starting
```bash
# Describe pod to see events
kubectl describe pod <pod-name> -n university

# Check init container logs
kubectl logs <pod-name> -c wait-for-sqldata -n university
```

### Database Connection Issues
```bash
# Check SQL Server logs
kubectl logs deployment/sqldata -n university

# Exec into SQL Server pod
kubectl exec -it deployment/sqldata -n university -- /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'My!P@ssword1'
```

### Redis Connection Issues
```bash
# Test Redis connectivity
kubectl exec -it deployment/redis -n university -- redis-cli ping
```

### RabbitMQ Issues
```bash
# Check RabbitMQ status
kubectl exec -it deployment/rabbitmq -n university -- rabbitmq-diagnostics status
```

## Cleanup

```bash
# Delete all resources in namespace
kubectl delete namespace university

# Or delete individual components
kubectl delete -f k8s/
```

## Migration from Docker Compose

| Docker Compose Feature | Kubernetes Equivalent |
|------------------------|----------------------|
| `depends_on` | Init containers + readiness probes |
| `container_name` | Service name (DNS) |
| `volumes` | PersistentVolumeClaims |
| `ports` | Service ports |
| `environment` | ConfigMaps + Secrets |
| `restart: unless-stopped` | Deployment replicas + liveness probes |
| `healthcheck` | Liveness + readiness probes |

## Best Practices Applied

? **Init containers** for startup dependencies  
? **Readiness probes** to prevent premature traffic  
? **Liveness probes** for automatic recovery  
? **Resource limits** to prevent resource exhaustion  
? **ConfigMaps** for configuration  
? **Secrets** for sensitive data  
? **Multiple replicas** for high availability  
? **PersistentVolumes** for stateful services  
? **Health check endpoints** (/health, /liveness)  

## Next Steps

1. **Set up ingress** for better routing and SSL/TLS
2. **Configure monitoring** with Prometheus/Grafana
3. **Set up GitOps** with ArgoCD or Flux
4. **Implement network policies** for security
5. **Add horizontal pod autoscaling** based on metrics
6. **Configure backup solutions** for persistent data

## Notes

- The application uses **Polly resilience policies**, so temporary failures during startup are automatically handled
- Health checks (`/health` and `/liveness` endpoints) are already implemented in your APIs
- Init containers provide a "wait for dependency" mechanism similar to Docker Compose `depends_on`
- All services communicate using Kubernetes DNS (service names)
