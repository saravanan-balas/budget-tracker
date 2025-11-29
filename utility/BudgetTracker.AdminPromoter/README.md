# Admin Promoter Utility

This utility script promotes a user to admin status by setting their `IsAdmin` flag to `true` in the database.

## Usage

```bash
cd utility/BudgetTracker.AdminPromoter
dotnet run <email>
```

### Example

```bash
dotnet run user@example.com
```

## Configuration

The connection string is configured in `appsettings.json`. You can override it using environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
dotnet run user@example.com
```

## Important Notes

After running this script, the user **must logout and login again** to get a new JWT token with the updated `IsAdmin` claim. The logs tab will only appear after re-authentication.

## What It Does

1. Connects to the database using the connection string from `appsettings.json`
2. Finds the user by email address
3. Sets `IsAdmin = true` for that user
4. Updates the `UpdatedAt` timestamp
5. Saves changes to the database

## Error Handling

- If no email is provided, shows usage instructions
- If user is not found, lists all available users
- If user is already admin, confirms and exits
- If database connection fails, shows error message

