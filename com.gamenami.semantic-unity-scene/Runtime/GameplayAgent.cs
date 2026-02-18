using System.Collections;
using System.Collections.Generic;
using Gamenami.SemanticUnityScene;
using UnityEngine;

namespace Gamenami.SemanticUnityScene
{
    public class GameplayAgent : AgentSingleton<GameplayAgent>
    {
        [Header("LLM Context Settings")] [SerializeField]
        private SceneExportSettings exportSettings = SceneExportSettings.DefaultPlayMode;
        
        [Header("Status")]
        [SerializeField] private bool awaitingResponse;
        
        private const float AGENT_INTERVAL = 1.0f; // Slower interval for LLM processing
        private float _cooldown = 0f;

        private void FixedUpdate()
        {
            _cooldown += Time.fixedDeltaTime;
            if (!(_cooldown >= AGENT_INTERVAL)) return;
            
            _cooldown = 0f;
            TryToAct();
        }

        private void TryToAct()
        {
            if (awaitingResponse) return;
            
            // Use AgentStateRelay. If no game logic is linked, it defaults to 'false'
            if (!AgentStateRelay.CanAgentAct()) return;
            if (AgentStateRelay.IsProcessing()) return;

            awaitingResponse = true;
            CaptureAndSend();
        }

        private void CaptureAndSend()
        {
            // 1. Generate Semantic Scene Representation 
            var sceneJson = SemanticSceneGenerator.Generate(exportSettings);

            // 2. Capture the Screenshot (Vision)
            ScreenshotTool.Instance.GetScreenshotBytes(imageBytes => 
            {
                // 3. Send both to via MPE Bridge
                BridgeRelay.Send(sceneJson, imageBytes);
                awaitingResponse = true;
                
                Debug.Log($"[Agent] Context sent. JSON Size: {sceneJson.Length / 1024}KB.");
            });
        }
    }
}