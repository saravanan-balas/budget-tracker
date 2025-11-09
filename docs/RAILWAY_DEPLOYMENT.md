# Railway Deployment Guide

This guide covers deploying the Budget Tracker to Railway with CSV-only support (simplified architecture).

## Architecture Changes

- **Worker Service Removed**: CSV processing is now handled synchronously in the API
- **PDF/Image Processing**: Temporarily disabled (will be added back later)
- **External Cache Removed**: No Redis dependency—the API relies on in-memory caching
- **Result**: Lower hosting costs (~$30-35/month instead of $60+)

## Railway Setup

### 1. Create Railway Account
1. Sign up at [railway.app](https://railway.app)
2. Choose the Hobby plan ($5/month with $5 credit)

### 2. Create New Project
```bash
railway login
railway init
```

### 3. Add PostgreSQL Database
```bash
railway add postgresql
```

This will provision a PostgreSQL instance with pgvector extension.

### 4. Deploy Services

#### Deploy API Service
```bash
# From project root
railway up --service api

# Set environment variables
railway variables set OPENAI_API_KEY="your-openai-key"
railway variables set JWT_KEY="your-secure-jwt-key"
railway variables set JWT_ISSUER="BudgetTrackerAPI"
railway variables set JWT_AUDIENCE="BudgetTrackerClient"
```

#### Deploy Frontend
```bash
# Deploy frontend
railway up --service frontend

# Set API URL
railway variables set NUXT_PUBLIC_API_BASE_URL="https://your-api.railway.app"
```

### 5. Database Migrations
```bash
# Connect to Railway PostgreSQL
railway run dotnet ef database update --project api/BudgetTracker.API
```

## Environment Variables

### API Service
- `DATABASE_URL`: (Automatically set by Railway)
- `OPENAI_API_KEY`: Your OpenAI API key
- `JWT_KEY`: Secure JWT signing key
- `PORT`: (Automatically set by Railway)

### Frontend Service
- `NUXT_PUBLIC_API_BASE_URL`: Your API URL
- `PORT`: 3000

## Cost Breakdown

### Estimated Monthly Costs (Railway Hobby Plan)
- **PostgreSQL**: ~$15-20/month (1GB RAM, 0.5 vCPU)
- **API Service**: ~$10-15/month (1GB RAM, 0.5 vCPU)
- **Frontend**: ~$5-10/month (0.5GB RAM, 0.25 vCPU)
- **Total**: ~$30-45/month

### Cost Optimization Tips
1. Start with minimal resources and scale up as needed
2. Use Railway's sleep mode for development environments
3. Monitor usage through Railway dashboard

## Limitations

### Current Limitations (CSV-only)
- ✅ CSV file import (synchronous processing)
- ❌ PDF import (disabled)
- ❌ Image/screenshot import (disabled)
- ✅ Transaction categorization
- ✅ Merchant normalization
- ✅ Recurring transaction detection

### File Size Limits
- CSV files: Maximum 10MB recommended
- Processing timeout: 30 seconds
- For larger files, consider splitting into multiple uploads

## Monitoring

### View Logs
```bash
railway logs --service api
railway logs --service frontend
```

### Check Service Status
```bash
railway status
```

## Rollback Plan

If you need to revert to the full architecture with worker service:

1. Uncomment worker service in docker-compose.yml
2. Deploy worker service to Railway
3. Update ImportController to use async processing

## Next Steps

1. Deploy to Railway following this guide
2. Test CSV imports thoroughly
3. Monitor performance and costs
4. Plan for adding PDF/image support when needed

## Support

For issues or questions:
- Railway documentation: [docs.railway.app](https://docs.railway.app)
- Budget Tracker issues: Create issue in GitHub repository