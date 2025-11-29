# Budget Tracker - Local Development Setup

## Quick Start

### Prerequisites

Make sure you have:
- **.NET 8 SDK** installed ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** and npm installed ([Download](https://nodejs.org/))
- **PostgreSQL client tools** (optional, for database management)
- **Neon Database** account with a database created ([Neon](https://neon.tech))

### 1. Clone and Install Dependencies

```bash
# Install frontend dependencies
cd app
npm install

# Install root dependencies (if needed)
cd ..
npm install
```

### 2. Configure Environment Variables

#### API Configuration

The API uses `appsettings.json` and `appsettings.Development.json` for configuration. Update the connection string in `api/BudgetTracker.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-neon-host.neon.tech;Database=your-database;Username=your-username;Password=your-password;SSL Mode=Require"
  }
}
```

**Required Environment Variables** (set in your shell or IDE):
- `OPENAI_API_KEY` - Your OpenAI API key for AI features
- `JWT_KEY` - Secure JWT signing key (or use default in appsettings.json)
- `Google:ClientId` - Google OAuth client ID (optional)
- `Google:ClientSecret` - Google OAuth client secret (optional)

#### Frontend Configuration

The frontend uses `nuxt.config.ts` which reads from environment variables. For local development, the default API URL is `http://localhost:5157`.

To override, create a `.env` file in the `app/` directory (optional):
```bash
NUXT_PUBLIC_API_BASE_URL=http://localhost:5157
```

### 3. Run Database Migrations

```bash
cd api/BudgetTracker.API
dotnet ef database update
```

### 4. Start the API

```bash
cd api/BudgetTracker.API
dotnet run
```

The API will start on:
- **HTTP**: http://localhost:5157
- **Swagger UI**: http://localhost:5157/swagger

### 5. Start the Frontend

In a new terminal:

```bash
cd app
npm run dev
```

The frontend will start on:
- **Frontend**: http://localhost:3000

## Service URLs (Local Development)

- **Frontend**: http://localhost:3000
- **API**: http://localhost:5157
- **API Swagger**: http://localhost:5157/swagger
- **Database**: Neon Cloud (configured in appsettings)

## Development Workflow

### 1. Start Development Environment

**Terminal 1 - API:**
```bash
cd api/BudgetTracker.API
dotnet run
```

**Terminal 2 - Frontend:**
```bash
cd app
npm run dev
```

### 2. Access Your Application

- Open http://localhost:3000 in your browser
- The frontend will automatically connect to the API at http://localhost:5157

### 3. View API Documentation

- Visit http://localhost:5157/swagger to explore the API endpoints

### 4. Hot Reloading

Both services support hot reloading:
- **API**: Automatically restarts when you change C# files
- **Frontend**: Automatically reloads when you change Vue/TypeScript files

## Database Management

### Connect to Neon Database

You can connect to your Neon database using:
- **Neon Console**: Use the SQL Editor in the Neon dashboard
- **psql**: Use the connection string from your Neon dashboard
- **GUI Tools**: Use tools like pgAdmin, DBeaver, DataGrip, or TablePlus with your Neon connection string

### Connect Using DataGrip

1. **Install DataGrip**
   - Download and install DataGrip from [JetBrains](https://www.jetbrains.com/datagrip/)

2. **Add Data Source**
   - Open DataGrip
   - Click the `+` button or go to `File` → `New` → `Data Source` → `PostgreSQL`
   - Enter the following connection details:
     - **Name**: `budget-tracker-dev`
     - **Host**: `ep-divine-wind-admlrh2m-pooler.c-2.us-east-1.aws.neon.tech`
     - **Port**: `5432`
     - **Database**: `neondb`
     - **User**: `neondb_owner`
     - **Password**: `npg_JFHy8Rg9nNrZ`
   - In the **SSL** tab, enable SSL and set mode to `require`
   - Click **Test Connection** to verify the connection works
   - Click **OK** to save the data source

### Run Migrations

```bash
cd api/BudgetTracker.API
dotnet ef database update
```

### Create a New Migration

```bash
cd api/BudgetTracker.API
dotnet ef migrations add MigrationName
```

## Available Scripts

### Frontend (app/)
- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run generate` - Generate static site
- `npm run generate:prod` - Generate static site with production API URL
- `npm run preview` - Preview production build

### API (api/BudgetTracker.API/)
- `dotnet run` - Run the API
- `dotnet build` - Build the API
- `dotnet ef database update` - Run database migrations
- `dotnet ef migrations add <name>` - Create a new migration

## Troubleshooting

### Port Conflicts

If you get port conflicts, make sure no other services are using:
- Port 3000 (Frontend)
- Port 5157 (API)

To change ports:
- **API**: Edit `api/BudgetTracker.API/Properties/launchSettings.json`
- **Frontend**: Edit `app/nuxt.config.ts` or use `--port` flag: `npm run dev -- --port 3001`

### Database Connection Issues

- Verify your Neon connection string is correct
- Check that your Neon database is running
- Ensure SSL mode is set to "Require" in the connection string
- Verify your IP is allowed (Neon databases are accessible from anywhere by default)

### API Not Starting

- Check that .NET 8 SDK is installed: `dotnet --version`
- Verify all NuGet packages are restored: `dotnet restore`
- Check the API logs for specific error messages

### Frontend Not Starting

- Verify Node.js version: `node --version` (should be 20+)
- Clear node_modules and reinstall: `rm -rf node_modules && npm install`
- Check for port conflicts on port 3000

### Environment Variables Not Loading

- For API: Set environment variables in your shell or IDE before running `dotnet run`
- For Frontend: Create a `.env` file in the `app/` directory or set them in your shell

## Configuration Files

### API Configuration
- `api/BudgetTracker.API/appsettings.json` - Production settings
- `api/BudgetTracker.API/appsettings.Development.json` - Development settings (database connection)

### Frontend Configuration
- `app/nuxt.config.ts` - Nuxt configuration
- `app/.env` - Environment variables (optional, not committed to git)

## Development Tips

1. **API Logging**: Check `api/BudgetTracker.API/logs/` for application logs
2. **Frontend DevTools**: Use browser DevTools and Vue DevTools for debugging
3. **API Testing**: Use Swagger UI at http://localhost:5157/swagger to test endpoints
4. **Database Changes**: Always create migrations for schema changes using `dotnet ef migrations add`

## Admin Access and Logs Tab

### Enabling Admin Access

To access the logs tab in the dashboard, you need to promote your user account to admin status:

1. **Run the Admin Promoter utility:**
   ```bash
   cd utility/BudgetTracker.AdminPromoter
   dotnet run your-email@example.com
   ```

2. **Important: Re-authenticate**
   - After promoting your account, you **must logout and login again**
   - This generates a new JWT token with the updated `IsAdmin` claim
   - The logs tab will only appear after re-authentication

3. **Verify access:**
   - After logging back in, you should see a "Logs" tab in the navigation bar
   - You can access the logs page at `/admin/logs`

### Alternative: Direct Database Update

If you prefer to update the database directly:

```sql
UPDATE "Users" SET "IsAdmin" = true WHERE "Email" = 'your-email@example.com';
```

**Remember:** You must logout and login again after updating the database for the changes to take effect.

## Next Steps

- Set up your OpenAI API key for AI features
- Configure Google OAuth if needed
- Set up email service (SendGrid) for password reset functionality
- Review `docs/` folder for additional configuration guides
