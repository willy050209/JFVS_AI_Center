using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "JFVS AI Center API", Version = "v1" });
});

builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<IMqttService>(sp => sp.GetRequiredService<MqttService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttService>());

builder.Services.AddSingleton<ISceneService, SceneService>();
builder.Services.AddSingleton<IAiService, AiService>();

builder.Services.AddSingleton<ModelManagerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ModelManagerService>());
builder.Services.AddTransient<AudioConversionService>();
builder.Services.AddSingleton<WhisperInferenceService>();
builder.Services.AddSingleton<ITtsService, TtsService>();
builder.Services.AddSingleton<ISapiTtsService, SapiTtsService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "JFVS AI Center API v1");
});

app.MapPost("/chat", async Task<Results<Ok<ChatResponse>, BadRequest<string>>> ([FromBody] ChatRequest request, [FromServices] IAiService aiService, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return TypedResults.Ok(new ChatResponse { Response = "請輸入對話內容" });
    }

    try
    {
        var responseText = await aiService.ProcessChatAsync(request.Text, request.SessionId);
        return TypedResults.Ok(new ChatResponse { Response = responseText });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat error");
        return TypedResults.Ok(new ChatResponse { Response = "抱歉，目前連線似乎有點問題，請稍後再試。" });
    }
});

app.MapPost("/api/transcribe", async Task<Results<Ok<TranscriptionResponse>, BadRequest<string>, ProblemHttpResult>> (
    IFormFile file,
    AudioConversionService conversionService,
    WhisperInferenceService whisperService,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
    {
        return TypedResults.BadRequest("請提供有效的音訊檔案");
    }

    string? tempWavPath = null;
    try
    {
        var extension = Path.GetExtension(file.FileName);
        using var stream = file.OpenReadStream();

        tempWavPath = await conversionService.ConvertToWavAsync(stream, extension, ct);
        var text = await whisperService.TranscribeAsync(tempWavPath, ct);

        return TypedResults.Ok(new TranscriptionResponse(
            Text: text,
            DurationSeconds: 0,
            Language: "zh"
        ));
    }
    catch (Exception ex)
    {
        return TypedResults.Problem($"發生未預期的錯誤: {ex.Message}");
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

app.MapPost("/api/voice-chat", async Task<Results<Ok<VoiceChatResponse>, BadRequest<string>, ProblemHttpResult>> (
    IFormFile file,
    [FromQuery] string? sessionId,
    AudioConversionService conversionService,
    WhisperInferenceService whisperService,
    IAiService aiService,
    ITtsService ttsService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
    {
        return TypedResults.BadRequest("請提供有效的音訊檔案");
    }

    string? tempWavPath = null;
    try
    {
        var extension = Path.GetExtension(file.FileName);
        using var stream = file.OpenReadStream();

        tempWavPath = await conversionService.ConvertToWavAsync(stream, extension, ct);
        var userText = await whisperService.TranscribeAsync(tempWavPath, ct);

        if (string.IsNullOrWhiteSpace(userText))
        {
            return TypedResults.Ok(new VoiceChatResponse(
                UserText: "",
                AiResponse: "很抱歉，我沒有聽清楚您說的話。",
                AudioBase64: null,
                Status: "empty_speech"
            ));
        }

        var aiResponse = await aiService.ProcessChatAsync(userText, sessionId ?? "voice-session");
        var audioBytes = await ttsService.SynthesizeAsync(aiResponse);
        var audioBase64 = Convert.ToBase64String(audioBytes);

        return TypedResults.Ok(new VoiceChatResponse(
            UserText: userText,
            AiResponse: aiResponse,
            AudioBase64: audioBase64
        ));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Voice Chat error");
        return TypedResults.Problem($"語音處理過程中發生未預期的錯誤: {ex.Message}");
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

app.MapGet("/api/tts", async Task<Results<FileContentHttpResult, BadRequest<string>, ProblemHttpResult>> (
    [FromQuery] string text,
    [FromQuery] string? voice,
    ITtsService ttsService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return TypedResults.BadRequest("請提供需要合成的文字");
    }

    try
    {
        var audioBytes = await ttsService.SynthesizeAsync(text, voice);
        return TypedResults.File(audioBytes, "audio/wav", $"tts_{Guid.NewGuid():N}.wav");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "TTS Standalone error");
        return TypedResults.Problem($"語音合成失敗: {ex.Message}");
    }
});

app.MapGet("/api/tts-sapi", async Task<Results<FileContentHttpResult, BadRequest<string>, ProblemHttpResult>> (
    [FromQuery] string text,
    ISapiTtsService sapiService,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return TypedResults.BadRequest("請提供需要合成的文字");
    }

    try
    {
        var audioBytes = await sapiService.SynthesizeAsync(text);
        return TypedResults.File(audioBytes, "audio/wav", $"sapi_{Guid.NewGuid():N}.wav");
    }
    catch (PlatformNotSupportedException ex)
    {
        logger.LogWarning(ex, "SAPI 不支援容器環境");
        return TypedResults.Problem("Windows SAPI 在容器環境下無法運作，請改用 /api/tts (Piper) 服務。");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SAPI TTS error");
        return TypedResults.Problem($"SAPI 語音合成失敗: {ex.Message}");
    }
});

app.Run();
