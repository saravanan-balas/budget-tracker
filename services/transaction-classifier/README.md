# Transaction Classifier (global-financial-transaction-classifier)

A minimal inference service for `mitulshah/global-financial-transaction-classifier`. Runs the DistilBERT model in a container and exposes a REST API for transaction categorization.

## How to use the model

```python
# Option 1: Standard transformers pipeline (no custom inference module)
from transformers import pipeline

classifier = pipeline(
    "text-classification",
    model="mitulshah/global-financial-transaction-classifier"
)

result = classifier("SIMPLISAFE HOME SECURITY MONTHLY")
# [{"label": "Utilities & Services", "score": 0.92}]
```

```bash
# Option 2: This service (HTTP API)
curl -X POST http://localhost:8000/classify \
  -H "Content-Type: application/json" \
  -d '{"text": "SIMPLISAFE HOME SECURITY MONTHLY"}'
# {"category": "Utilities & Services", "confidence": 0.92}
```

## Cost to run in your container

| Scenario | Cost |
|---------|------|
| **Your own VM/container** | **$0** extra if you already have a server. Just CPU + RAM. |
| **Small cloud instance** (2 vCPU, 4GB RAM, e.g. AWS t3.medium) | ~$30–40/month |
| **CPU-only container** (no GPU) | DistilBERT runs fine on CPU. ~50–200ms per request. |
| **GPU** (optional) | ~$0.50–2.50/hr if you use T4/L4/A100 – overkill for this model |

**Model specs:** 267MB, runs on CPU, ~1–2GB RAM total for Python + model.

**Summary:** If you have existing container/VM capacity, running this model adds **no API cost**. You only pay for the compute you already run. DistilBERT is lightweight; a 2 vCPU container can handle hundreds of categorizations per minute.

## Setup

1. Accept model terms: https://huggingface.co/mitulshah/global-financial-transaction-classifier  
2. `huggingface-cli login` (or set `HF_TOKEN` env var)

## Run locally

```bash
pip install torch transformers fastapi uvicorn
python app.py
```

## Run in Docker

```bash
docker build -t transaction-classifier .
docker run -p 8000:8000 -e HF_TOKEN=your_token transaction-classifier
```

## API

- `POST /classify` – `{"text": "MERCHANT DESCRIPTION"}` → `{"category": "...", "confidence": 0.95}`
- `POST /classify/batch` – `{"texts": ["...", "..."]}` → `[{"category": "...", "confidence": ...}, ...]`
- `GET /health` – Health check

## Integrate with Budget Tracker (C#)

Add an `IMlTransactionClassifier` that calls this service when MCC + HF lookup + learned mappings don't match. Use it in `TryDefaultAssignment` before falling back to AI/Uncategorized. Configure the classifier URL via `appsettings.json` (e.g. `MlClassifier:BaseUrl`).
