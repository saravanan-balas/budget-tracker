-- Set user as admin by email
-- Usage: Run this script against your PostgreSQL database
-- Replace 'sacssuresh@gmail.com' with the email you want to make admin

UPDATE "Users"
SET "IsAdmin" = true
WHERE "Email" = 'sacssuresh@gmail.com';

-- Verify the update
SELECT "Id", "Email", "FirstName", "LastName", "IsAdmin"
FROM "Users"
WHERE "Email" = 'sacssuresh@gmail.com';

