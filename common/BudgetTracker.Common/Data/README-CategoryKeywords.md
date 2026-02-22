# Merchant Category Keywords

`MerchantCategoryKeywords.json` provides rule-based transaction categorization using curated multi-word keywords. Keywords are matched (case-insensitive `Contains`) against merchant names and transaction descriptions.

## Design Principles

- **Multi-word only** — every keyword must be unambiguous enough that a substring match won't produce false positives (e.g., "grocery store" not "store")
- **Curated, not generated** — the old auto-generated MCC approach produced 2700+ keywords with many false positives; this file is manually maintained
- **Complementary** — well-known merchants are handled by `WellKnownMerchants.json`; the HF dataset handles the long tail; these keywords catch generic category patterns

## Categorization Pipeline Order

1. Memory cache (in-process)
2. Well-known merchant heuristics (`WellKnownMerchants.json`)
3. HF dataset lookup (`HfTransactionLookup.json`)
4. **MCC keyword rules** (`MerchantCategoryKeywords.json`) ← this file
5. Learned merchant mappings (database)
6. AI fallback (OpenAI) — results are persisted to avoid repeat calls

## Editing

Edit `MerchantCategoryKeywords.json` directly. Format:

```json
{
  "CategoryName": ["keyword phrase 1", "keyword phrase 2"]
}
```

- Use lowercase keywords; matching is case-insensitive
- Keywords are matched with `Contains` against merchant + description text
- Category names must match existing app category names
- Test changes with: `dotnet test tests/BudgetTracker.Categorization.Tests/`
