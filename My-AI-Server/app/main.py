from contextlib import asynccontextmanager
from pathlib import Path
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse, FileResponse
from app.config import settings
from app.models import model_manager
from app.routes import router

# Resolve templates directory paths
TEMPLATES_DIR = Path(__file__).parent / "templates"
CHAT_HTML_PATH = TEMPLATES_DIR / "chat.html"


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup Action: Automatically download and load the default model
    print("\n" + "="*50)
    print("         STARTING OPENVINO AI SERVER")
    print("="*50)
    print(f"Host:           {settings.HOST}")
    print(f"Port:           {settings.PORT}")
    print(f"Default Model:  {settings.DEFAULT_MODEL}")
    print(f"Target Device:  {settings.OPENVINO_DEVICE}")
    print(f"Models Dir:     {settings.MODELS_DIR}")
    print("="*50 + "\n")
    
    try:
        # Load the default model on startup so the server is ready for inference immediately
        model_manager.load_model(settings.DEFAULT_MODEL)
    except Exception as e:
        print(f"\n[WARNING] Failed to load default model '{settings.DEFAULT_MODEL}' on startup: {str(e)}")
        print("The server is running, but you must load a model via '/api/models/load' before running queries.\n")
        
    yield
    
    # Shutdown Action: Cleanup resources if necessary
    print("\n" + "="*50)
    print("         SHUTTING DOWN OPENVINO AI SERVER")
    print("="*50 + "\n")

app = FastAPI(
    title="OpenVINO AI Server",
    description="A local, high-performance OpenAI-compatible LLM Server accelerated by Intel OpenVINO.",
    version="1.0.0",
    lifespan=lifespan
)

# Enable CORS for standard web frontend clients (e.g. OpenWebUI, chatbot interfaces)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include Chat & Model management routes
app.include_router(router)

@app.get("/", response_class=HTMLResponse, include_in_schema=False)
async def root():
    """
    Serves the premium web chat UI at the root path of the server.
    """
    if CHAT_HTML_PATH.exists():
        return FileResponse(str(CHAT_HTML_PATH))
    return HTMLResponse(content="<h3>Web Chat UI Template Not Found</h3>", status_code=404)


@app.get("/scalar", response_class=HTMLResponse, include_in_schema=False)
async def get_scalar():
    """
    Renders the modern, highly interactive Scalar API Reference UI using the deepSpace theme.
    """
    html_content = """
    <!doctype html>
    <html>
      <head>
        <title>Scalar API Reference - OpenVINO AI Server</title>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <style>
          body {
            margin: 0;
            background-color: #0b0f19;
          }
        </style>
      </head>
      <body>
        <!-- Configuration options customizes theme and layouts of Scalar UI -->
        <script
          id="api-reference"
          data-url="/openapi.json"
          data-configuration='{"theme": "deepSpace", "layout": "modern", "showSidebar": true}'></script>
        <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
      </body>
    </html>
    """
    return HTMLResponse(content=html_content, status_code=200)
