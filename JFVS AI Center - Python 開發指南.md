# **JFVS AI Center \- Python 開發者快速上手指南 🚀**

歡迎來到 JFVS AI Center 專案！這是一個高度整合的邊緣運算 (Edge AI) 與物聯網 (IoT) 伺服器。雖然本專案主力是使用 C\# (.NET 10\) 開發，但其背後的「系統架構」與「設計模式」是跨語言通用的。

這份指南專為熟悉 Python 的開發者所寫。我們將拆解系統底層的核心技術與算法，並提供 Python 生態系的對應做法，幫助你秒懂專案邏輯，甚至具備用 Python 重寫微服務的能力。

## **第一部分：專案核心技術與算法拆解**

整個系統可以想像成一個擁有大腦、耳朵、嘴巴與神經網路的數位機器人。以下是它的五大器官：

### **1\. 核心大腦：AI Agent 與對話狀態管理 (AiService)**

* **技術核心**：基於大型語言模型 (LLM) 的 **Function Calling (工具呼叫)** 機制。  
* **設計邏輯**：  
  * **會話隔離 (Session Management)**：Web 伺服器是無狀態的。為了記住不同使用者的對話，我們在記憶體中使用 ConcurrentDictionary (類似執行緒安全的 Python dict)，以 SessionId 為 Key 來儲存對話陣列。為節省 Token，系統只會保留最近的 15 筆訊息。  
  * **LLM 路由**：將使用者的文字加上可用工具清單 (如 JSON Schema 格式的 get\_scene\_info, control\_device) 送給模型。模型會自己決定是要直接回答，還是回傳一個「呼叫工具」的指令。  
* **關鍵算法：捷徑攔截器 (Fast Intent Matcher)**  
  * **痛點**：呼叫 LLM 推論至少需要 1\~3 秒。如果使用者只是想「開燈」，不應該等這麼久。  
  * **算法實作**：使用啟發式字串匹配 (Heuristic String Matching)。我們設計了**前向否定詞偵測 (Lookbehind Negation)**，當偵測到「燈」與「開」時，會往前回溯 2 個字元檢查是否有「不、別、沒」等否定詞（避免「不准開燈」被誤判）。匹配成功則直接回覆，並在背景 (Task.Run) 非同步觸發硬體。

### **2\. 聽覺感知：語音轉文字 (WhisperInferenceService)**

* **技術核心**：**Whisper.cpp** \+ **OpenVINO (Intel 硬體加速)** \+ **FFmpeg (音訊前處理)**。  
* **設計邏輯**：  
  * **音訊正規化**：AI 模型只接受特定格式。我們第一步一定是呼叫 FFmpeg 將任何上傳的錄音檔強制轉為 16kHz, 16-bit, 單聲道 PCM WAV。  
  * **動態硬體探測算法**：為了最大化壓榨硬體效能，系統啟動時會透過 C-API 尋找本機裝置，並嚴格按照 **NPU (神經處理單元) \-\> GPU (顯卡) \-\> CPU** 的順序分配算力。  
  * **延遲優化**：我們將 Whisper 參數強制綁定為中文 ("zh")，省去模型猜測語言的時間。

### **3\. 語音表達：文字轉語音 (TtsService)**

* **技術核心**：**Piper TTS (神經網絡語音)**。  
* **設計邏輯**：  
  * **跨行程通訊 (IPC) 封裝**：Piper 是一個獨立的執行檔 (.exe)。我們透過 Process 啟動它，將要念的文字寫入標準輸入 (stdin)，然後即時從標準輸出 (stdout) 讀取生成的裸音訊資料 (Raw PCM)。  
  * **音訊封裝算法**：因為 Piper 產出的是沒有檔頭的波形數據，程式中實作了二進位寫入邏輯，手動計算並補上標準的 WAV (RIFF) Header，讓前端瀏覽器可以直接播放。

### **4\. 物聯網神經：MQTT 設備控制 (DeviceControlService & MqttClientService)**

