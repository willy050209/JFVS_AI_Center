# JFVS AI Center

JFVS AI Center 是一個基於 ASP.NET Core 10.0 的全方位 AI 整合伺服器，整合了大型語言模型 (LLM) 對話、物聯網 (MQTT) 控制以及語音轉文字 (STT) 功能。

## 核心功能

- **AI 聊天服務 (`POST /chat`)**
  - 串接 LM Studio (本地 OpenAI 相容介面)。
  - 支援工具呼叫 (Tool Calling)，包含景點查詢與設備控制。
  - 具備捷徑指令辨識 (Fast Intent Matcher)，優化開關燈與風扇之反應速度。

- **語音辨識服務 (`POST /api/transcribe`)**
  - 整合 Whisper.net 引擎。
  - 使用 OpenVINO 加速，支援 Intel NPU、GPU 與 CPU。
  - 自動處理音訊轉碼 (16kHz WAV)。

- **物聯網控制**
  - 使用 MQTTnet 套件，透過 MQTT 協定控制實體設備。

## 系統需求

- .NET 10.0 SDK
- LM Studio (運行於 `1234` 埠口)
- Intel 處理器 (推薦，以發揮 OpenVINO 效能)
- FFmpeg (伺服器啟動時會自動下載)

## 快速開始

1. 確保已安裝 .NET 10 SDK。
2. 啟動 LM Studio 並載入一個模型，開啟 Local Server。
3. 進入專案目錄並啟動伺服器：

```powershell
cd JFVS_AI_Center.Api
dotnet run
```

4. 訪問 Swagger 介面測試 API：`http://localhost:5000/swagger`

## 專案結構

- `JFVS_AI_Center.Api/`
  - `Services/`: 包含 AI、MQTT、Whisper、OpenVINO 等核心邏輯。
  - `Models/`: 定義 API 資料模型。
  - `Program.cs`: 伺服器啟動設定與路由。
