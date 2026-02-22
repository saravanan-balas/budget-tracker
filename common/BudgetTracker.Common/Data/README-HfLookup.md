# HF Transaction Dataset Lookup

`HfTransactionLookup.json` provides transaction-to-category mappings from the [mitulshah/transaction-categorization](https://huggingface.co/datasets/mitulshah/transaction-categorization) dataset (~700K entries). It is shipped as a `Content` file (not embedded — too large at ~70MB).

## Generating the lookup

```bash
# Install
pip install datasets

# Login to Hugging Face (accept dataset terms at the link above)
huggingface-cli login

# Build full lookup
python scripts/build-hf-dataset-lookup.py
```

**Options (env vars):**
- `HF_SAMPLE=100000` – Use a sample for faster runs
- `HF_MIN_OCCURRENCES=2` – Only include descriptions seen 2+ times
- `HF_OUTPUT=/path/to/file.json` – Custom output path

Output goes to `common/BudgetTracker.Common/Data/HfTransactionLookup.json`.

> **Note:** This file is in `.gitignore` because it is too large for git. Regenerate it locally after cloning.

## Categorization Pipeline Order

1. Memory cache (in-process)
2. Well-known merchant heuristics (`WellKnownMerchants.json`)
3. **HF dataset lookup** (`HfTransactionLookup.json`) ← this file
4. MCC keyword rules (`MerchantCategoryKeywords.json`)
5. Learned merchant mappings (database)
6. AI fallback (OpenAI) — results are persisted to avoid repeat calls

## How matching works

1. Exact match on merchant name, description, or combined text
2. Substring match with safeguards: HF key must cover ≥50% of merchant length to prevent false positives (e.g., HF key "delta" incorrectly matching "DELTA DENTAL")
3. HF dataset categories are mapped to app categories via `HfToAppCategory` dictionary

If no lookup file is present, HF-based categorization is skipped; the app continues with the remaining pipeline stages.
