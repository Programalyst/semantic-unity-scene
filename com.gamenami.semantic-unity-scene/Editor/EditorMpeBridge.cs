using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.MPE;
using UnityEngine;

namespace Gamenami.SemanticUnityScene.Editor
{
    [InitializeOnLoad]
    public static class EditorMpeBridge
    {
        private const string ChannelName = "sus-agent-channel";
        private const string PythonHandshakeUrl = "ws://127.0.0.1:8765";

        static EditorMpeBridge()
        {
            // 1. Ensure the internal ChannelService is running
            if (!ChannelService.IsRunning())
            {
                ChannelService.Start();
            }
            
            // 2. Schedule the handshake
            EditorApplication.delayCall += async () => {
                await SendHandshake();
            };

            // 2. Register a handler for incoming messages
            // This will trigger even in Editor Mode (No Play Mode required)
            ChannelService.GetOrCreateChannel(ChannelName, OnMessageReceived);
            
            Gamenami.SemanticUnityScene.BridgeRelay.OnRequestSendToServer += HandleRuntimeRequest;
        
            Debug.Log($"MPE Bridge Active on {ChannelService.GetAddress()}:{ChannelService.GetPort()}");
        }
        
        private static async System.Threading.Tasks.Task SendHandshake()
        {
            using var ws = new ClientWebSocket();
            
            try {
                await ws.ConnectAsync(new Uri(PythonHandshakeUrl), CancellationToken.None);
                
                // Get the real dynamic port from Unity's API
                int mpePort = ChannelService.GetPort();
                string message = $"{{\"type\": \"mpe_init\", \"port\": {mpePort}}}";
                
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                
                Debug.Log($"[MPE] Sent dynamic port {mpePort} to Semantic Unity Scene agent server.");
            }
            catch (Exception e) {
                Debug.LogWarning($"[MPE] Python handshake failed (is server running?): {e.Message}");
            }
        }

        private static void OnMessageReceived(int clientId, byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            Debug.Log($"[MPE Received] Client {clientId}: {json}");
            var command = JsonConvert.DeserializeObject<dynamic>(json);
            if (command.type == "function_call")
            {
                foreach (var call in command.content)
                {
                    HandleFunctionCall(call);
                }
            }
            else if (command.type == "text")
            {
                Debug.LogWarning($"[MPE] Agent text response: {command.content}");
                AgentCommandRelay.CommandReceived(command.content); // allow GameplayAgent to try to act again. LLM may have decided not to act: hence returned text.
            }
            else 
            {
                Debug.LogWarning($"[MPE] Unknown command type: {command.type}");
            }
        }
        
        private static void HandleRuntimeRequest(string json, byte[] image)
        {
            var payload = new {
                sceneJson = JsonConvert.DeserializeObject(json), // Ensures nested JSON is valid
                b64Image = Convert.ToBase64String(image)
            };
            
            SendToAgent(JsonConvert.SerializeObject(payload));
        }
        
        private static void SendToAgent(string message)
        {
            var channel = ChannelService.GetChannelList();
            foreach (var info in channel)
            {
                if (info.name != ChannelName) continue;
                
                byte[] data = Encoding.UTF8.GetBytes(message);
                ChannelService.Broadcast(info.id, data);
            }
        }
        
        private static void HandleFunctionCall(dynamic call)
        {
            string funcName = call.name;
            var args = call.args;
            var intent = call.args.Intent != null ? (string)call.args.Intent : "No Intent";

            // Wrapping in delayCall ensures the click happens safely on the main thread during the next editor update
            EditorApplication.delayCall += () =>
            {
                switch (funcName)
                {
                    case "click_screen_position":
                    {
                        // Gemini sends 0-1 Viewport coordinates
                        var vx = (float)args.screenX;
                        var vy = (float)args.screenY;
                    
                        AgentCommandRelay.ExecuteScreenClick(ConvertToScreenPosition(vx, vy));
                        break;
                    }
                    case "click_ui_button":
                        AgentCommandRelay.ExecuteButtonClick(args.ButtonName.ToString());
                        break;
                }
                AgentCommandRelay.CommandReceived(intent); // allow GameplayAgent to act again
            };
        }
        
        private static Vector2 ConvertToScreenPosition(float normalizedX, float normalizedY)
        {
            // 1. Flip Y back (Unity Screen/Viewport Y is bottom-up, LLM Y is top-down)
            float correctedY = 1f - normalizedY;

            // 2. Convert 0-1 Viewport to Actual Pixels
            var pixelPosition = new Vector2(
                normalizedX * Screen.width,
                correctedY * Screen.height
            );

            Debug.Log($"Viewport: {normalizedX},{normalizedY} -> Pixels: {pixelPosition}");
            
            return pixelPosition;
        }
    }
}