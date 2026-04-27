# JFVS AI Center

JFVS AI Center 是一個基於 **.NET 10.0** 與 **C# 14** 構建的全方位 AI 整合伺服器。本專案專為技術型高中設計，採用現代化架構，整合了大型語言模型 (LLM) 對話、物聯網 (MQTT) 設備控制、OpenVINO 加速的語音轉文字 (STT) 以及高品質本機語音合成 (TTS) 功能。

## 🏗️ 系統架構

本專案採用解耦的領域驅動設計 (DDD) 概念，結構如下：

- **Web (src/Web)**：基於 Minimal API 的高效能進入點，負責路由配置、Swagger 整合及 Docker 配置。
- **Services (src/Services)**：核心業務邏輯層，包含 AI 對話流程控管與情境劇本服務。
- **Infrastructure (src/Infrastructure)**：技術基礎建設層，處理 MQTT 通訊、OpenVINO 裝置偵測、Whisper 推論、Piper 語音合成及 FFmpeg 音訊轉碼。
- **Models (src/Models)**：採用 C# Record 實作的不可變資料模型與 DTO。

## 🚀 核心功能

- **AI 智能聊天 (`POST /chat`)**
  - **多 Session 管理**：支援按 `SessionId` 獨立維護對話紀錄，確保多用戶平行使用的上下文隔離。
  - **本地大腦**：完美串接 LM Studio (OpenAI 相容 API)，確保所有對話數據均保留在校園內部。
  - **工具呼叫 (Tool Calling)**：AI 具備自主決策能力，能自動呼叫景點資訊查詢與實體設備控制。
  - **智能清掃**：內建對話歷史限制，自動優化 Context 視窗以節省運算資源。

- **語音辨識 (STT) (`POST /api/transcribe`)**
  - **OpenVINO™ 加速**：利用 Intel 推論引擎優化 Whisper 模型，支援 NPU、GPU 與 CPU 自動切換。
  - **自適應轉碼**：內建 `AudioConversionService` 搭配 FFmpeg，自動將上傳的音訊轉換為 16kHz, 16-bit PCM 格式。

- **高品質語音合成 (TTS) (`GET /api/tts`)**
  - **Piper 引擎**：整合官方 Piper 離線 TTS，提供低延遲、高質感的中文女聲。
  - **多引擎支援**：除了 Piper，亦保留了 Windows SAPI TTS 接口，適應不同情境需求。

- **全本機語音對話 (`POST /api/voice-chat`)**
  - **一站式交互**：在單次請求中完成「語音辨識 -> AI 思考 -> 語音合成」完整流程，回傳 Base64 音訊數據。

- **物聯網控制 (IoT Control)**
  - **意圖預判**：內建 `FastIntentMatcher` 靜態純函數，在進入大腦推論前快速識別簡單的開關指令。
  - **強健連線**：基於 MQTTnet 實作的長連線服務，具備自動重連與斷線緩衝。

## 🛠️ 開發環境需求

- **.NET 10.0 SDK** (或更高版本)。
- **Docker** (選配，用於容器化部署)。
- **LM Studio**：建議運行於本地 1234 埠口。
- **硬體推薦**：支援 Intel OpenVINO 的處理器或具備專屬 NPU 的設備。

## ⚙️ 配置說明

### 核心設定 (`appsettings.json`)
```json
{
  "Mqtt": {
    "Host": "broker.emqx.io",
    "Port": 1883,
    "Username": "",
    "Password": ""
  },
  "Ai": {
    "Endpoint": "http://127.0.0.1:1234/v1",
    "Model": "local-model",
    "ApiKey": "lm-studio"
  }
}
```

## 🏁 快速開始

1. **編譯專案**：
   ```powershell
   dotnet build
   ```
2. **執行伺服器**：
   ```powershell
   dotnet run --project JFVS_AI_Center.Api/JFVS_AI_Center.Api.csproj
   ```
3. **存取 Swagger UI**：
   打開瀏覽器至 `http://localhost:5000/swagger`

## 📦 技術棧

- **Runtime**: .NET 10.0 (Win-x64)
- **Framework**: ASP.NET Core Minimal API
- **AI/ML**: Whisper.net, OpenVINO, Piper TTS
- **Communication**: MQTTnet
- **Media**: Xabe.FFmpeg


