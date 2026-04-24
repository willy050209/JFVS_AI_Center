using JFVS_AI_Center.Api.Models;
using JFVS_AI_Center.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));

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
            Language: "auto"
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

app.MapGet("/", () => "JFVS AI Center API is running.");

app.Run();
