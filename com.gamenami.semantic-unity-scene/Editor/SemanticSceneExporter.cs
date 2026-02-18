using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement; // Don't use Editor scene management so it works during runtime

namespace Gamenami.SemanticUnityScene.Editor
{
    public class SemanticSceneExporter : EditorWindow 
    {
        // Can edit in Editor Window
        private int _maxDepth = 1;
        private bool _includeComponents = true;
        private bool _includeTransforms = false;
        private LayerMask _excludeLayers = 0;
        private bool _includeLayerStats = true; // for debugging if we have too many objects

        [MenuItem("Tools/Export Semantic Scene")]
        public static void ShowWindow() 
        {
            GetWindow<SemanticSceneExporter>("Scene Exporter");
        }

        private void OnGUI() 
        {
            GUILayout.Label("Semantic Scene Exporter", EditorStyles.boldLabel);
            _maxDepth = EditorGUILayout.IntField("Max Child Depth", _maxDepth);
            _includeComponents = EditorGUILayout.Toggle("Include Components", _includeComponents);
            _includeTransforms = EditorGUILayout.Toggle("Include Transforms", _includeTransforms);
            _includeLayerStats  = EditorGUILayout.Toggle("Include Layer stats", _includeLayerStats);
            
            // Get all defined layer names in Unity
            string[] layerNames = new string[32];
            for (var i = 0; i < 32; i++) 
            {
                layerNames[i] = LayerMask.LayerToName(i);
            }
            _excludeLayers = EditorGUILayout.MaskField("Layers to Exclude", _excludeLayers, layerNames);

            if (!GUILayout.Button("Export to JSON")) return;
            
            var activeScene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrEmpty(activeScene.name) ? "UntitledScene" : activeScene.name;
            var json = SemanticSceneGenerator.Generate(new SceneExportSettings
            {
                includeComponents = _includeComponents,
                includeTransforms = _includeTransforms,
                excludeLayers = _excludeLayers,
                includeLayerStats = _includeLayerStats
            });
            Debug.Log("Scene Exported (Max Depth: " + _maxDepth + ")");
            
            // Save to a file
            var path = EditorUtility.SaveFilePanel(
                "Save Semantic Scene", 
                "", 
                $"{sceneName}.json", 
                "json"
            );
                
            if (string.IsNullOrEmpty(path)) return;
            System.IO.File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }
    }
}
