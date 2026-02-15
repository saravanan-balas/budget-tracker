#!/usr/bin/env python3
"""
Build a lookup JSON from the Hugging Face transaction-categorization dataset.
Use this to power HF-based transaction categorization in the budget tracker.

Usage:
  1. pip install datasets
  2. huggingface-cli login   (and accept dataset terms at mitulshah/transaction-categorization)
  3. python scripts/build-hf-dataset-lookup.py

Output: common/BudgetTracker.Common/Data/HfTransactionLookup.json

Options:
  HF_SAMPLE=100000    Limit rows for faster run (default: 0 = full dataset)
  HF_OUTPUT=path      Custom output path
  HF_MIN_OCCURRENCES=2 Only include descriptions seen 2+ times (more reliable)
"""

import json
import os
import sys
from collections import Counter
from pathlib import Path

# HF dataset columns: transaction_description, category, country, currency
HF_DATASET = "mitulshah/transaction-categorization"


def main():
    sample = int(os.environ.get("HF_SAMPLE", 0))
    output_path = os.environ.get("HF_OUTPUT") or str(
        Path(__file__).resolve().parent.parent
        / "common"
        / "BudgetTracker.Common"
        / "Data"
        / "HfTransactionLookup.json"
    )
    min_occurrences = int(os.environ.get("HF_MIN_OCCURRENCES", 1))

    print("Loading Hugging Face dataset (mitulshah/transaction-categorization)...")
    try:
        from datasets import load_dataset
    except ImportError:
        print("Error: pip install datasets")
        sys.exit(1)

    try:
        ds = load_dataset(HF_DATASET, split="train")
        if sample > 0:
            ds = ds.shuffle(seed=42).select(range(min(sample, len(ds))))
            print(f"  Using sample of {len(ds):,} rows")
    except Exception as e:
        print(f"Error: {e}")
        print("  Run: huggingface-cli login")
        print("  Accept terms: https://huggingface.co/datasets/mitulshah/transaction-categorization")
        sys.exit(1)

    # desc -> [(category, count), ...]; we'll take most frequent category
    desc_counts: dict[str, Counter] = {}
    for row in ds:
        desc = (row.get("transaction_description") or "").strip()
        cat = (row.get("category") or "").strip()
        if not desc or not cat:
            continue
        key = desc.lower()
        if key not in desc_counts:
            desc_counts[key] = Counter()
        desc_counts[key][cat] += 1

    # Build final lookup: desc -> most common category (only if min_occurrences met)
    lookup = {}
    for key, counter in desc_counts.items():
        most_common = counter.most_common(1)[0]
        category, count = most_common[0], most_common[1]
        if count >= min_occurrences:
            lookup[key] = category

    out_dir = Path(output_path).parent
    out_dir.mkdir(parents=True, exist_ok=True)

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(lookup, f, ensure_ascii=False)

    print(f"Wrote {len(lookup):,} entries to {output_path}")
    print(f"File size: {Path(output_path).stat().st_size / (1024*1024):.2f} MB")


if __name__ == "__main__":
    main()
