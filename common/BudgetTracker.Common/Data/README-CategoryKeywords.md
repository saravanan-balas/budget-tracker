# Merchant Category Keywords

`MerchantCategoryKeywords.json` drives rule-based transaction categorization. Keywords are matched against merchant names and transaction descriptions.

## Regenerating from MCC (recommended)

The file is **generated** from the [greggles/mcc-codes](https://github.com/greggles/mcc-codes) dataset (MCC = Merchant Category Code standard). To regenerate:

```bash
npm run generate:mcc-keywords
```

This fetches `mcc_codes.csv` from GitHub, maps MCC irs_description values to app categories, extracts keywords from edited_description, and overwrites this JSON file. You get **280+ categories** with thousands of keywords—no longer limited to previously hardcoded values.

## Customization

Edit `scripts/generate-mcc-keywords.js` to:

- Change `IRS_TO_APP_CATEGORY` – map MCC categories to your app category names
- Adjust `GENERIC_SKIP` – filter out overly generic keywords
- Modify `extractKeywords()` – change how keywords are derived from MCC entries

## Manual editing

You can also edit `MerchantCategoryKeywords.json` directly. Format:

```json
{
  "CategoryName": ["keyword1", "keyword2", "keyword3"]
}
```

- Use lowercase keywords; matching is case-insensitive.
- Keywords are matched with `Contains` against merchant + description text.
- Category names must match app category names (or will be created on first use).
