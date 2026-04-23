# JFVS AI Center

JFVS AI Center 是一個基於 ASP.NET Core 10.0 的全方位 AI 整合伺服器，專為技術型高中設計，整合了大型語言模型 (LLM) 對話、物聯網 (MQTT) 設備控制以及 OpenVINO 加速的語音轉文字 (STT) 功能。

## 🚀 核心功能

- **AI 智能聊天 (`POST /chat`)**
  - **本地大腦**：串接 LM Studio (支援 OpenAI 相容 API)，保護數據隱私。
  - **工具呼叫 (Tool Calling)**：AI 能自動判斷並呼叫「景點查詢」與「設備控制」工具。
  - **捷徑模式 (Fast Matcher)**：針對開關燈、風扇等高頻指令進行快速攔截與背景處理，提升反應效率。
  - **歷史紀錄管理**：自動維護對話脈絡，並具備長度限制以優化 Token 使用。

- **語音轉文字 (`POST /api/transcribe`)**
  - **高效推論**：整合 Whisper.net，並利用 OpenVINO™ 加速。
  - **硬體適應**：自動偵測並選用最佳裝置 (優先順序：NPU > GPU > CPU)。
  - **自動轉碼**：內建 FFmpeg 自動下載與整合，支援多種音訊格式自動轉為 16kHz WAV。

- **物聯網設備控制**
  - 透過 MQTT 協定控制實體設備 (如 ESP32 控制的燈光與風扇)。
  - 預設連線至 `broker.emqx.io`。

## 🛠️ 系統配置與自訂

### 景點資訊配置 (`scenes.json`)
為了方便維護，所有校園景點資訊已從程式碼中獨立出來。您可以直接修改根目錄下的 `scenes.json`：
- **Keywords**: 觸發該景點資訊的關鍵字列表。
- **Title**: 景點名稱。
- **Content**: AI 回覆時參考的詳細資訊。

### 系統需求
- **.NET 10.0 SDK** (預覽版或正式版)。
- **LM Studio**: 運行於本地 `1234` 埠口。
- **硬體**: 建議使用具備 Intel NPU 或 GPU 的電腦以獲得最佳辨識速度。
- **FFmpeg**: 伺服器首次啟動時會自動下載至執行目錄。

## 🏁 快速開始

1. **環境準備**：確保 LM Studio 已啟動並載入模型。
2. **啟動伺服器**：
   ```powershell
   cd JFVS_AI_Center.Api
   dotnet run
   ```
3. **API 測試**：
   - 瀏覽器打開 `http://localhost:5000/swagger` 即可進入 Swagger 測試介面。
   - 聊天 API: `POST http://localhost:5000/chat` (JSON: `{"text": "你好"}`)
   - 語音 API: `POST http://localhost:5000/api/transcribe` (Multipart Form Data: `file`)

## 📂 專案結構

- `JFVS_AI_Center.Api/`
  - `Services/`: 核心服務 (AI, MQTT, Scene, Whisper, OpenVINO)。
  - `Models/`: 資料模型定義。
  - `scenes.json`: 可自訂的景點資料庫。
  - `Program.cs`: 進入點與路由設定。
  - `Properties/launchSettings.json`: 伺服器埠口與環境變數設定。


