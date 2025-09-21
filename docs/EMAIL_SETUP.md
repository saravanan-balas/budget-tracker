# Email Setup Guide

## Option 1: SendGrid (Recommended - Free Tier: 100 emails/day)

### Step 1: Create SendGrid Account
1. Go to [sendgrid.com](https://sendgrid.com)
2. Sign up for a free account
3. Verify your email address

### Step 2: Create API Key
1. Go to Settings → API Keys
2. Click "Create API Key"
3. Choose "Restricted Access"
4. Give it "Mail Send" permissions
5. Copy the API key (you'll only see it once!)

### Step 3: Update Configuration
Edit `api/BudgetTracker.API/appsettings.json`:

```json
"Email": {
  "SendGridApiKey": "SG.your-actual-api-key-here",
  "FromEmail": "noreply@budgettracker.com",
  "FromName": "Budget Tracker"
}
```

### Step 4: Restart API
```bash
docker-compose restart api
```

## Option 2: Mailgun (Alternative - Free Tier: 5,000 emails/month)

### Step 1: Create Mailgun Account
1. Go to [mailgun.com](https://mailgun.com)
2. Sign up for a free account
3. Verify your domain (or use sandbox domain for testing)

### Step 2: Get API Key
1. Go to Settings → API Keys
2. Copy your Private API key

### Step 3: Update Configuration
Edit `api/BudgetTracker.API/appsettings.json`:

```json
"Email": {
  "MailgunApiKey": "your-mailgun-api-key",
  "MailgunDomain": "your-domain.mailgun.org",
  "FromEmail": "noreply@yourdomain.com",
  "FromName": "Budget Tracker"
}
```

## Testing

After setup, test the forgot password feature:
1. Go to http://localhost:3000/auth/forgot-password
2. Enter your email address
3. Check your inbox for the reset email!

## Benefits of Using SendGrid/Mailgun

✅ **Works with any email provider** (Gmail, Outlook, Yahoo, etc.)
✅ **No need to configure SMTP settings**
✅ **Better deliverability** (less likely to go to spam)
✅ **Free tiers available**
✅ **Professional email templates**
✅ **Email analytics and tracking**
