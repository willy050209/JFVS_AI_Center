import queue
import threading
from pathlib import Path
from huggingface_hub import snapshot_download
import openvino_genai as ov_genai
from app.config import settings

class ModelManager:
    def __init__(self):
        self.pipeline = None
        self.active_model = None
        self.active_device = None

    def get_local_path(self, repo_id: str) -> Path:
        """
        Reconstruct a local file-system safe path for storing the Hugging Face repo.
        """
        safe_name = repo_id.replace("/", "--")
        return settings.MODELS_DIR / safe_name

    def is_downloaded(self, repo_id: str) -> bool:
        """
        Check if the model's OpenVINO XML specification exists locally.
        """
        local_path = self.get_local_path(repo_id)
        return (local_path / "openvino_model.xml").exists()

    def download_model(self, repo_id: str) -> Path:
        """
        Download the model repository from Hugging Face Hub if it's not already downloaded.
        """
        local_path = self.get_local_path(repo_id)
        if not self.is_downloaded(repo_id):
            print(f"Downloading model '{repo_id}' to '{local_path}'...")
            snapshot_download(
                repo_id=repo_id,
                local_dir=str(local_path),
                token=settings.HF_TOKEN,
                # Ignore standard large weights format to save space (since we only need OpenVINO IR files)
                ignore_patterns=["*.git*", "*.bin.tmp", "*.pth", "*.pt", "*.safetensors"]
            )
        return local_path

    def load_model(self, repo_id: str, device: str = None) -> str:
        """
        Download and compile the model into the active pipeline on the targeted device.
        """
        if not device:
            device = settings.OPENVINO_DEVICE
        
        # Ensure model is downloaded
        local_path = self.download_model(repo_id)
        
        import json
        config_path = local_path / "config.json"
        is_vlm = False
        if config_path.exists():
            try:
                with open(config_path, "r", encoding="utf-8") as f:
                    config_data = json.load(f)
                    model_type = config_data.get("model_type", "")
                    architectures = config_data.get("architectures", [])
                    if model_type == "gemma4" or any("ConditionalGeneration" in arch for arch in architectures):
                        is_vlm = True
            except Exception as e:
                print(f"Warning: Failed to parse config.json at {config_path}: {e}")

        print(f"Loading OpenVINO model '{repo_id}' on device '{device}'...")
        if is_vlm:
            # Gemma 4 conditional generation models are loaded with VLMPipeline
            self.pipeline = ov_genai.VLMPipeline(str(local_path), device)
        else:
            self.pipeline = ov_genai.LLMPipeline(str(local_path), device)
            
        self.active_model = repo_id
        self.active_device = device
        print(f"Model '{repo_id}' loaded successfully on device '{device}'!")
        return repo_id

    def list_local_models(self) -> list[str]:
        """
        List all models available locally in the models cache directory.
        """
        models = []
        if settings.MODELS_DIR.exists():
            for folder in settings.MODELS_DIR.iterdir():
                if folder.is_dir() and (folder / "openvino_model.xml").exists():
                    models.append(folder.name.replace("--", "/"))
        return models

    def _prepare_generation_config(self, params: dict) -> ov_genai.GenerationConfig:
        """
        Map API parameters to OpenVINO GenAI's GenerationConfig properties.
        """
        config = ov_genai.GenerationConfig()
        
        # Map parameters
        max_tokens = params.get("max_tokens") or params.get("max_new_tokens")
        if max_tokens is not None:
            config.max_new_tokens = int(max_tokens)
            
        temperature = params.get("temperature")
        if temperature is not None:
            temp_val = float(temperature)
            config.temperature = temp_val
            # If temperature is very low, fall back to greedy decoding
            if temp_val <= 0.05:
                config.do_sample = False
            else:
                config.do_sample = True
                
        top_p = params.get("top_p")
        if top_p is not None:
            config.top_p = float(top_p)
            config.do_sample = True
            
        top_k = params.get("top_k")
        if top_k is not None:
            config.top_k = int(top_k)
            config.do_sample = True

        repetition_penalty = params.get("repetition_penalty")
        if repetition_penalty is not None:
            config.repetition_penalty = float(repetition_penalty)

        presence_penalty = params.get("presence_penalty")
        if presence_penalty is not None:
            config.presence_penalty = float(presence_penalty)

        frequency_penalty = params.get("frequency_penalty")
        if frequency_penalty is not None:
            config.frequency_penalty = float(frequency_penalty)

        stop = params.get("stop")
        if stop:
            if isinstance(stop, str):
                config.stop_strings = {stop}
            elif isinstance(stop, list):
                config.stop_strings = set(stop)

        return config

    def generate(self, messages: list[dict], params: dict) -> str:
        """
        Generate a complete non-streaming response.
        """
        if not self.pipeline:
            raise RuntimeError("No model loaded. Please load a model first using /api/models/load.")

        tokenizer = self.pipeline.get_tokenizer()
        prompt = tokenizer.apply_chat_template(messages, add_generation_prompt=True)
        config = self._prepare_generation_config(params)
        
        if isinstance(self.pipeline, ov_genai.VLMPipeline):
            res = self.pipeline.generate(prompt, generation_config=config)
            if hasattr(res, "texts"):
                return res.texts[0]
            return str(res)
        else:
            return self.pipeline.generate(prompt, config)

    def generate_stream(self, messages: list[dict], params: dict):
        """
        Stream generated tokens back in a generator.
        """
        if not self.pipeline:
            raise RuntimeError("No model loaded. Please load a model first using /api/models/load.")

        tokenizer = self.pipeline.get_tokenizer()
        prompt = tokenizer.apply_chat_template(messages, add_generation_prompt=True)
        config = self._prepare_generation_config(params)

        q = queue.Queue()

        # OpenVINO GenAI streamer callback
        def streamer_callback(word: str) -> bool:
            q.put(word)
            return False  # False means continue generation

        def run_generation():
            try:
                if isinstance(self.pipeline, ov_genai.VLMPipeline):
                    self.pipeline.generate(prompt, generation_config=config, streamer=streamer_callback)
                else:
                    self.pipeline.generate(prompt, config, streamer_callback)
            except Exception as e:
                q.put(e)
            finally:
                q.put(None)  # Sentinel token signaling end

        # Run model inference in a background thread to prevent blocking FastAPI's event loop
        thread = threading.Thread(target=run_generation)
        thread.start()

        while True:
            item = q.get()
            if item is None:
                break
            if isinstance(item, Exception):
                raise item
            yield item

model_manager = ModelManager()
