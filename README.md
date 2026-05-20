# JFVS AI Center

JFVS AI Center 是一個基於 **.NET 10.0** 與 **C# 14** 構建的全方位 AI 整合伺服器。本專案專為技術型高中設計，採用現代化架構，整合了大型語言模型 (LLM) 對話、物聯網 (MQTT) 設備控制、OpenVINO 加速的語音轉文字 (STT) 以及高品質本機語音合成 (TTS) 功能。

## 🏗️ 系統架構

本專案採用解耦的領域驅動設計 (DDD) 概念，結構如下：

- **Web (src/Web)**：基於 Minimal API 的高效能進入點，負責路由配置、Scalar UI 整合。
- **Services (src/Services)**：核心業務邏輯層，包含 AI 對話流程控管 (`AiService`)、設備控制邏輯 (`DeviceControlService`) 與景點資訊服務 (`SceneService`)。
- **Infrastructure (src/Infrastructure)**：技術基礎建設層，處理底層 MQTT 通訊 (`MqttClientService`)、OpenVINO 裝置偵測、Whisper 推論與 Piper 語音合成。採用 `IResourceInitializer` 模式進行環境檢查與模型預熱，由 `ModelManagerService` 統一協調啟動流程。
- **Models (src/Models)**：採用 C# Record 實作的不可變資料模型與 DTO。

## 🚀 核心功能

- **AI 智能聊天 (`POST /chat`)**
  - **多 Session 管理**：支援按 `SessionId` 獨立維護對話紀錄，確保多用戶平行使用的上下文隔離。
  - **本地大腦**：串接本機 Python 微服務 `My-AI-Server` (FastAPI)，利用 OpenVINO™ GenAI 進行本機 LLM 推理，確保所有對話數據保留在校園內部。
  - **Gemma 4 支援**：整合最新 `gemma-4-E4B-it-int4-ov` 條件生成模型，內部採用 `openvino-genai` 的 `VLMPipeline` (使用 nightly `2026.3.dev` 開發包) 進行高效對話生成。
  - **工具呼叫 (Tool Calling)**：AI 具備自主決策能力，透過 `DeviceControlService` 與 `SceneService` 自動執行任務。
  - **智能清掃**：內建對話歷史限制，自動優化 Context 視窗以節省運算資源。

- **語音辨識 (STT) (`POST /api/transcribe`)**
  - **OpenVINO™ 加速**：利用 Intel 推論引擎優化 Whisper 模型，支援 NPU、GPU 與 CPU 自動切換。
  - **自適應轉碼**：內建 `AudioConversionService` 搭配 FFmpeg，自動將上傳的音訊轉換為 16kHz, 16-bit PCM 格式。
  - **模型預熱 (Warm-up)**：系統啟動時自動執行背景預熱推論，消除 OpenVINO 首次執行的編譯延遲，達成「開箱即用」的零延遲體驗。

- **高品質語音合成 (TTS) (`GET /api/tts`)**
  - **Piper 引擎**：整合官方 Piper 離線 TTS，並透過 `AudioFormatUtils` 提供低延遲、高質感的 WAV 音訊。
  - **多引擎支援**：除了 Piper，亦保留了 Windows SAPI TTS 接口，適應不同情境需求。

- **全本機語音對話 (`POST /api/voice-chat`)**
  - **一站式交互**：在單次請求中完成「語音辨識 -> AI 思考 -> 語音合成」完整流程，回傳 Base64 音訊數據。

- **物聯網控制 (IoT Control)**
  - **意圖預判**：內建 `FastIntentMatcher` 靜態純函數，在進入大腦推論前快速識別簡單的開關指令。
  - **職責分離**：業務邏輯 (`DeviceControlService`) 與底層通訊 (`MqttClientService`) 完全解耦，具備自動重連與斷線緩衝。

## 🛠️ 開發環境需求

- **.NET 10.0 SDK** (或更高版本)。
- **Python 3.10+** (用於運行 `My-AI-Server`，C# 啟動時會自動建立虛擬環境與下載依賴)。
- **Docker** (選配，用於容器化部署)。
- **硬體推薦**：支援 Intel OpenVINO 的處理器或具備專屬 NPU 的設備。

## ⚙️ 配置說明

### 核心設定 (`appsettings.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Mqtt": {
    "Host": "broker.emqx.io",
    "Port": 1883,
    "Username": "jfvs000",
    "Password": "jfvs000"
  },
  "Ai": {
    "Endpoint": "http://127.0.0.1:8000/v1",
    "Model": "OpenVINO/gemma-4-E4B-it-int4-ov",
    "ApiKey": "local-server"
  }
}
```

## 🏁 快速開始

1. **編譯專案**：
   ```powershell
   dotnet build
   ```
2. **執行伺服器** (啟動時會自動在背景初始化 `My-AI-Server` 執行環境並載入模型)：
   ```powershell
   dotnet run --project JFVS_AI_Center.Api/JFVS_AI_Center.Api.csproj
   ```
3. **存取 API 文件 (Scalar UI)**：
   打開瀏覽器至 `http://localhost:5000/scalar/v1`

## 📦 技術棧

- **Backend Runtime**: .NET 10.0 (Win-x64)
- **Backend Framework**: ASP.NET Core Minimal API
- **Python Inference Server**: FastAPI + Uvicorn
- **AI/ML**: OpenVINO GenAI (`VLMPipeline` / `LLMPipeline` 雙模支援), Whisper.net, Piper TTS
- **Communication**: MQTTnet
- **Media**: Xabe.FFmpeg
