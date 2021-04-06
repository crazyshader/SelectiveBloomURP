using UnityEngine.Rendering.Universal;
using UnityEngine;

namespace Framework.Rendering
{
    public enum RenderQueueType
    {
        Opaque,
        Transparent,
        All,
    }

    [System.Serializable]
    public class FilterSettings
    {
        public RenderQueueType RenderQueue = RenderQueueType.All;
        public LayerMask LayerMask = 0;
        public string[] PassNames;
    }

    public class SelectiveBloom : ScriptableRendererFeature
    {
        [System.Serializable]
        public class BloomSettings
        {
            public Material BloomMaterial = null;
            public float Threshold = 0.5f;
            public float Intensity = 0.8f;
            public float SoftKnee = 0.5f;
            public float Radius = 2.5f;
            public bool HighQuality = false;
        }

        [System.Serializable]
        public class SelectiveBloomSettings
        {
            public string PassTag = "Selective Bloom";
            public RenderPassEvent Event = RenderPassEvent.BeforeRenderingPostProcessing;
            public FilterSettings FilterSettings = new FilterSettings();
            public BloomSettings BloomSettings = new BloomSettings();
        }

        public SelectiveBloomSettings settings = new SelectiveBloomSettings();
        private SelectiveBloomPass m_RenderObjectsPass;

        public override void Create()
        {
            m_RenderObjectsPass = new SelectiveBloomPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.BloomSettings.BloomMaterial == null)
            {
                Debug.LogWarning($"Selective bloom material is null.");
                return;
            }

            m_RenderObjectsPass.Setup(renderer.cameraColorTarget, renderer.cameraColorTarget);
            renderer.EnqueuePass(m_RenderObjectsPass);
        }
    }
}

