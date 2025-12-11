#!/bin/bash

# Kubernetes Deployment Script for University Application
# This script deploys the entire application to Kubernetes

set -e  # Exit on error

NAMESPACE="university"
TIMEOUT="300s"

echo "=========================================="
echo "University Application - Kubernetes Deploy"
echo "=========================================="
echo ""

# Color codes
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print colored messages
print_success() {
    echo -e "${GREEN}? $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}? $1${NC}"
}

print_error() {
    echo -e "${RED}? $1${NC}"
}

print_info() {
    echo -e "? $1"
}

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    print_error "kubectl is not installed. Please install kubectl first."
    exit 1
fi

print_success "kubectl is available"

# Check if cluster is accessible
if ! kubectl cluster-info &> /dev/null; then
    print_error "Cannot connect to Kubernetes cluster. Please check your kubeconfig."
    exit 1
fi

print_success "Connected to Kubernetes cluster"
echo ""

# Step 1: Create namespace
print_info "Step 1: Creating namespace '$NAMESPACE'..."
kubectl apply -f k8s/namespace.yaml
print_success "Namespace created"
echo ""

# Step 2: Apply configuration
print_info "Step 2: Applying configuration..."
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
print_success "Configuration applied"
echo ""

# Step 3: Deploy infrastructure
print_info "Step 3: Deploying infrastructure services..."
print_info "  - SQL Server"
kubectl apply -f k8s/sqldata.yaml
print_info "  - Redis"
kubectl apply -f k8s/redis.yaml
print_info "  - RabbitMQ"
kubectl apply -f k8s/rabbitmq.yaml
print_info "  - Seq"
kubectl apply -f k8s/seq.yaml
print_success "Infrastructure services deployed"
echo ""

# Step 4: Wait for infrastructure
print_info "Step 4: Waiting for infrastructure to be ready..."
print_info "  This may take a few minutes..."

print_info "  Waiting for SQL Server..."
if kubectl wait --for=condition=ready pod -l app=sqldata -n $NAMESPACE --timeout=$TIMEOUT 2>/dev/null; then
    print_success "SQL Server is ready"
else
    print_warning "SQL Server readiness check timed out, but continuing..."
fi

print_info "  Waiting for Redis..."
if kubectl wait --for=condition=ready pod -l app=redis -n $NAMESPACE --timeout=120s 2>/dev/null; then
    print_success "Redis is ready"
else
    print_warning "Redis readiness check timed out, but continuing..."
fi

print_info "  Waiting for RabbitMQ..."
if kubectl wait --for=condition=ready pod -l app=rabbitmq -n $NAMESPACE --timeout=180s 2>/dev/null; then
    print_success "RabbitMQ is ready"
else
    print_warning "RabbitMQ readiness check timed out, but continuing..."
fi

print_info "  Waiting for Seq..."
if kubectl wait --for=condition=ready pod -l app=seq -n $NAMESPACE --timeout=120s 2>/dev/null; then
    print_success "Seq is ready"
else
    print_warning "Seq readiness check timed out, but continuing..."
fi

echo ""

# Step 5: Deploy APIs
print_info "Step 5: Deploying API services..."
print_info "  - StudentsAPI"
kubectl apply -f k8s/studentsapi.yaml
print_info "  - CoursesAPI"
kubectl apply -f k8s/coursesapi.yaml
print_success "API services deployed"
echo ""

# Step 6: Wait for APIs
print_info "Step 6: Waiting for APIs to be ready..."

print_info "  Waiting for StudentsAPI..."
if kubectl wait --for=condition=ready pod -l app=studentsapi -n $NAMESPACE --timeout=180s 2>/dev/null; then
    print_success "StudentsAPI is ready"
else
    print_warning "StudentsAPI readiness check timed out, but continuing..."
fi

print_info "  Waiting for CoursesAPI..."
if kubectl wait --for=condition=ready pod -l app=coursesapi -n $NAMESPACE --timeout=180s 2>/dev/null; then
    print_success "CoursesAPI is ready"
else
    print_warning "CoursesAPI readiness check timed out, but continuing..."
fi

echo ""

# Step 7: Deploy frontend and gateway
print_info "Step 7: Deploying frontend and gateway..."
print_info "  - UniversityWeb"
kubectl apply -f k8s/universityweb.yaml
print_info "  - ApiGateway"
kubectl apply -f k8s/apigateway.yaml
print_success "Frontend and gateway deployed"
echo ""

# Step 8: Wait for frontend
print_info "Step 8: Waiting for frontend to be ready..."
if kubectl wait --for=condition=ready pod -l app=universityweb -n $NAMESPACE --timeout=180s 2>/dev/null; then
    print_success "UniversityWeb is ready"
else
    print_warning "UniversityWeb readiness check timed out, but continuing..."
fi
echo ""

# Summary
echo "=========================================="
echo "Deployment Summary"
echo "=========================================="
echo ""

print_success "All resources deployed!"
echo ""

print_info "Checking pod status..."
kubectl get pods -n $NAMESPACE
echo ""

print_info "Checking services..."
kubectl get svc -n $NAMESPACE
echo ""

# Get external access information
print_info "=========================================="
print_info "Access Information"
print_info "=========================================="
echo ""

# UniversityWeb
UNIVERSITYWEB_IP=$(kubectl get svc universityweb -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "pending")
UNIVERSITYWEB_HOSTNAME=$(kubectl get svc universityweb -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || echo "")

if [ "$UNIVERSITYWEB_IP" != "pending" ] && [ "$UNIVERSITYWEB_IP" != "" ]; then
    print_success "UniversityWeb: http://$UNIVERSITYWEB_IP"
elif [ "$UNIVERSITYWEB_HOSTNAME" != "" ]; then
    print_success "UniversityWeb: http://$UNIVERSITYWEB_HOSTNAME"
else
    print_warning "UniversityWeb LoadBalancer IP is pending..."
    print_info "Run: kubectl port-forward svc/universityweb 8080:80 -n $NAMESPACE"
    print_info "Then access: http://localhost:8080"
fi

echo ""
print_info "To access Seq logs:"
print_info "  kubectl port-forward svc/seq-external 5341:5341 -n $NAMESPACE"
print_info "  Then open: http://localhost:5341"
echo ""

print_info "To access RabbitMQ management:"
print_info "  kubectl port-forward svc/rabbitmq-management 15672:15672 -n $NAMESPACE"
print_info "  Then open: http://localhost:15672 (guest/guest)"
echo ""

print_info "=========================================="
print_info "Useful Commands"
print_info "=========================================="
echo ""
echo "View logs:"
echo "  kubectl logs -f deployment/studentsapi -n $NAMESPACE"
echo "  kubectl logs -f deployment/coursesapi -n $NAMESPACE"
echo "  kubectl logs -f deployment/universityweb -n $NAMESPACE"
echo ""
echo "Check health:"
echo "  kubectl port-forward svc/studentsapi 5001:80 -n $NAMESPACE"
echo "  curl http://localhost:5001/health"
echo ""
echo "Scale deployments:"
echo "  kubectl scale deployment studentsapi --replicas=3 -n $NAMESPACE"
echo ""
echo "Delete everything:"
echo "  kubectl delete namespace $NAMESPACE"
echo ""

print_success "Deployment completed!"
