namespace JFVS_AI_Center.Api.Models;

public class AiOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:1234/v1";
    public string Model { get; set; } = "local-model";
    public string ApiKey { get; set; } = "lm-studio";
}
