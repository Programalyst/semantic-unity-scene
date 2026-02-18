using Gamenami.SemanticUnityScene;
using UnityEngine;

public class CapturePlayContext : MonoBehaviour
{
    public SceneExportSettings playModeSettings = SceneExportSettings.DefaultPlayMode;
    
    public void CaptureContextForLlm() 
    {
        // 1. Get the Semantic JSON
        string json = SemanticSceneGenerator.Generate(playModeSettings);

        // 2. Take a Screenshot
        ScreenshotTool.Instance.GetScreenshotBytes((bytes) => {
            // 3. Send both to the LLM
            SendToLlm(json, bytes);
        });
    }

    private void SendToLlm(string json, byte[] screenshot)
    {
        
    }
}
