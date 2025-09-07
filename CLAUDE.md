# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI-First Budget Tracker is an intelligent personal finance management system with AI-powered insights, automated transaction categorization, and conversational analytics. The system consists of multiple microservices built with .NET 8 and uses PostgreSQL with vector extensions for semantic search capabilities.

## Commands

### Development Commands

**Start all services with Docker:**
```bash
docker-compose up -d
```

**API Development:**
```bash
cd api/BudgetTracker.API
dotnet watch run
# API available at: http://localhost:5000/swagger
```

**Worker Development:**
```bash
cd worker/BudgetTracker.Worker
dotnet watch run
```

**UI Development:**
```bash
cd ui/BudgetTracker.UI
dotnet watch run
# UI available at: http://localhost:3000
```

**Database Operations:**
```bash
cd api/BudgetTracker.API
dotnet ef migrations add MigrationName
dotnet ef database update
```

**Build Solution:**
```bash
dotnet build budget-tracker.sln
```

**Test Commands:**
```bash
dotnet test
```

### Environment Setup

1. Copy environment file: `cp .env.example .env`
2. Start PostgreSQL: `docker-compose up -d postgres`
3. Apply migrations: `cd api/BudgetTracker.API && dotnet ef database update`
4. Start services individually or with `docker-compose up -d`

## Architecture

### Service Architecture
- **API** (.NET 8 Web API): RESTful endpoints, JWT authentication, real-time chat API, Swagger documentation
- **Worker** (.NET 8 Background Service): CSV/PDF import processing, recurring transaction detection, scheduled maintenance tasks
- **Common** (.NET 8 Class Library): Shared domain models, DTOs, repository interfaces, Azure Blob Storage service, Entity Framework context
- **UI** (.NET 8 Web App): Server-side rendered frontend with Chart.js visualizations

### Key Technologies
- **Database**: PostgreSQL with pgvector extension for semantic search
- **Cache**: Redis for session management and caching
- **Storage**: Azure Blob Storage for file uploads
- **AI/ML**: OpenAI GPT-4 integration, embeddings for merchant normalization
- **Auth**: JWT-based authentication with BCrypt password hashing

### Domain Model Structure
- **Transaction**: Core entity with merchant normalization, categorization, recurring detection, transfer matching, and split transaction support
- **Account**: User financial accounts (checking, savings, credit cards)
- **Merchant**: Normalized merchant data with embeddings for semantic matching
- **Category**: Hierarchical categorization system with budgeting
- **User**: User management with multi-tenant row-level security
- **RecurringSeries**: Automatic detection and tracking of recurring transactions
- **ImportedFile**: Track CSV/PDF imports with processing status

### AI Integration Points
- **Conversational Analytics**: Natural language queries converted to SQL via function calls
- **Transaction Categorization**: Rule-based → ML embeddings → LLM fallback pipeline  
- **Merchant Normalization**: Semantic matching using pgvector embeddings
- **Anomaly Detection**: Statistical analysis with AI-generated explanations
- **Financial Insights**: Monthly recaps and "what-if" scenario analysis

### Data Pipeline
1. **File Import**: CSV dialect detection, PDF text extraction with OCR fallback
2. **Normalization**: Date parsing, amount standardization, merchant cleaning
3. **Deduplication**: Hash-based duplicate detection across amount/merchant/date
4. **Categorization**: Multi-tier classification system (rules → embeddings → LLM)
5. **Post-processing**: Transfer detection, recurring pattern analysis

### Database Considerations
- Uses Entity Framework Core with PostgreSQL provider
- pgvector extension enabled for semantic search
- Row-level security implemented for multi-tenant isolation
- Audit logging for all financial data changes
- Soft deletes for regulatory compliance

### Security Implementation
- JWT tokens with configurable expiration
- BCrypt password hashing
- Row-level security enforced at database level
- API rate limiting and input validation
- Encrypted storage for sensitive financial data

## Environment Variables
Key configuration in `.env`:
- `DATABASE_URL`: PostgreSQL connection string
- `OPENAI_API_KEY`: Required for AI features
- `AZURE_STORAGE_CONNECTION_STRING`: File upload storage
- `JWT_KEY`: Token signing key
- `REDIS_CONNECTION`: Cache connection string