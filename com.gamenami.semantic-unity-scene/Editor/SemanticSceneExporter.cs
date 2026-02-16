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
            for (int i = 0; i < 32; i++) {
                layerNames[i] = LayerMask.LayerToName(i);
            }
            _excludeLayers = EditorGUILayout.MaskField("Layers to Exclude", _excludeLayers, layerNames);

            if (!GUILayout.Button("Export to JSON")) return;
            
            var activeScene = SceneManager.GetActiveScene();
            var sceneName = string.IsNullOrEmpty(activeScene.name) ? "UntitledScene" : activeScene.name;
            var json = ExportActiveScene(activeScene, sceneName);
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

        private string ExportActiveScene(Scene activeScene, string sceneName) 
        {
            var sceneData = new SemanticScene
            {
                sceneName = sceneName,
                sceneContext = "Each entry in the JSON represents a single interactable entity. " +
                               "To interact with a unit or obstacle, use the viewportPos of its root node.",
                // Initialize layer statistics if toggled true
                layerCounts = _includeLayerStats ? new Dictionary<string, int>() : null
            };

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var root in rootObjects) 
            {
                AddNodesRecursively(root, sceneData, null, 0);
            }
            
            var jsonSettings = new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore // To stop Vector3.normalized infinite loop
            };

            return JsonConvert.SerializeObject(sceneData, jsonSettings);
        }


        private void AddNodesRecursively(GameObject obj, SemanticScene scene, string parentPath, int currentDepth) 
        {
            // --- OPTIMIZATIONS ---
            // Ignore disabled objects and their entire children sub-hierarchy
            if (!obj.activeSelf) return; 
            
            // Check if the object's layer bit is toggled in exclusion mask
            if (((1 << obj.layer) & _excludeLayers) != 0) return;
            
            // Stop if _maxDepth exceeded
            if (currentDepth > _maxDepth) return;
            
            // Prune branch traversal if we hit a SkinnedMeshRenderer (Character Rig)
            // This ignores all bones, joints, and target points inside the character
            if (obj.GetComponent<SkinnedMeshRenderer>()) return;
            
            
            // Layer STATISTICS for debugging: Count objects per layer
            var layerName = LayerMask.LayerToName(obj.layer);
            if (_includeLayerStats)
            {
                scene.layerCounts.TryAdd(layerName, 0);
                scene.layerCounts[layerName]++;
            }
            
            // --- GENERALIZABLE CULLING LOGIC ---
            bool isVisible = false;
            SimpleVec2? vPos = null;
            
            var mainCamera = Camera.main ? Camera.main : SceneView.lastActiveSceneView.camera;
            if (mainCamera) {
                Vector3 viewPoint = mainCamera.WorldToViewportPoint(obj.transform.position);
        
                // Is it actually in front of the camera and within the frame?
                if (viewPoint is { z: > 0, x: >= 0 and <= 1, y: >= 0 and <= 1 }) {
                    // Unity Viewport Y is 0 at bottom, but Vision Models/Web usually expect 0 at top.
                    // Let's flip it to be "Standard Image Space" (0,0 is Top-Left)
                    isVisible = true;
                    vPos = new SimpleVec2(viewPoint.x, 1f - viewPoint.y);
                }
            }
            
            // If it's a "Grid Tile" but NOT visible, skip it. 
            // This allows the LLM to see the 100 tiles on screen but ignore the 2,400 off-screen.
            if (layerName == "Grid Tiles" && !isVisible) return;
            
            // Build the breadcrumb path
            var currentPath = string.IsNullOrEmpty(parentPath) ? obj.name : $"{parentPath}/{obj.name}";

            // Use heuristics to determine if an object should be included
            if (HeuristicFilters.IsGameplayObject(obj))
            {
                var node = new SemanticNode {
                    name = obj.name,
                    path = currentPath,
                    viewportPos = vPos,
                };

                // For Editor time work such as changing object placements
                if (_includeTransforms)
                {
                    node.layer = layerName;
                    node.position = obj.transform.position;
                    node.rotation = obj.transform.eulerAngles;
                    node.scale = obj.transform.localScale == Vector3.one ? null : obj.transform.localScale; // exclude scale if it is 1.0, 1.0, 1.0
                }

                if (_includeComponents)
                {
                    var uniqueComponents = new HashSet<string>();
                
                    foreach (var comp in obj.GetComponents<Component>()) 
                    {
                        // Use heuristics to determine if a component gives context to the LLM
                        if (HeuristicFilters.IsFunctionalComponent(comp))
                        {
                            uniqueComponents.Add(comp.GetType().Name);
                        }
                    }
                    
                    // Convert back to List for the SemanticNode (if there are any)
                    if (uniqueComponents.Count > 0)
                    {
                        node.components = new List<string>(uniqueComponents);
                    }
                }
                
                scene.entities.Add(node);
            }
            
            // Continue recursion for child nodes
            foreach (Transform child in obj.transform)
            {
                var newDepth = HeuristicFilters.IsFolderObject(obj) ? currentDepth : currentDepth + 1;
                // Pass the currentPath as the parentPath for the next generation
                AddNodesRecursively(child.gameObject, scene, currentPath, newDepth);
            }
        }
    }
}
