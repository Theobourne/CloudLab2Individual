@echo off
REM Kubernetes Deployment Script for University Application (Windows)
REM This script deploys the entire application to Kubernetes

setlocal enabledelayedexpansion

set NAMESPACE=university
set TIMEOUT=300s

echo ==========================================
echo University Application - Kubernetes Deploy
echo ==========================================
echo.

REM Check if kubectl is available
kubectl version --client >nul 2>&1
if errorlevel 1 (
    echo [ERROR] kubectl is not installed. Please install kubectl first.
    exit /b 1
)
echo [OK] kubectl is available

REM Check if cluster is accessible
kubectl cluster-info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Cannot connect to Kubernetes cluster. Please check your kubeconfig.
    exit /b 1
)
echo [OK] Connected to Kubernetes cluster
echo.

REM Step 1: Create namespace
echo Step 1: Creating namespace '%NAMESPACE%'...
kubectl apply -f k8s/namespace.yaml
if errorlevel 1 (
    echo [ERROR] Failed to create namespace
    exit /b 1
)
echo [OK] Namespace created
echo.

REM Step 2: Apply configuration
echo Step 2: Applying configuration...
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
if errorlevel 1 (
    echo [ERROR] Failed to apply configuration
    exit /b 1
)
echo [OK] Configuration applied
echo.

REM Step 3: Deploy infrastructure
echo Step 3: Deploying infrastructure services...
echo   - SQL Server
kubectl apply -f k8s/sqldata.yaml
echo   - Redis
kubectl apply -f k8s/redis.yaml
echo   - RabbitMQ
kubectl apply -f k8s/rabbitmq.yaml
echo   - Seq
kubectl apply -f k8s/seq.yaml
echo [OK] Infrastructure services deployed
echo.

REM Step 4: Wait for infrastructure
echo Step 4: Waiting for infrastructure to be ready...
echo   This may take a few minutes...

echo   Waiting for SQL Server...
kubectl wait --for=condition=ready pod -l app=sqldata -n %NAMESPACE% --timeout=%TIMEOUT% >nul 2>&1
if errorlevel 1 (
    echo [WARNING] SQL Server readiness check timed out, but continuing...
) else (
    echo [OK] SQL Server is ready
)

echo   Waiting for Redis...
kubectl wait --for=condition=ready pod -l app=redis -n %NAMESPACE% --timeout=120s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] Redis readiness check timed out, but continuing...
) else (
    echo [OK] Redis is ready
)

echo   Waiting for RabbitMQ...
kubectl wait --for=condition=ready pod -l app=rabbitmq -n %NAMESPACE% --timeout=180s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] RabbitMQ readiness check timed out, but continuing...
) else (
    echo [OK] RabbitMQ is ready
)

echo   Waiting for Seq...
kubectl wait --for=condition=ready pod -l app=seq -n %NAMESPACE% --timeout=120s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] Seq readiness check timed out, but continuing...
) else (
    echo [OK] Seq is ready
)
echo.

REM Step 5: Deploy APIs
echo Step 5: Deploying API services...
echo   - StudentsAPI
kubectl apply -f k8s/studentsapi.yaml
echo   - CoursesAPI
kubectl apply -f k8s/coursesapi.yaml
echo [OK] API services deployed
echo.

REM Step 6: Wait for APIs
echo Step 6: Waiting for APIs to be ready...

echo   Waiting for StudentsAPI...
kubectl wait --for=condition=ready pod -l app=studentsapi -n %NAMESPACE% --timeout=180s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] StudentsAPI readiness check timed out, but continuing...
) else (
    echo [OK] StudentsAPI is ready
)

echo   Waiting for CoursesAPI...
kubectl wait --for=condition=ready pod -l app=coursesapi -n %NAMESPACE% --timeout=180s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] CoursesAPI readiness check timed out, but continuing...
) else (
    echo [OK] CoursesAPI is ready
)
echo.

REM Step 7: Deploy frontend and gateway
echo Step 7: Deploying frontend and gateway...
echo   - UniversityWeb
kubectl apply -f k8s/universityweb.yaml
echo   - ApiGateway
kubectl apply -f k8s/apigateway.yaml
echo [OK] Frontend and gateway deployed
echo.

REM Step 8: Wait for frontend
echo Step 8: Waiting for frontend to be ready...
kubectl wait --for=condition=ready pod -l app=universityweb -n %NAMESPACE% --timeout=180s >nul 2>&1
if errorlevel 1 (
    echo [WARNING] UniversityWeb readiness check timed out, but continuing...
) else (
    echo [OK] UniversityWeb is ready
)
echo.

REM Summary
echo ==========================================
echo Deployment Summary
echo ==========================================
echo.
echo [OK] All resources deployed!
echo.

echo Checking pod status...
kubectl get pods -n %NAMESPACE%
echo.

echo Checking services...
kubectl get svc -n %NAMESPACE%
echo.

REM Access information
echo ==========================================
echo Access Information
echo ==========================================
echo.

echo To access UniversityWeb:
echo   kubectl port-forward svc/universityweb 8080:80 -n %NAMESPACE%
echo   Then open: http://localhost:8080
echo.

echo To access Seq logs:
echo   kubectl port-forward svc/seq-external 5341:5341 -n %NAMESPACE%
echo   Then open: http://localhost:5341
echo.

echo To access RabbitMQ management:
echo   kubectl port-forward svc/rabbitmq-management 15672:15672 -n %NAMESPACE%
echo   Then open: http://localhost:15672 (guest/guest)
echo.

echo ==========================================
echo Useful Commands
echo ==========================================
echo.
echo View logs:
echo   kubectl logs -f deployment/studentsapi -n %NAMESPACE%
echo   kubectl logs -f deployment/coursesapi -n %NAMESPACE%
echo   kubectl logs -f deployment/universityweb -n %NAMESPACE%
echo.
echo Check health:
echo   kubectl port-forward svc/studentsapi 5001:80 -n %NAMESPACE%
echo   curl http://localhost:5001/health
echo.
echo Scale deployments:
echo   kubectl scale deployment studentsapi --replicas=3 -n %NAMESPACE%
echo.
echo Delete everything:
echo   kubectl delete namespace %NAMESPACE%
echo.

echo [OK] Deployment completed!
pause
