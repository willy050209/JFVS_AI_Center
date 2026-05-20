import os
from pathlib import Path
from dotenv import load_dotenv

# Load configuration from environment variables / .env file
load_dotenv()

class Settings:
    HOST: str = os.getenv("HOST", "0.0.0.0")
    PORT: int = int(os.getenv("PORT", "8000"))
    DEFAULT_MODEL: str = os.getenv("DEFAULT_MODEL", "Intel/Qwen2.5-3B-Instruct-int4-ov")
    OPENVINO_DEVICE: str = os.getenv("OPENVINO_DEVICE", "AUTO")
    
    # Resolve absolute path for models directory
    MODELS_DIR: Path = Path(os.getenv("MODELS_DIR", "./models")).resolve()
    
    # Hugging Face access token for gated models
    HF_TOKEN: str | None = os.getenv("HF_TOKEN") or None

    def __init__(self):
        # Ensure models directory exists
        self.MODELS_DIR.mkdir(parents=True, exist_ok=True)

settings = Settings()
