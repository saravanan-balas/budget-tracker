# Google SSO Issues & Troubleshooting

## Current Status: ⚠️ Partially Working

### Issue Summary
Google Sign-In is experiencing CORS and FedCM (Federated Credential Management) issues that prevent successful authentication.

### Error Messages Encountered
```
- "Can't continue with google.com, Something went wrong"
- "Missing Allow Origin header"
- "The fetch of the id assertion endpoint resulted in a network error: ERR_FAILED"
- "Server did not send the correct CORS headers"
- "[GSI_LOGGER]: FedCM get() rejects with IdentityCredentialError: Error retrieving a token"
```

### Root Cause
Google's newer Identity Services API is trying to use FedCM (Federated Credential Management) which has stricter CORS requirements and is causing authentication failures.

### Attempted Solutions

#### 1. ✅ Configuration Updates
- **Published Google OAuth app** (moved from testing to production)
- **Added required scopes**: `email`, `profile`, `openid`
- **Updated authorized origins**: `http://localhost:3000`, `http://localhost:5000`
- **Updated redirect URIs**: `http://localhost:3000/auth/login`, `http://localhost:3000/auth/register`

#### 2. ✅ Code Changes
- **Switched from Google Identity Services** to older Google API (`gapi.auth2`)
- **Added FedCM disable flags**: `use_fedcm_for_prompt: false`, `itp_support: false`
- **Updated frontend implementation** to use `gapi.auth2.getAuthInstance()`

#### 3. ❌ Still Not Working
Despite all changes, Google Sign-In still fails with CORS/FedCM errors.

### Current Implementation
- **Backend**: Properly configured with Google OAuth credentials
- **Frontend**: Using `gapi.auth2` method instead of newer `google.accounts.id`
- **Configuration**: All OAuth settings are correct in Google Cloud Console

### Next Steps for Resolution

#### Option 1: Domain Verification
- **Verify domain ownership** in Google Cloud Console
- **Add domain to authorized origins** instead of localhost
- **Use HTTPS** instead of HTTP for development

#### Option 2: Alternative OAuth Flow
- **Implement server-side OAuth flow** instead of client-side
- **Use Google's server-side authentication** with redirects
- **Handle token exchange on backend**

#### Option 3: Different OAuth Provider
- **Consider Microsoft Azure AD** for SSO
- **Use Auth0** or similar identity provider
- **Implement custom OAuth solution**

### Files Modified
- `app/pages/auth/login.vue` - Updated Google Sign-In implementation
- `app/pages/auth/register.vue` - Updated Google Sign-In implementation
- `api/BudgetTracker.API/appsettings.json` - Google OAuth configuration
- `api/BudgetTracker.API/Services/AuthService.cs` - Google authentication logic

### Testing Notes
- **Password management features work perfectly** ✅
- **Email delivery works with SendGrid** ✅
- **Google SSO fails consistently** ❌
- **Regular login/register works** ✅

### Priority
- **Low Priority** - Password management was the main goal and is working
- **Can be addressed later** - SSO is nice-to-have, not critical
- **Main features are complete** - Forgot password, reset password, change password all work

### Resources
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [Google Identity Services Migration Guide](https://developers.google.com/identity/gsi/web/guides/migration)
- [FedCM Documentation](https://developer.mozilla.org/en-US/docs/Web/API/FedCM_API)

---
**Last Updated**: September 19, 2025  
**Status**: Open - Needs further investigation  
**Priority**: Low (main features working)
