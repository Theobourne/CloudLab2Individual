# Quick Start - Deploy to Kubernetes

## TL;DR - 3 Commands to Deploy Everything

```bash
# 1. Build Docker images
docker build -f StudentsAPI/Dockerfile -t studentsapi:latest .
docker build -f CoursesAPI/Dockerfile -t coursesapi:latest .
docker build -f UniversityWeb/Dockerfile -t universityweb:latest .
docker build -f ApiGateway/Dockerfile -t apigateway:latest .

# 2. Deploy to Kubernetes (Linux/Mac)
chmod +x k8s/deploy.sh
./k8s/deploy.sh

# OR for Windows
k8s\deploy.bat

# 3. Access the application
kubectl port-forward svc/universityweb 8080:80 -n university
# Open: http://localhost:8080
```

## What's Different from Docker Compose?

### The Main Issue: `depends_on` Doesn't Exist in Kubernetes

**Your docker-compose.yml has:**
```yaml
coursesapi:
  depends_on:
    sqldata:
      condition: service_healthy
    rabbitmq:
      condition: service_healthy
    redis:
      condition: service_healthy
```

**In Kubernetes, we replaced it with:**
1. **Init Containers** - Wait for services to be reachable
2. **Readiness Probes** - Don't send traffic until ready
3. **Your Polly Policies** - Retry if dependencies aren't ready yet!

## Your Application is Already Kubernetes-Ready! ??

You don't need to change ANY code because you already have:

? **Health Check Endpoints** (`/health`, `/liveness`)  
? **Polly Retry Policies** (handles startup races)  
? **Environment Variable Configuration** (works with ConfigMaps)  
? **Distributed Caching** (Redis)  
? **Structured Logging** (Seq)  

## Manual Deployment (Step by Step)

### 1. Build Images
```bash
cd <your-project-directory>

docker build -f StudentsAPI/Dockerfile -t studentsapi:latest .
docker build -f CoursesAPI/Dockerfile -t coursesapi:latest .
docker build -f UniversityWeb/Dockerfile -t universityweb:latest .
docker build -f ApiGateway/Dockerfile -t apigateway:latest .
```

### 2. Deploy Infrastructure First
```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/sqldata.yaml
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/rabbitmq.yaml
kubectl apply -f k8s/seq.yaml
```

### 3. Wait for Infrastructure (Optional but Recommended)
```bash
kubectl wait --for=condition=ready pod -l app=sqldata -n university --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n university --timeout=120s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n university --timeout=180s
```

### 4. Deploy Applications
```bash
kubectl apply -f k8s/studentsapi.yaml
kubectl apply -f k8s/coursesapi.yaml
kubectl apply -f k8s/universityweb.yaml
kubectl apply -f k8s/apigateway.yaml
```

### 5. Check Status
```bash
kubectl get pods -n university
kubectl get svc -n university
```

## Accessing Your Application

### Option 1: Port Forwarding (Local Development)
```bash
# Frontend
kubectl port-forward svc/universityweb 8080:80 -n university

# APIs
kubectl port-forward svc/studentsapi 5001:80 -n university
kubectl port-forward svc/coursesapi 5002:80 -n university

# Seq Logs
kubectl port-forward svc/seq-external 5341:5341 -n university

# RabbitMQ Management
kubectl port-forward svc/rabbitmq-management 15672:15672 -n university
```

### Option 2: LoadBalancer (Cloud/Docker Desktop)
```bash
# Get external IP
kubectl get svc universityweb -n university

# If using Docker Desktop, it will be localhost:port
```

## Testing

### Health Checks
```bash
# Port forward API
kubectl port-forward svc/studentsapi 5001:80 -n university

# In another terminal
curl http://localhost:5001/health
curl http://localhost:5001/liveness
```

### View Logs
```bash
# All StudentsAPI pods
kubectl logs -f deployment/studentsapi -n university

# Specific pod
kubectl logs -f <pod-name> -n university

# Previous crashed pod
kubectl logs --previous <pod-name> -n university
```

### Database Connection
```bash
# Exec into SQL Server
kubectl exec -it deployment/sqldata -n university -- /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'My!P@ssword1'

# Run query
SELECT name FROM sys.databases;
GO
```

