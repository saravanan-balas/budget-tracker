-- Check import status
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
ORDER BY created_at DESC
LIMIT 10;

-- Check transactions linked to imports
SELECT 
    i.id as import_id,
    i.file_name,
    COUNT(t.id) as linked_transactions
FROM imported_files i
LEFT JOIN transactions t ON t.imported_file_id = i.id
GROUP BY i.id, i.file_name
ORDER BY i.created_at DESC;

-- View recent transactions
SELECT 
    id,
    transaction_date,
    original_merchant,
    amount,
    import_hash,
    imported_file_id,
    created_at
FROM transactions
ORDER BY created_at DESC
LIMIT 20;

-- To clear all transactions (BE CAREFUL - this deletes all transaction data!)
-- DELETE FROM transactions;
-- UPDATE imported_files SET imported_transactions = 0, duplicate_transactions = 0;

-- To clear transactions from a specific import
-- DELETE FROM transactions WHERE imported_file_id = 'YOUR_IMPORT_ID_HERE';