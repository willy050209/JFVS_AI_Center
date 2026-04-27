namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 模型路徑供應器，集中管理所有 AI 模型的檔案路徑。
/// </summary>
public class ModelPathProvider
{
    private readonly string _baseDir = AppContext.BaseDirectory;
    private readonly string _modelFolder;
    private readonly string _piperFolder;

    public ModelPathProvider()
    {
        _modelFolder = Path.Combine(_baseDir, "Models");
        _piperFolder = Path.Combine(_baseDir, "Piper");
    }

    public string ModelFolder => _modelFolder;
    public string PiperFolder => _piperFolder;
    public string BaseDir => _baseDir;

    // Whisper 相關路徑
    public string WhisperModelPath => Path.Combine(_modelFolder, "ggml-base.bin");
    public string WhisperOpenVinoXmlPath => Path.Combine(_modelFolder, "ggml-base-encoder-openvino.xml");
    public string WhisperOpenVinoBinPath => Path.Combine(_modelFolder, "ggml-base-encoder-openvino.bin");

    // Piper 相關路徑
    public string PiperExePath => Path.Combine(_piperFolder, "piper", "piper.exe");
    public string PiperModelPath => Path.Combine(_piperFolder, "zh_CN-huayan-medium.onnx");
    public string PiperJsonPath => Path.Combine(_piperFolder, "zh_CN-huayan-medium.onnx.json");
}