## Troubleshooting

### Pods Stuck in "Init:0/3"
**Cause:** Init containers waiting for dependencies

**Solution:** Check if infrastructure is running
```bash
kubectl get pods -n university
kubectl logs <pod-name> -c wait-for-sqldata -n university
```

### Pods CrashLoopBackOff
**Cause:** Application failing to start

**Solution:** Check application logs
```bash
kubectl logs <pod-name> -n university
kubectl describe pod <pod-name> -n university
```

### "Connection refused" Errors
**Cause:** Service not ready or wrong service name

**Solution:** 
1. Check service exists: `kubectl get svc -n university`
2. Check service is ready: `kubectl get endpoints -n university`
3. Verify connection string uses correct service name

### Images Not Found
**Cause:** Images not available to cluster

**For local clusters (Docker Desktop/Minikube):**
```bash
# Make sure images are in local Docker
docker images | grep -E "studentsapi|coursesapi|universityweb"

# For Minikube, load images
minikube image load studentsapi:latest
minikube image load coursesapi:latest
minikube image load universityweb:latest
minikube image load apigateway:latest
```

**For cloud clusters:**
```bash
# Tag for registry
docker tag studentsapi:latest your-registry/studentsapi:latest

# Push to registry
docker push your-registry/studentsapi:latest

# Update k8s manifests to use registry path
# image: your-registry/studentsapi:latest
```

## Scaling

```bash
# Scale up
kubectl scale deployment studentsapi --replicas=3 -n university

# Auto-scale
kubectl autoscale deployment studentsapi --min=2 --max=10 --cpu-percent=70 -n university

# Check scaling
kubectl get hpa -n university
```

## Cleanup

```bash
# Delete everything
kubectl delete namespace university

# OR delete individual resources
kubectl delete -f k8s/
```

## What Got Deployed?

| Component | Replicas | Type | Purpose |
|-----------|----------|------|---------|
| SQL Server | 1 | StatefulSet | Database |
| Redis | 1 | StatefulSet | Caching |
| RabbitMQ | 1 | StatefulSet | Message Queue |
| Seq | 1 | StatefulSet | Logging |
| StudentsAPI | 2 | Deployment | API Service |
| CoursesAPI | 2 | Deployment | API Service |
| UniversityWeb | 2 | Deployment | Frontend |
| ApiGateway | 2 | Deployment | Gateway |

## Key Configuration Files

```
k8s/
??? namespace.yaml              # Creates 'university' namespace
??? secrets.yaml                # Passwords, connection strings
??? configmap.yaml              # Non-sensitive configuration
??? sqldata.yaml                # SQL Server + PVC
??? redis.yaml                  # Redis + PVC
??? rabbitmq.yaml               # RabbitMQ + PVC
??? seq.yaml                    # Seq + PVC
??? studentsapi.yaml            # StudentsAPI deployment + service
??? coursesapi.yaml             # CoursesAPI deployment + service
??? universityweb.yaml          # UniversityWeb deployment + service
??? apigateway.yaml             # ApiGateway deployment + service
??? deploy.sh                   # Deployment script (Linux/Mac)
??? deploy.bat                  # Deployment script (Windows)
??? KUBERNETES_DEPLOYMENT_GUIDE.md
??? DOCKER_COMPOSE_VS_KUBERNETES.md
```

## Next Steps

1. ? **Basic Deployment** - You're here!
2. ?? **Add Ingress** - Better routing and SSL/TLS
3. ?? **Add Monitoring** - Prometheus + Grafana
4. ?? **Secure Secrets** - Use sealed secrets or vault
5. ?? **CI/CD Pipeline** - Automate deployments
6. ?? **Multi-Environment** - Dev, staging, production

## Questions?

- Init containers not working? ? Check `kubectl logs <pod> -c <init-container-name>`
- Service not reachable? ? Check `kubectl get endpoints`
- Health checks failing? ? Check `kubectl describe pod <pod-name>`
- Need more replicas? ? Use `kubectl scale`

**Remember:** Your Polly policies will handle most startup issues automatically! The retry logic gives services time to become available.
