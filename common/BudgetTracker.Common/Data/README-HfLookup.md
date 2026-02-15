# HF Transaction Dataset Lookup

`HfTransactionLookup.json` provides transaction-to-category mappings from the [mitulshah/transaction-categorization](https://huggingface.co/datasets/mitulshah/transaction-categorization) dataset. It is used as a fallback when MCC keyword rules and merchant-based learning don't match.

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

## Using the lookup

1. **Embed in the assembly** – Add to `BudgetTracker.Common.csproj`:
   ```xml
   <EmbeddedResource Include="Data\HfTransactionLookup.json" />
   ```

2. **Or copy to output** – Copy `HfTransactionLookup.json` to your app's output directory (or `Data/` subfolder) so the loader can find it at runtime.

If no lookup is present, HF-based categorization is skipped; the app continues with keyword rules and merchant learning.

## Categorization order

1. MCC keyword rules (`MerchantCategoryKeywords.json`)
2. **HF dataset lookup**
3. Merchant-based learning (from past assignments)
4. AI fallback or Uncategorized
