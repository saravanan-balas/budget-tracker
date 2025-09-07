# AI-First Budget Tracker

An intelligent personal finance management system with AI-powered insights, automated transaction categorization, and conversational analytics.

## Architecture

```
budget-tracker/
├── api/                    # .NET Core Web API
├── worker/                 # Background processing service
├── common/                 # Shared models and infrastructure
├── ui/                     # Nuxt/Vue frontend
├── docs/                   # Documentation
├── docker-compose.yml      # Local development setup
└── init.sql               # Database initialization
```

## Tech Stack

- **Backend**: .NET 8, C#, Entity Framework Core
- **Frontend**: Nuxt 3, Vue 3, TypeScript
- **Database**: PostgreSQL with pgvector extension
- **Cache**: Redis
- **Storage**: Azure Blob Storage
- **AI/ML**: OpenAI GPT-4, embeddings for semantic search
- **Infrastructure**: Docker, Azure

## Features

### MVP Features
- CSV/PDF statement import with intelligent parsing
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
- API: http://localhost:5000/swagger
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
cd ui
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