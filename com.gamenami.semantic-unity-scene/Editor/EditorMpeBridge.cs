using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
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
            string message = Encoding.UTF8.GetString(data);
            Debug.Log($"[MPE Received] Client {clientId}: {message}");
        
            // Example: Process command from your Python Agent
            if (message.Contains("CreateCube"))
            {
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            }
        }

        // Call this to send data back to your Python agent
        public static void SendToAgent(string message)
        {
            var channel = ChannelService.GetChannelList();
            foreach (var info in channel)
            {
                if (info.name != ChannelName) continue;
                
                byte[] data = Encoding.UTF8.GetBytes(message);
                ChannelService.Broadcast(info.id, data);
            }
        }
    }
}