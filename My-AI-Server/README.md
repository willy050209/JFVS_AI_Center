# Intel OpenVINO AI Server

An extremely fast, local, OpenAI-compatible LLM Chat completion server accelerated by **Intel OpenVINO** and built using **FastAPI**. 

This server is designed to run efficiently on Intel hardware (CPU, Integrated GPU, Arc Discrete GPU, and Core Ultra NPU) and features automatic model downloads, streaming chat responses, and interactive Swagger & Scalar documentation.

---

## Features

- 🚀 **Intel OpenVINO Acceleration**: Hardware-optimized model execution via OpenVINO GenAI on CPU, GPU, and NPU.
- 📦 **Automated Downloading**: Simply request a model by its Hugging Face repository name (e.g. `Intel/Qwen2.5-3B-Instruct-int4-ov`), and the server will download, cache, and load it automatically.
- 🔄 **OpenAI-Compatible API**: Implements standard endpoints (`/v1/chat/completions` with streaming, `/v1/models`) making it compatible with frontends like **OpenWebUI**, **LibreChat**, or the official OpenAI SDK.
- 📖 **Beautiful Interactive Docs**: Serves standard Swagger UI (`/docs`) and the premium, dark-themed **Scalar API Reference** (`/scalar`).

---

## Project Structure

```
My-AI-Server/
├── .env                  # Port, default model, and device settings
├── .gitignore            # Excludes caches, venv, and local models
├── requirements.txt      # Python dependencies
├── app/
│   ├── __init__.py       # Package marker
│   ├── config.py         # Loads & parses environmental configurations
│   ├── models.py         # Downloads HF snapshots & manages OpenVINO pipeline
│   ├── routes.py         # Chat & Model management endpoints
│   └── main.py           # FastAPI initialization & documentation UIs
└── README.md             # Setup and developer guide (this file)
```

---

## Configuration (`.env`)

You can customize the server behavior in the `.env` file:

```ini
# Server host and port
HOST=0.0.0.0
PORT=8000

# Default model to load on startup
DEFAULT_MODEL=Intel/Qwen2.5-3B-Instruct-int4-ov

# Hardware target (AUTO, CPU, GPU, NPU)
OPENVINO_DEVICE=AUTO

# Where model files will be saved
MODELS_DIR=./models

# Hugging Face Access Token (Only required for gated models like Llama-3/3.1/3.2)
HF_TOKEN=
```

---

## Getting Started

### 1. Requirements

Ensure you are using Python 3.9 - 3.12 (Python 3.12 is recommended) on a system with Intel hardware.

### 2. Setup Virtual Environment & Install

Create the virtual environment:
```bash
python -m venv .venv
```

Activate the environment (Windows PowerShell):
```powershell
.venv\Scripts\Activate.ps1
```

Install requirements:
```bash
pip install -r requirements.txt
```

### 3. Run the Server

Start the development server with live reload:
```bash
.venv\Scripts\python -m uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

---

## API Documentation

Once the server is running, open your browser to:

- 🎨 **Scalar API Reference**: `http://localhost:8000/scalar` (Recommended: Beautiful, interactive documentation)
- 🛠️ **Swagger UI**: `http://localhost:8000/docs`

---

## API Usage Examples

### 1. Chat Completion (Streaming - SSE)

```bash
curl -X POST http://localhost:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Intel/Qwen2.5-3B-Instruct-int4-ov",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Explain Intel OpenVINO in one sentence."}
    ],
    "stream": true
  }'
```

### 2. Python SDK integration (OpenAI Library)

You can run your code directly against this server using the standard `openai` library:

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:8000/v1",
    api_key="local-server" # Any value works
)

response = client.chat.completions.create(
    model="Intel/Qwen2.5-3B-Instruct-int4-ov",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "What is the capital of France?"}
    ],
    stream=True
)

for chunk in response:
    if chunk.choices[0].delta.content:
        print(chunk.choices[0].delta.content, end="", flush=True)
```

### 3. Manage Models

- **List local/loaded models**:
  ```bash
  curl http://localhost:8000/v1/models
  ```
- **Load or switch active model**:
  ```bash
  curl -X POST http://localhost:8000/api/models/load \
    -H "Content-Type: application/json" \
    -d '{"repo_id": "Intel/Qwen2.5-0.5B-Instruct-int4-ov", "device": "CPU"}'
  ```
- **Pre-download a model in the background**:
  ```bash
  curl -X POST http://localhost:8000/api/models/download \
    -H "Content-Type: application/json" \
    -d '{"repo_id": "Intel/Qwen2.5-0.5B-Instruct-int4-ov"}'
  ```
