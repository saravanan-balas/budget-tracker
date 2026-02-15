# Hugging Face Coverage Check

Verifies if the HF transaction dataset would categorize your Miscellaneous merchants.

## One-time setup

1. **Create Hugging Face account**: https://huggingface.co/join

2. **Accept dataset terms**: https://huggingface.co/datasets/mitulshah/transaction-categorization  
   (Click through to accept conditions)

3. **Log in via CLI**:
   ```bash
   pip install huggingface_hub
   huggingface-cli login
   ```
   Enter your HF token (get it from https://huggingface.co/settings/tokens)

## Add your merchants

Edit `scripts/miscellaneous-merchants.txt` — one merchant/description per line.  
Copy from your transactions page (filter by "Miscellaneous").

Example format:
```
SIMPLISAFE
YUPPTV USA INC
SQ SRI VENKATESWARA TEMP
AMAZON MKTPL QT4G88P63
```

## Run

```bash
cd /Users/ram/workspace/personal/Ram-Coding/budget-tracker

# Quick run (50k sample, ~30 sec)
HF_SAMPLE=50000 python3 scripts/verify-hf-coverage.py scripts/miscellaneous-merchants.txt

# Full dataset (~2 min)
python3 scripts/verify-hf-coverage.py scripts/miscellaneous-merchants.txt
```

## Output

- ✓ EXACT — merchant found in HF with category  
- ~ PARTIAL — substring match in HF  
- ✗ NOT IN DATASET — not found  

Summary shows how many would be categorized by HF.
