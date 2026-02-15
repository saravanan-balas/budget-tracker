FROM python:3.11-slim

WORKDIR /app

# Install deps (torch CPU-only to keep image smaller)
RUN pip install --no-cache-dir torch --index-url https://download.pytorch.org/whl/cpu && \
    pip install --no-cache-dir transformers fastapi uvicorn huggingface_hub

COPY app.py .

# HF token for gated model (pass at runtime)
ENV HF_HUB_ENABLE_HF_TRANSFER=1

EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
