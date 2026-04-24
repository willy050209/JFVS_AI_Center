# JFVS AI Center

JFVS AI Center 是一個基於 ASP.NET Core 10.0 的全方位 AI 整合伺服器，專為技術型高中設計，整合了大型語言模型 (LLM) 對話、物聯網 (MQTT) 設備控制、OpenVINO 加速的語音轉文字 (STT) 以及高品質本機語音合成 (TTS) 功能。

## 🚀 核心功能

- **AI 智能聊天 (`POST /chat`)**
  - **多 Session 管理**：支援按 `SessionId` 獨立維護對話紀錄，適合多用戶同時使用。
  - **本地大腦**：串接 LM Studio (支援 OpenAI 相容 API)，保護數據隱私。
  - **工具呼叫 (Tool Calling)**：AI 能自動判斷並呼叫「景點查詢」與「設備控制」工具。
  - **歷史紀錄管理**：自動維護對話脈絡，並具備長度限制以優化 Token 使用。

- **語音轉文字 (`POST /api/transcribe`)**
  - **高效推論**：整合 Whisper.net，並利用 OpenVINO™ 加速。
  - **硬體適應**：自動偵測並選用最佳裝置 (優先順序：NPU > GPU > CPU)。
  - **自動轉碼**：內建 FFmpeg 自動下載與整合，支援多種音訊格式自動轉為 16kHz WAV。

- **高品質本機 TTS (`GET /api/tts`)**
  - **完全離線**：基於 Piper TTS 引擎，不需網路連接即可生成高品質語音。
  - **優質語音**：預載 `zh_CN-huayan-medium` 模型，提供自然流暢的中文女聲（支援繁體輸入）。
  - **自動部署**：系統啟動時會自動下載 Piper 引擎與模型檔案。

- **Windows SAPI TTS (`GET /api/tts-sapi`)**
  - **零依賴**：使用 Windows 系統內建的 Speech API，無需額外模型。
  - **極速回應**：適合簡單的系統通知與輕量級應用。

- **超低延遲語音對話 (`POST /api/voice-chat`)**
  - **一條龍整合**：結合 STT、AI 聊天與 Piper TTS，實現全本機的語音互動流程。
  - **低延遲優化**：Whisper 固定中文模式並使用 Greedy Decoding，確保辨識速度。
  - **Base64 回傳**：結果直接以 Base64 編碼隨 JSON 回傳，方便前端即時播放。

- **物聯網設備控制**
  - **進階捷徑模式 (Fast Matcher)**：針對設備控制指令進行快速攔截，具備否定詞偵測。
  - **MQTT 整合**：採用單一長連線模式，內建自動重連機制。

## 🛠️ 系統配置與自訂

### 核心設定 (`appsettings.json`)
您可以在此檔案中配置 MQTT 伺服器與本地 AI 模型資訊：

```json
{
  "Mqtt": {
    "Host": "broker.emqx.io",
    "Port": 1883,
    "Username": "your_user",
    "Password": "your_password"
  },
  "Ai": {
    "Endpoint": "http://127.0.0.1:1234/v1",
    "Model": "local-model",
    "ApiKey": "lm-studio"
  }
}
```

### 景點資訊配置 (`scenes.json`)
為了方便維護，所有校園景點資訊已從程式碼中獨立出來。您可以直接修改此檔案來新增或修改景點。

### 系統需求
- **.NET 10.0 SDK (Windows)**。
- **LM Studio**: 運行於本地，預設為 `1234` 埠口。
- **硬體**: 建議使用具備 Intel NPU 或 GPU 的電腦以獲得最佳辨識速度。
- **自動下載**: 伺服器首次啟動時會自動下載 FFmpeg、Piper 引擎及語音模型。

## 🏁 快速開始

1. **環境準備**：確保 LM Studio 已啟動並載入模型。
2. **啟動伺服器**：
   ```powershell
   cd JFVS_AI_Center.Api
   dotnet run
   ```
3. **API 測試**：
   - 瀏覽器打開 `http://localhost:5000/swagger` 即可進入測試介面。
   - **聊天**: `POST /chat`
   - **STT**: `POST /api/transcribe` (Multipart Form Data)
   - **Piper TTS**: `GET /api/tts?text=你好`
   - **SAPI TTS**: `GET /api/tts-sapi?text=你好`
   - **整合對話**: `POST /api/voice-chat` (Multipart Form Data)

## 📦 主要使用的 NuGet 套件

- **`Whisper.net` & `Whisper.net.Runtime.OpenVino`**: 語音辨識與 OpenVINO 加速。
- **`System.Speech`**: Windows SAPI TTS 支援。
- **`OpenAI`**: 串接 OpenAI 相容 API (如 LM Studio)。
- **`MQTTnet`**: 高效 MQTT 通訊。
- **`Xabe.FFmpeg`**: 音訊轉碼處理。

## 📂 專案結構

- `JFVS_AI_Center.Api/`
  - `Services/`: 核心服務 (AI, MQTT, Scene, Whisper, Piper TTS, SAPI TTS)。
  - `Piper/`: Piper 引擎與模型存放目錄（自動生成）。
  - `Models/`: 資料模型與 Options 類別。
  - `scenes.json`: 景點資料庫。
