#!/usr/bin/env python3
"""
Transaction classifier API using mitulshah/global-financial-transaction-classifier.
Run: pip install torch transformers fastapi uvicorn && python app.py
"""

import os
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

# Lazy load model on first request
_classifier = None


def get_classifier():
    global _classifier
    if _classifier is None:
        from transformers import pipeline
        model_id = os.environ.get("HF_MODEL", "mitulshah/global-financial-transaction-classifier")
        token = os.environ.get("HF_TOKEN")
        _classifier = pipeline(
            "text-classification",
            model=model_id,
            token=token,
        )
    return _classifier


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Preload model on startup (optional, removes first-request delay)
    try:
        get_classifier()
    except Exception:
        pass  # Will load on first request
    yield


app = FastAPI(title="Transaction Classifier", lifespan=lifespan)


class ClassifyRequest(BaseModel):
    text: str


class ClassifyBatchRequest(BaseModel):
    texts: list[str]


class ClassifyResponse(BaseModel):
    category: str
    confidence: float


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/classify", response_model=ClassifyResponse)
def classify(req: ClassifyRequest):
    try:
        classifier = get_classifier()
        result = classifier(req.text.strip() or "Unknown", truncation=True, max_length=128)
        if result:
            r = result[0]
            label = r.get("label", "LABEL_0")
            score = float(r.get("score", 0))
            return ClassifyResponse(category=label, confidence=score)
        return ClassifyResponse(category="Uncategorized", confidence=0)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/classify/batch")
def classify_batch(req: ClassifyBatchRequest):
    classifier = get_classifier()
    texts = [t.strip() or "Unknown" for t in req.texts]
    results = classifier(texts, truncation=True, max_length=128, batch_size=32)
    out = []
    for r in results:
        if isinstance(r, list):
            r = r[0] if r else {}
        label = r.get("label", "LABEL_0")
        score = float(r.get("score", 0))
        out.append({"category": label, "confidence": score})
    return out


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=int(os.environ.get("PORT", 8000)))
