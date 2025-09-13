# Budget Tracker Test Data Generator

This console utility generates realistic test data for the Budget Tracker application, including a test user with sample accounts, categories, and a full year of transactions.

## Generated Data

### Test User
- **Email**: test@example.com  
- **Password**: Test123**
- **Name**: Test User

### Sample Accounts (4)
- Primary Checking ($5,000 balance)
- Savings Account ($15,000 balance) 
- Rewards Credit Card (-$1,250 balance)
- Cash Wallet ($300 balance)

### Categories (20)
**Income Categories (5):**
- Salary, Freelance, Investment Returns, Side Business, Other Income

**Expense Categories (15):**
- Groceries, Dining Out, Transportation, Utilities, Rent/Mortgage
- Healthcare, Entertainment, Shopping, Subscriptions, Insurance
- Education, Travel, Personal Care, Gifts & Donations, Other Expenses

### Realistic Transactions (~400+ over one year)
- **Monthly salary** deposits (~$5,000)
- **Weekly grocery** shopping ($80-$200)
- **Daily random expenses** (dining, transport, entertainment, shopping)
- **Monthly recurring** bills (rent $2,200, utilities ~$150, insurance ~$275)
- **Occasional freelance** income ($300-$1,200)

## Usage

### Prerequisites
- .NET 8 SDK
- PostgreSQL database running (via Docker or local)
- Database connection configured in appsettings.json

### Running the Generator

1. **Navigate to utility directory:**
   ```bash
   cd utility/BudgetTracker.TestDataGenerator
   ```

2. **Restore packages:**
   ```bash
   dotnet restore
   ```

3. **Update connection string if needed:**
   Edit `appsettings.json` if your PostgreSQL connection differs:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=budgettracker;Username=postgres;Password=postgres"
     }
   }
   ```

4. **Run the generator:**
   ```bash
   dotnet run
   ```

### Expected Output
```
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Starting test data generation...
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Created test user: test@example.com
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Created 4 sample accounts
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Created 20 sample categories
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Generated 438 transactions over one year
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Generated test data for user: test@example.com
info: BudgetTracker.TestDataGenerator.TestDataGenerator[0]
      Test data generation completed successfully!
```

## Features

- **Cleanup**: Automatically removes existing test user data before regenerating
- **Realistic Patterns**: Simulates real-world spending and income patterns
- **Date Distribution**: Spreads transactions across 365 days with realistic timing
- **Account Variety**: Uses different accounts for different transaction types
- **Category Diversity**: Assigns appropriate categories to each transaction type
- **Error Handling**: Comprehensive logging and error management

## Testing the Generated Data

After running the generator, you can:

1. **Login to the app** with `test@example.com` / `Test123**`
2. **View transactions** on the `/transactions` page
3. **Check charts** showing spending by category and daily trends
4. **Test filtering** by date ranges, categories, and accounts
5. **Verify data integrity** across different months and categories

## Re-running

The utility can be run multiple times safely - it will clean up existing test data before generating new data.