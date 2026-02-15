#!/usr/bin/env python3
"""
Verify if the Hugging Face transaction-categorization dataset would improve
categorization for merchants currently in Miscellaneous.

Usage:
  1. Create a file 'miscellaneous-merchants.txt' with one merchant/description per line
     (copy from your transactions page)
  2. pip install datasets
  3. python scripts/verify-hf-coverage.py

Or pipe: echo -e "SIMPLISAFE\nYUPPTV" | python scripts/verify-hf-coverage.py

To get your 17 merchants: Filter by "Miscellaneous" on /transactions,
copy the merchant names, paste into miscellaneous-merchants.txt (one per line).

Requires: Hugging Face account + accept dataset terms at
  https://huggingface.co/datasets/mitulshah/transaction-categorization
"""

import sys
import os

def main():
    # Read input: file or stdin
    if len(sys.argv) > 1:
        path = sys.argv[1]
        with open(path) as f:
            merchants = [line.strip() for line in f if line.strip()]
    else:
        merchants = [line.strip() for line in sys.stdin if line.strip()]

    if not merchants:
        print("No merchants provided.")
        print("Usage: python verify-hf-coverage.py miscellaneous-merchants.txt")
        print("   or: echo 'SIMPLISAFE' | python verify-hf-coverage.py")
        sys.exit(1)

    print(f"Checking {len(merchants)} merchants against Hugging Face dataset...\n")

    try:
        from datasets import load_dataset
    except ImportError:
        print("Install: pip install datasets")
        sys.exit(1)

    # Load dataset (requires HF login + accepting terms for mitulshah/transaction-categorization)
    sample = int(os.environ.get("HF_SAMPLE", 0))  # 0 = full dataset
    print("Loading mitulshah/transaction-categorization (may take 1-2 min for full dataset)...")
    try:
        ds = load_dataset("mitulshah/transaction-categorization", split="train")
        if sample > 0:
            ds = ds.shuffle(seed=42).select(range(min(sample, len(ds))))
            print(f"  (using sample of {len(ds):,} for faster run)")
    except Exception as e:
        print(f"Error: {e}")
        print("\nMake sure you:")
        print("  1. pip install datasets")
        print("  2. Have a Hugging Face account")
        print("  3. Accepted terms at https://huggingface.co/datasets/mitulshah/transaction-categorization")
        print("  4. Logged in: huggingface-cli login")
        sys.exit(1)

    # Build lookup: description (lowercase) -> category
    desc_to_cat = {}
    for row in ds:
        desc = (row.get("transaction_description") or "").strip().lower()
        cat = (row.get("category") or "").strip()
        if desc and cat and desc not in desc_to_cat:
            desc_to_cat[desc] = cat

    print(f"Loaded {len(ds):,} transactions, {len(desc_to_cat):,} unique descriptions\n")

    # HF categories for reference
    hf_categories = {
        "Charity & Donations", "Government & Legal", "Income", "Financial Services",
        "Utilities & Services", "Healthcare & Medical", "Entertainment & Recreation",
        "Shopping & Retail", "Transportation", "Food & Dining"
    }

    results = []
    for m in merchants:
        m_lower = m.lower()
        # Exact match
        match = desc_to_cat.get(m_lower)
        if match:
            results.append((m, "EXACT", match))
            continue
        # Substring match: search for descriptions containing this merchant
        matches = [(d, c) for d, c in desc_to_cat.items() if m_lower in d or d in m_lower]
        if matches:
            # Take most relevant (prefer when merchant is at start)
            best = min(matches, key=lambda x: (0 if m_lower in x[0][:len(m_lower)+5] else 1, len(x[0])))
            results.append((m, "PARTIAL", best[1], best[0][:60]))
        else:
            results.append((m, "NONE", None))

    # Report
    print("=" * 70)
    print("RESULTS: Would Hugging Face dataset help?")
    print("=" * 70)

    helped = 0
    for r in results:
        merchant, match_type, category, *extra = r
        if match_type == "EXACT":
            print(f"  ✓ {merchant[:45]:<45} -> {category}")
            helped += 1
        elif match_type == "PARTIAL":
            print(f"  ~ {merchant[:45]:<45} -> {category}  (partial: {extra[0]}...)")
            helped += 1
        else:
            print(f"  ✗ {merchant[:45]:<45} -> NOT IN DATASET")

    print("=" * 70)
    print(f"\nSummary: {helped}/{len(merchants)} would be categorized by HF dataset.")
    if helped > 0:
        print("Yes, Hugging Face integration would likely reduce Miscellaneous counts.")
    else:
        print("HF dataset may not cover these specific merchants; the pre-trained model")
        print("(global-financial-transaction-classifier) might still help via semantic similarity.")

if __name__ == "__main__":
    main()
