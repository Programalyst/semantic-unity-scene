using UnityEngine;

namespace Gamenami.SemanticUnityScene
{
    [System.Serializable]
    public struct SceneExportSettings
    {
        public int maxDepth;
        public bool includeComponents;
        public bool includeTransforms;
        public LayerMask excludeLayers;
        public bool includeLayerStats;
        
        // Default settings for Play Mode
        public static SceneExportSettings DefaultPlayMode => new SceneExportSettings {
            maxDepth = 0,
            includeComponents = true,
            includeTransforms = false,
            excludeLayers = 0,
            includeLayerStats = false
        };
    }
}