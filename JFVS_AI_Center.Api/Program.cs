using JFVS_AI_Center.Api.Models;
using JFVS_AI_Center.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

// Configure JSON options for Minimal APIs to support Chinese characters in output
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "JFVS AI Center API", Version = "v1" });
});

// Register existing services
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<IMqttService>(sp => sp.GetRequiredService<MqttService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttService>());

builder.Services.AddSingleton<ISceneService, SceneService>();
builder.Services.AddSingleton<IAiService, AiService>();

// Register Whisper & OpenVINO services
builder.Services.AddSingleton<ModelManagerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ModelManagerService>());
builder.Services.AddTransient<AudioConversionService>();
builder.Services.AddSingleton<WhisperInferenceService>();
builder.Services.AddSingleton<ITtsService, TtsService>();
builder.Services.AddSingleton<ISapiTtsService, SapiTtsService>();

// Increase upload limit for audio files
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "JFVS AI Center API v1");
});

// --- Chat Endpoint ---
app.MapPost("/chat", async ([FromBody] ChatRequest request, [FromServices] IAiService aiService) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.Ok(new ChatResponse { Response = "請說話。" });
    }

    try
    {
        var responseText = await aiService.ProcessChatAsync(request.Text, request.SessionId);
        return Results.Ok(new ChatResponse { Response = responseText });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Chat error");
        return Results.Ok(new ChatResponse { Response = "本地大腦連線異常，請確認 LM Studio 是否啟動伺服器。" });
    }
});

// --- Transcription Endpoint ---
app.MapPost("/api/transcribe", async (
    IFormFile file,
    AudioConversionService conversionService,
    WhisperInferenceService whisperService,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("未提供音訊檔案。");
    }

    string? tempWavPath = null;
    try
    {
        var extension = Path.GetExtension(file.FileName);
        using var stream = file.OpenReadStream();
        
        // 1. Convert to 16kHz WAV
        tempWavPath = await conversionService.ConvertToWavAsync(stream, extension, ct);

        // 2. Inference
        var text = await whisperService.TranscribeAsync(tempWavPath, ct);

        return Results.Ok(new TranscriptionResponse(
            Text: text,
            DurationSeconds: 0,
            Language: "zh"
        ));
    }
    catch (Exception ex)
    {
        return Results.Problem($"轉錄過程中發生錯誤: {ex.Message}");
    }
    finally
    {
        if (tempWavPath != null && File.Exists(tempWavPath))
        {
            File.Delete(tempWavPath);
        }
    }
})
.DisableAntiforgery();

// --- Voice Chat Endpoint ---
app.MapPost("/api/voice-chat", async (
    IFormFile file,
    [FromQuery] string sessionId,
    AudioConversionService conversionService,
    WhisperInferenceService whisperService,
    IAiService aiService,
    ITtsService ttsService,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("未提供音訊檔案。");
    }

    string? tempWavPath = null;
    try
    {
        var extension = Path.GetExtension(file.FileName);
        using var stream = file.OpenReadStream();
        
        // 1. STT: Convert to 16kHz WAV and Transcribe
        tempWavPath = await conversionService.ConvertToWavAsync(stream, extension, ct);
        var userText = await whisperService.TranscribeAsync(tempWavPath, ct);

        if (string.IsNullOrWhiteSpace(userText))
        {
            return Results.Ok(new VoiceChatResponse(
                UserText: "",
                AiResponse: "我沒聽清楚，可以再說一次嗎？",
                AudioBase64: null,
                Status: "empty_speech"
            ));
        }

        // 2. Chat: Process with AI
        var aiResponse = await aiService.ProcessChatAsync(userText, sessionId ?? "voice-session");

        // 3. TTS: Synthesize Response
        var audioBytes = await ttsService.SynthesizeAsync(aiResponse);
        var audioBase64 = Convert.ToBase64String(audioBytes);

        return Results.Ok(new VoiceChatResponse(
            UserText: userText,
            AiResponse: aiResponse,
            AudioBase64: audioBase64
        ));
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Voice Chat error");
        return Results.Problem($"語音對話過程中發生錯誤: {ex.Message}");
    }
    finally
    {
        if (tempWavPath != null && File.Exists(tempWavPath))
        {
            File.Delete(tempWavPath);
        }
    }
})
.DisableAntiforgery();

// --- Standalone TTS Endpoint ---
app.MapGet("/api/tts", async (
    [FromQuery] string text,
    [FromQuery] string? voice,
    ITtsService ttsService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return Results.BadRequest("未提供文字內容。");
    }

    try
    {
        var audioBytes = await ttsService.SynthesizeAsync(text, voice);
        return Results.File(audioBytes, "audio/wav", $"tts_{Guid.NewGuid():N}.wav");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "TTS Standalone error");
        return Results.Problem($"語音合成失敗: {ex.Message}");
    }
});

// --- SAPI TTS Endpoint ---
app.MapGet("/api/tts-sapi", async (
    [FromQuery] string text,
    ISapiTtsService sapiService) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return Results.BadRequest("未提供文字內容。");
    }

    try
    {
        var audioBytes = await sapiService.SynthesizeAsync(text);
        return Results.File(audioBytes, "audio/wav", $"sapi_{Guid.NewGuid():N}.wav");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "SAPI TTS error");
        return Results.Problem($"SAPI 語音合成失敗: {ex.Message}");
    }
});

app.MapGet("/", () => "JFVS AI Center API is running.");

app.Run();