* **技術核心**：**MQTT (輕量級發布/訂閱協定)** 與 **職責分離原則**。  
* **設計邏輯**：  
  * **底層通訊層 (MqttClientService)**：負責純粹的 MQTT 連線、斷線重連與訊息發布。這是一個背景長連線服務 (IHostedService)，伺服器啟動時即連線並保持，具備自動重連機制。
  * **業務控制層 (DeviceControlService)**：負責處理設備對應邏輯（例如將「燈」對應到特定 Topic）與動作解析，並決定回饋給使用者的文字。
  * **優點**：當你想換成不同的 MQTT Broker 或甚至是改用 HTTP 控制時，你只需要修改或替換底層通訊層，而不需要動到 AI 工具呼叫的業務邏輯。

## **第二部分：Python 生態系替代方案對照表**

如果你要在 Python 中實作上述功能，可以使用以下對應的神兵利器：

| 專案領域 | .NET (本專案使用) | Python 推薦替代方案 | 說明與建議 |
| :---- | :---- | :---- | :---- |
| **Web 框架** | ASP.NET Core Minimal APIs | **FastAPI** | Python 最強的非同步 API 框架，原生支援 async/await，且自帶 Swagger UI，寫法和 Minimal API 極度相似。 |
| **LLM 串接** | OpenAI 官方 SDK | **openai** (Python) 或 **LangChain** | 直接使用 Python 版 openai 套件即可無縫連接 LM Studio。 |
| **對話狀態** | ConcurrentDictionary | 內建 **dict** \+ **asyncio.Lock()** | FastAPI 底層是非同步事件迴圈，若涉及多併發寫入，請記得加上非同步鎖。 |
| **語音轉文字** | Whisper.net \+ OpenVINO | **faster-whisper** | 基於 CTranslate2，效能極高。若需 Intel 顯卡加速，可改用 openvino-whisper。 |
| **文字轉語音** | Piper TTS (.exe) \+ Utils | **piper-tts** \+ **pyttsx3** | Piper 有官方的 Python package，不需要像 C\# 那樣辛苦處理 IPC 和 WAV Header。 |
| **MQTT 通訊** | MQTTnet | **aiomqtt** (或 paho-mqtt) | 推薦 aiomqtt，完美整合 Python asyncio。建議也採解耦設計：一個 Class 跑 Client，一個 Class 跑 Logic。 |
| **音訊轉檔** | Xabe.FFmpeg | **ffmpeg-python** 或 **pydub** | pydub 處理極簡：AudioSegment.from\_file().set\_frame\_rate(16000).export() |
| **背景服務** | IHostedService | **FastAPI @app.lifespan** | 在 FastAPI lifespan 中啟動 MQTT 連線，關閉時斷線，行為等同 C\#。 |

## **第三部分：Python 視角對照範例**

為了讓你秒懂本專案最複雜的 /api/voice-chat 路由，這裡是使用 **FastAPI** 寫出的等價邏輯。你看，思維模式是一模一樣的：

``` python**

from fastapi import FastAPI, UploadFile, File
import asyncio
# 假設以下是你自定義的 Python services
from services import audio_converter, whisper_service, ai_service, piper_tts

app = FastAPI()

@app.post("/api/voice-chat")
async def voice_chat(
    file: UploadFile = File(...), 
    session_id: str = "default"
):
    try:
        # 1. 存檔與音訊轉碼 (對應 AudioConversionService)
        # 將任意格式轉換為 16kHz WAV
        wav_path = await audio_converter.convert_to_16k_wav(file)
        
        # 2. 語音轉文字 STT (對應 WhisperInferenceService)
        user_text = await whisper_service.transcribe(wav_path)
        
        if not user_text:
            return {"UserText": "", "AiResponse": "我沒聽清楚，可以再說一次嗎？", "Status": "empty_speech"}
        
        # 3. 處理 AI 邏輯與工具呼叫 (對應 AiService)
        # 包含捷徑攔截、歷史紀錄管理與 MQTT 控制
        ai_response = await ai_service.process_chat(user_text, session_id)
        
        # 4. 文字轉語音 TTS (對應 TtsService)
        # 直接回傳 Base64 讓前端不用再發起一次請求
        audio_base64 = await piper_tts.synthesize_to_base64(ai_response)
        
        return {
            "UserText": user_text,
            "AiResponse": ai_response,
            "AudioBase64": audio_base64,
            "Status": "success"
        }
    
    except Exception as e:
        return {"error": str(e), "Status": "error"}

```
祝你在探索 JFVS AI Center 的過程中學習愉快！有任何想法都可以隨時動手將它轉化為 Python 實作。