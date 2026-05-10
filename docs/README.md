# AI-First Budget Tracker

An intelligent personal finance management system with AI-powered insights, automated transaction categorization, and conversational analytics.

## Architecture

```
budget-tracker/
├── api/                    # .NET Core Web API
├── worker/                 # Background processing service
├── common/                 # Shared models and infrastructure
├── app/                     # Nuxt/Vue frontend
├── docs/                   # Documentation
├── docker-compose.yml      # Local development setup
└── init.sql               # Database initialization
```

## Tech Stack

- **Backend**: .NET 8, C#, Entity Framework Core
- **Frontend**: Nuxt 3, Vue 3, TypeScript
- **Database**: PostgreSQL with pgvector extension
- **Caching**: In-memory (ASP.NET MemoryCache)
- **Storage**: Azure Blob Storage
- **AI/ML**: OpenAI GPT-4, embeddings for semantic search
- **Infrastructure**: Docker, Azure

## Features

### MVP Features
- CSV statement import with intelligent parsing
- Automatic transaction categorization using ML
- Duplicate detection and transfer matching
- Conversational financial analytics
- Recurring transaction detection
- Budget tracking and goals
- Month-end financial recaps

### AI Differentiators
- Natural language queries for financial insights
- Anomaly detection and explanations
- Semantic merchant normalization
- Counterfactual "what-if" scenarios
- Automated financial coaching

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- Docker & Docker Compose
- PostgreSQL (via Docker)

### Quick Start

1. Clone the repository
```bash
git clone <repository-url>
cd budget-tracker
```

2. Copy environment variables
```bash
cp .env.example .env
# Edit .env with your configuration
```

3. Start services with Docker Compose
```bash
docker-compose up -d
```

4. Apply database migrations
```bash
cd api/BudgetTracker.API
dotnet ef database update
```

5. Access the application
- Frontend: http://localhost:3000
- API: http://localhost:5157/swagger
- PostgreSQL: localhost:5432

## Development

### API Development
```bash
cd api/BudgetTracker.API
dotnet watch run
```

### Worker Development
```bash
cd worker/BudgetTracker.Worker
dotnet watch run
```

### Frontend Development
```bash
cd app
npm install
npm run dev
```

### Database Migrations
```bash
cd api/BudgetTracker.API
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Project Structure

### Common Library
- Domain models (User, Transaction, Category, etc.)
- DTOs for API communication
- Repository interfaces and implementations
- Azure Blob Storage service
- Database context

### API
- RESTful endpoints for all operations
- JWT authentication
- Real-time chat API for conversational analytics
- File import handling
- Swagger documentation

### Worker
- Background job processing
- CSV/PDF import processing
- Recurring transaction detection
- Scheduled tasks for maintenance

### UI
- Nuxt 3 with Vue 3 Composition API
- TypeScript for type safety
- Tailwind CSS for styling
- Chart.js for visualizations
- Real-time updates with WebSockets

## Security

- JWT-based authentication
- Row-level security in database
- Encrypted storage for sensitive data
- API rate limiting
- Input validation and sanitization

## Observability & Logging

The application includes configurable logging to PostgreSQL for monitoring and debugging.

### Configuration

Configure logging in `appsettings.json`:

```json
{
  "Observability": {
    "Enabled": true,
    "SamplingRate": 1.0,
    "MinimumLevel": "Warning"
  }
}
```

**Options:**
- `Enabled` (boolean): Enable/disable writing logs to PostgreSQL database
- `SamplingRate` (double, 0.0-1.0): Percentage of logs to write
  - `1.0` = log all events (100%)
  - `0.5` = log 50% of events
  - `0.1` = log 10% of events
  - Note: Errors are always logged regardless of sampling rate
- `MinimumLevel` (string): Minimum log level to write to database
  - Options: `"Information"`, `"Warning"`, `"Error"`, `"Fatal"`
  - Default: `"Warning"` (excludes Information level logs)

### Example Configurations

**Development (log everything):**
```json
{
  "Observability": {
    "Enabled": true,
    "SamplingRate": 1.0,
    "MinimumLevel": "Information"
  }
}
```

**Production (only warnings and errors):**
```json
{
  "Observability": {
    "Enabled": true,
    "SamplingRate": 1.0,
    "MinimumLevel": "Warning"
  }
}
```

**High-volume production (sampled, errors only):**
```json
{
  "Observability": {
    "Enabled": true,
    "SamplingRate": 0.1,
    "MinimumLevel": "Error"
  }
}
```

### Viewing Logs

Logs are stored in the `ApplicationLogs` table and can be viewed via:
- **Admin UI**: Navigate to `/admin/logs` (requires admin access)
- **API**: `GET /api/logs` (requires admin authentication)

### Granting Admin Access

To grant a user admin access for viewing logs:

**Option 1: API Endpoint (Development only)**
```bash
POST http://localhost:5157/api/auth/make-admin
Content-Type: application/json

{
  "email": "user@example.com"
}
```

**Option 2: Direct SQL**
```sql
UPDATE "Users" 
SET "IsAdmin" = true, "UpdatedAt" = NOW() 
WHERE "Email" = 'user@example.com';
```

**Important**: After granting admin access, the user must log out and log back in to receive a new JWT token with admin claims.

## Scheduled Jobs

Automated maintenance tasks run via GitHub Actions using the `DATABASE_URL` secret.

| Workflow | Schedule | What it does |
|---|---|---|
| `log-cleanup.yml` | Daily at 3:00 AM UTC | Deletes `ApplicationLogs` entries older than 24 hours |
| `db-backup.yml` | Every Sunday at 2:00 AM UTC | Runs `pg_dump` and uploads backup as a GitHub artifact (retained 90 days) |

Both can be triggered manually from the GitHub Actions UI via `workflow_dispatch`.

## Deployment

### Azure Deployment
1. Create Azure resources (App Service, PostgreSQL, Storage Account)
2. Configure connection strings in App Service settings
3. Deploy using GitHub Actions or Azure DevOps
4. Enable Application Insights for monitoring

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit changes with clear messages
4. Push to your fork
5. Submit a pull request

## License

This project is licensed under the MIT License.