-- Database cleanup script for a single user (PostgreSQL)
-- ------------------------------------------------------
-- Usage:
--   1. Open psql (or your DB client) connected to the BudgetTracker database.
--   2. Replace the placeholder email below with the target user's email.
--   3. Run the entire script.
--
-- NOTE:
-- - Most user‑owned data (Accounts, Transactions, Categories, Rules, Goals,
--   ImportedFiles, PasswordResetTokens, etc.) is configured with
--   ON DELETE CASCADE at the database level via EF Core.
--   Deleting the user row will automatically delete those related rows.
-- - This script explicitly cleans up tables that may not cascade
--   or where we want to be extra explicit for safety.

BEGIN;

-- 1) Identify the user by email
WITH target_user AS (
    SELECT "Id" AS id
    FROM "Users"
    WHERE "Email" = 'user@example.com'  -- TODO: replace with real user email
),

-- 2) Explicitly delete user-related rows that are not cascaded
del_user_merchant AS (
    DELETE FROM "UserMerchantCategoryMappings"
    WHERE "UserId" IN (SELECT id FROM target_user)
    RETURNING 1
),
del_audit_events AS (
    DELETE FROM "AuditEvents"
    WHERE "UserId" IN (SELECT id FROM target_user)
    RETURNING 1
)

-- 3) Delete the user row (will cascade to most dependent tables)
DELETE FROM "Users"
WHERE "Id" IN (SELECT id FROM target_user);

COMMIT;


