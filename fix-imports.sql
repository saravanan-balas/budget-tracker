-- 1. First, check what imports you have
SELECT 
    id,
    file_name,
    status,
    total_rows,
    imported_transactions,
    duplicate_transactions,
    detected_bank_name,
    created_at
FROM imported_files
ORDER BY created_at DESC;

-- 2. Check if transactions are actually linked to imports
SELECT 
    COUNT(*) as total_transactions,
    COUNT(imported_file_id) as linked_to_import,
    COUNT(*) - COUNT(imported_file_id) as orphaned_transactions
FROM transactions;

-- 3. View transactions without import links (likely from previous runs)
SELECT 
    id,
    transaction_date,
    original_merchant,
    amount,
    import_hash,
    imported_file_id,
    created_at
FROM transactions
WHERE imported_file_id IS NULL
ORDER BY created_at DESC
LIMIT 10;

-- 4. OPTION A: Link orphaned transactions to the most recent import
-- (Only do this if you're sure they belong to that import)
/*
UPDATE transactions 
SET imported_file_id = '2b327400-3028-4a04-9989-56429a8a4605'  -- Your import ID
WHERE imported_file_id IS NULL;
*/

-- 5. OPTION B: Clear all transactions to start fresh
-- (BE CAREFUL - this deletes all transaction data!)
/*
TRUNCATE TABLE transactions CASCADE;
UPDATE imported_files SET 
    status = 'Pending',
    imported_transactions = 0, 
    duplicate_transactions = 0,
    processed_rows = 0,
    total_rows = 0,
    processing_started_at = NULL,
    processing_completed_at = NULL;
*/

-- 6. OPTION C: Clear specific import to retry
/*
DELETE FROM transactions WHERE imported_file_id = '2b327400-3028-4a04-9989-56429a8a4605';
UPDATE imported_files 
SET 
    status = 'Processing',
    imported_transactions = 0,
    duplicate_transactions = 0,
    processed_rows = 0
WHERE id = '2b327400-3028-4a04-9989-56429a8a4605';
*/