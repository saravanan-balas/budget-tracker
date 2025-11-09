# Environment Configuration Guide - Container Deployment

## 🚀 Quick Setup

Set environment variables in your `.env` file (for docker-compose) or container orchestrator:

## 📋 Required Environment Variables

### Essential
- `OPENAI_API_KEY` - Your OpenAI API key for embeddings
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `JWT_KEY` - JWT signing key

### Optional (with defaults)
- `JWT_ISSUER` (default: BudgetTrackerAPI)
- `JWT_AUDIENCE` (default: BudgetTrackerClient)

## 🐳 Container Deployment Examples

### Docker Compose
```yaml
services:
  worker:
    environment:
      OPENAI_API_KEY: ${OPENAI_API_KEY}
      ConnectionStrings__DefaultConnection: Host=postgres;Database=budget_tracker;Username=postgres;Password=postgres
```

### Kubernetes
```yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: budget-tracker-worker
        env:
        - name: OPENAI_API_KEY
          valueFrom:
            secretKeyRef:
              name: openai-secret
              key: api-key
```

### Docker Run
```bash
docker run -e OPENAI_API_KEY=sk-your-key-here budget-tracker-worker
```

### Environment File (.env)
```bash
# Required
OPENAI_API_KEY=sk-your-key-here
JWT_KEY=your-secure-jwt-key

# Optional  
JWT_ISSUER=BudgetTrackerAPI
JWT_AUDIENCE=BudgetTrackerClient
```

## 🔒 Security Notes

- Use secrets management in production (Kubernetes secrets, Docker secrets, etc.)
- Never commit API keys to version control
- Rotate API keys regularly
- Use environment-specific configurations

## ✅ Verification

Both API and Worker will log at startup:
- ✅ "OPENAI_API_KEY loaded successfully (length: 51)"
- ❌ "ERROR: OPENAI_API_KEY environment variable not found!" (exits with code 1)

## 🚨 Startup Validation

The worker will **exit immediately** if required environment variables are missing, preventing misconfigured deployments.