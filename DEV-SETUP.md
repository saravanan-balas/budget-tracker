# Budget Tracker - Docker Local Development Setup

## Quick Start

### Start All Services with Docker
```bash
npm run docker:dev
```
This will start all services (API, Frontend, Database, Redis, Worker) in Docker containers.

### Alternative Docker Commands
```bash
# Start all services
docker-compose up -d

# Start with rebuild
docker-compose up --build -d

# Stop all services
docker-compose down

# View logs
docker-compose logs -f
```

## Prerequisites

Make sure you have:
- Docker Desktop installed and running
- Docker Compose v2+

## Service URLs (Docker Local)

- **Frontend**: http://localhost:3000
- **API**: http://localhost:5000
- **API Swagger**: http://localhost:5000/swagger
- **Database**: localhost:5432
- **Redis**: localhost:6379

## Available Scripts

- `npm run docker:dev` - Start all services in Docker (recommended)
- `npm run docker:build` - Build and start all services
- `npm run docker:up` - Start all services
- `npm run docker:down` - Stop all services
- `npm run docker:logs` - View logs from all services
- `npm run docker:clean` - Stop services and remove containers/volumes

## Development Workflow

### 1. Start Development Environment
```bash
npm run docker:dev
```

### 2. Access Your Application
- Open http://localhost:3000 in your browser
- The frontend will automatically connect to the API at http://api:5000 (internal Docker network)

### 3. View API Documentation
- Visit http://localhost:5000/swagger to explore the API

### 4. Monitor Logs
```bash
npm run docker:logs
```

### 5. Stop When Done
```bash
npm run docker:down
```

## Database Management

### Connect to Database
```bash
# Using psql
docker exec -it budget_tracker_db psql -U postgres -d budget_tracker

# Or using a GUI tool
# Host: localhost
# Port: 5432
# Database: budget_tracker
# Username: postgres
# Password: postgres
```

### Reset Database
```bash
docker-compose down -v
docker-compose up -d
```

## Troubleshooting

### Port Conflicts
If you get port conflicts, make sure no other services are using:
- Port 3000 (Frontend)
- Port 5000 (API)
- Port 5432 (PostgreSQL)
- Port 6379 (Redis)

### Container Issues
```bash
# Check container status
docker-compose ps

# Restart specific service
docker-compose restart frontend

# Rebuild specific service
docker-compose up --build frontend
```

### View Service Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f frontend
docker-compose logs -f api
```

## Configuration

The application is configured for Docker local development:
- Frontend connects to API via Docker internal network (`http://api:5000`)
- Database and Redis are accessible via Docker network
- All services run in isolated containers

## Hot Reloading

For development with hot reloading, you can mount your source code as volumes. The current setup builds the containers, but you can modify the docker-compose.yml to mount source directories for live development.
