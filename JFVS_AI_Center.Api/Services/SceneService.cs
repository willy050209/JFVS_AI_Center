namespace JFVS_AI_Center.Api.Services;

public interface ISceneService
{
    string GetSceneInfo(string sceneName);
}

public class SceneService : ISceneService
{
    public string GetSceneInfo(string sceneName)
    {
        Console.WriteLine($"[MCP 工具觸發] 正在查詢: {sceneName}");

        if (sceneName.Contains("圖書館") || sceneName.Contains("晨星"))
        {
            return @"
        【晨星圖書館】
        - 特色：紅磚設計、礦業風格。
        - 設施：一樓有創客討論室與懶骨頭區，三樓有安靜自習室。
        - 推薦：三樓深處的「沉思特區」是寫程式卡關的面壁聖地。
        - 備註：嚴禁甜飲，僅限清水。
        ";
        }
        else if (sceneName.Contains("教學樓") || sceneName.Contains("實習大樓"))
        {
            return @"
        【智慧實習大樓】
        - 特色：匯集資訊與電子科，配備 RTX 4070 顯卡電腦。
        - 設施：三樓有機房與 3D 列印機，四樓有選手訓練室。
        - 推薦：三樓空中廊道，通風良好，適合 Debug 時冷靜。
        ";
        }
        else if (sceneName.Contains("校友亭") || sceneName.Contains("思源亭") || sceneName.Contains("校友庭"))
        {
            return @"
        【思源亭（校友亭）】
        - 特色：紅柱綠瓦八角涼亭，風景優美。
        - 傳說：考前摸摸石碑，程式碼就不會報錯。
        - 科技：燈光與風扇已連上物聯網，可由我遠端控制。
        ";
        }
        else
        {
            return "目前沒有這個景點的即時資訊。";
        }
    }
}
