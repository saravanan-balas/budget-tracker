# Configuration Guide

## Required Configuration

### 1. Google OAuth Setup

1. **Go to [Google Cloud Console](https://console.cloud.google.com/)**
2. **Create OAuth 2.0 credentials**
3. **Update `api/BudgetTracker.API/appsettings.json`**:
   ```json
   "Google": {
     "ClientId": "your-actual-client-id.apps.googleusercontent.com",
     "ClientSecret": "your-actual-client-secret"
   }
   ```
4. **Update frontend files** (`app/pages/auth/login.vue` and `register.vue`):
   ```javascript
   client_id: 'your-actual-client-id.apps.googleusercontent.com'
   ```

### 2. SendGrid Email Setup

1. **Sign up for [SendGrid](https://sendgrid.com)**
2. **Create API key**
3. **Update `api/BudgetTracker.API/appsettings.json`**:
   ```json
   "Email": {
     "SendGridApiKey": "your-actual-sendgrid-api-key",
     "FromEmail": "your-verified-email@domain.com",
     "FromName": "Budget Tracker"
   }
   ```

### 3. Database Migration

Run the following command to apply database migrations:
```bash
cd api/BudgetTracker.API
dotnet ef database update
```

## Features Implemented

✅ **Forgot Password** - Users can request password reset via email
✅ **Reset Password** - Users can reset password using email token
✅ **Change Password** - Authenticated users can change their password
✅ **Email Service** - SendGrid integration with fallback to SMTP
✅ **Frontend UI** - Complete user interface for all password flows
✅ **Security** - Secure token generation and validation
✅ **Error Handling** - Comprehensive error handling and validation

## Testing

1. **Start the application**:
   ```bash
   docker-compose up -d
   ```

2. **Test forgot password**:
   - Go to `/auth/forgot-password`
   - Enter email address
   - Check email for reset link

3. **Test password reset**:
   - Click link in email
   - Enter new password
   - Verify login works

4. **Test change password**:
   - Login to application
   - Go to Settings → Change Password
   - Enter current and new password
