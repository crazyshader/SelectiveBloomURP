using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/*
 * Blit Renderer Feature                                                https://github.com/Cyanilux/URP_BlitRenderFeature
 * ------------------------------------------------------------------------------------------------------------------------
 * Based on the Blit from the UniversalRenderingExamples
 * https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses
 * 
 * Extended to allow for :
 * - Specific access to selecting a source and destination (via current camera's color / texture id / render texture object
 * - Automatic switching to using _AfterPostProcessTexture for After Rendering event, in order to correctly handle the blit after post processing is applied
 * - Setting a _InverseView matrix (cameraToWorldMatrix), for shaders that might need it to handle calculations from screen space to world.
 *     e.g. reconstruct world pos from depth : https://twitter.com/Cyanilux/status/1269353975058501636 
 * ------------------------------------------------------------------------------------------------------------------------
 * @Cyanilux
*/
public class CustomBlit : ScriptableRendererFeature {

    static class ShaderConstants
    {
        public static int _BloomBaseTex;
        public static int[] _BloomMipUp;
        public static int[] _BloomMipDown;
    }

    public class BlitPass : ScriptableRenderPass {

        public Material blitMaterial = null;
        public FilterMode filterMode { get; set; }
        
        private BlitSettings settings;

        private RenderTargetIdentifier source { get; set; }
        private RenderTargetIdentifier destination { get; set; }

        RenderTargetHandle m_PrefilteredColorTexture;

        RenderTargetHandle m_TemporaryColorTexture;
        RenderTargetHandle m_DestinationTexture;
        string m_ProfilerTag;

        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        private ProfilingSampler m_ProfilingSampler;
        private RenderQueueType m_RenderQueueType;
        private FilteringSettings m_FilteringSettings;

        private const int m_MaxIterations = 16;
        private readonly GraphicsFormat m_DefaultHDRFormat;
        private readonly bool m_UseRGBM = true;

        private Vector2[] m_BloomTextureSize;

        public BlitPass(RenderPassEvent renderPassEvent, string[] shaderTags, BlitSettings settings, string profilerTag, RenderQueueType renderQueueType, int layerMask)
        {
            this.renderPassEvent = renderPassEvent;
            this.settings = settings;
            blitMaterial = settings.bloomSettings.blitMaterial;
            m_ProfilerTag = profilerTag;

            //if (blitMaterial == null)
            //{
            //    var shader = Shader.Find("Hidden/Universal Render Pipeline/Blit");
            //    blitMaterial = CoreUtils.CreateEngineMaterial(shader);
            //    settings.blitMaterialPassIndex = 0;
            //}

            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            
            m_RenderQueueType = renderQueueType;
            RenderQueueRange renderQueueRange = RenderQueueRange.all;
            switch (renderQueueType)
            {
                case RenderQueueType.Opaque:
                    renderQueueRange = RenderQueueRange.opaque;
                    break;
                case RenderQueueType.Transparent:
                    renderQueueRange = RenderQueueRange.transparent;
                    break;
                case RenderQueueType.All:
                    break;
                default:
                    break;
            }

            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);

            if (shaderTags != null && shaderTags.Length > 0)
            {
                foreach (var passName in shaderTags)
                    m_ShaderTagIdList.Add(new ShaderTagId(passName));
            }
            else
            {
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }

            m_TemporaryColorTexture.Init("_TemporaryColorTexture");
            m_PrefilteredColorTexture.Init("_PrefilteredColorTexture");

            if (settings.dstType == Target.TextureID) {
                m_DestinationTexture.Init(settings.dstTextureId);
            }

            // Texture format pre-lookup
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render))
            {
                m_DefaultHDRFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                m_UseRGBM = false;
            }
            else
            {
                m_DefaultHDRFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? GraphicsFormat.R8G8B8A8_SRGB
                    : GraphicsFormat.R8G8B8A8_UNorm;
                m_UseRGBM = true;
            }

            m_BloomTextureSize = new Vector2[m_MaxIterations];

            ShaderConstants._BloomBaseTex = Shader.PropertyToID("_BloomBaseTex");

            // Bloom pyramid shader ids - can't use a simple stackalloc in the bloom function as we
            // unfortunately need to allocate strings
            ShaderConstants._BloomMipUp = new int[m_MaxIterations];
            ShaderConstants._BloomMipDown = new int[m_MaxIterations];

            for (int i = 0; i < m_MaxIterations; i++)
            {
                ShaderConstants._BloomMipUp[i] = Shader.PropertyToID("_BloomMipUp" + i);
                ShaderConstants._BloomMipDown[i] = Shader.PropertyToID("_BloomMipDown" + i);
            }
        }

        public void Setup(RenderTargetIdentifier source, RenderTargetIdentifier destination) {
            this.source = source;
            this.destination = destination;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.ClearRenderTarget(false, true, Color.clear);

            //if (m_TemporaryColorTexture == null)
            //{
            //    cmd.GetTemporaryRT(m_TemporaryColorTexture.id, cameraTextureDescriptor, filterMode);
            //}

            //ConfigureTarget(m_TemporaryColorTexture.Identifier());
            //ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {

            SortingCriteria sortingCriteria = (m_RenderQueueType == RenderQueueType.Transparent)
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            RenderTextureDescriptor renderTextureDesc = renderingData.cameraData.cameraTargetDescriptor;
            renderTextureDesc.depthBufferBits = 24;
            cmd.GetTemporaryRT(m_TemporaryColorTexture.id, renderTextureDesc, filterMode);
            cmd.SetRenderTarget(m_TemporaryColorTexture.id);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
            /*
            cmd.Clear();

            int tw = renderTextureDesc.width >> 1;
            int th = renderTextureDesc.height >> 1;
            var logh = Mathf.Log(Mathf.Min(tw, th), 2) + settings.bloomSettings.Radius - 8;
            var logh_i = (int)logh;
            var iterations = Mathf.Clamp(logh_i, 1, m_MaxIterations);
            float threshold = Mathf.GammaToLinearSpace(settings.bloomSettings.Threshold);
            blitMaterial.SetFloat("_Threshold", threshold);
            var knee = threshold * settings.bloomSettings.SoftKnee + 1e-5f;
            var curve = new Vector3(threshold - knee, knee * 2, 0.25f / knee);
            blitMaterial.SetVector("_Curve", curve);
            blitMaterial.SetFloat("_PrefilterOffs", !settings.bloomSettings.HighQuality ? -0.5f : 0.0f);
            blitMaterial.SetFloat("_SampleScale", 0.5f + logh - logh_i);
            blitMaterial.SetFloat("_Intensity", Mathf.Max(0, settings.bloomSettings.Intensity));

            CoreUtils.SetKeyword(blitMaterial, "_BLOOM_HQ", settings.bloomSettings.HighQuality);
            CoreUtils.SetKeyword(blitMaterial, "_USE_RGBM", m_UseRGBM);

            blitMaterial.EnableKeyword("_BLOOM_HQ");

            var materialPass = 0;
            renderTextureDesc.width = tw;
            renderTextureDesc.height = th;
            renderTextureDesc.graphicsFormat = m_DefaultHDRFormat;
            renderTextureDesc.depthBufferBits = 0;
            cmd.GetTemporaryRT(m_PrefilteredColorTexture.id, renderTextureDesc, filterMode);
            Blit(cmd, m_TemporaryColorTexture.Identifier(), m_PrefilteredColorTexture.id, blitMaterial, materialPass);

            var lastColorTexture = m_PrefilteredColorTexture.id;
            for (int i = 0; i < iterations; i++)
            {
                tw = Mathf.Max(1, tw >> 1);
                th = Mathf.Max(1, th >> 1);
                renderTextureDesc.width = tw;
                renderTextureDesc.height = th;
                m_BloomTextureSize[i] = new Vector2(tw, th);
                int mipDown = ShaderConstants._BloomMipDown[i];
                cmd.GetTemporaryRT(mipDown, renderTextureDesc, FilterMode.Bilinear);
                materialPass = (i == 0) ? 1 : 2;
                Blit(cmd, lastColorTexture, mipDown, blitMaterial, materialPass);
                lastColorTexture = mipDown;
            }

            for (int j = iterations - 2; j >= 0; j--)
            {
                var basetex = ShaderConstants._BloomMipDown[j];
                int mipUp = ShaderConstants._BloomMipUp[j];
                cmd.SetGlobalTexture(ShaderConstants._BloomBaseTex, basetex);
                renderTextureDesc.width = (int)m_BloomTextureSize[j].x;
                renderTextureDesc.height = (int)m_BloomTextureSize[j].y;
                cmd.GetTemporaryRT(mipUp, renderTextureDesc, FilterMode.Bilinear);
                materialPass = 3;
                Blit(cmd, lastColorTexture, mipUp, blitMaterial, materialPass);
                lastColorTexture = mipUp;
            }

            materialPass = 4;
            Blit(cmd, lastColorTexture, destination, blitMaterial, materialPass);

            cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
            cmd.ReleaseTemporaryRT(m_PrefilteredColorTexture.id);
            for (int k = 0; k < iterations; k++)
            {
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[k]);
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[k]);
            }
            */
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public override void FrameCleanup(CommandBuffer cmd) 
        {
            cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
            cmd.ReleaseTemporaryRT(m_PrefilteredColorTexture.id);
            for (int k = 0; k < m_MaxIterations; k++)
            {
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[k]);
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[k]);
            }

            if (settings.dstType == Target.TextureID) {
                cmd.ReleaseTemporaryRT(m_DestinationTexture.id);
            }
            if (source == destination || (settings.srcType == settings.dstType && settings.srcType == Target.CameraColor)) {
                cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
            }
        }
    }

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

    [System.Serializable]
    public class BloomSettings
    {
        public Material blitMaterial = null;
        public float Threshold = 0.5f;
        public float Intensity = 0.8f;
        public float SoftKnee = 0.5f;
        public float Radius = 2.5f;
        public bool HighQuality = false;
    }

    [System.Serializable]
    public class BlitSettings
    {
        public string PassTag = "Blit Feature";
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        public FilterSettings filterSettings = new FilterSettings();
        public BloomSettings bloomSettings = new BloomSettings();
        public Target srcType = Target.CameraColor;
        public string srcTextureId = "_CameraColorTexture";
        public RenderTexture srcTextureObject;
        public Target dstType = Target.CameraColor;
        public string dstTextureId = "_BlitPassTexture";
        public RenderTexture dstTextureObject;
    }

    public enum Target {
        CameraColor,
        TextureID,
        RenderTextureObject
    }

    public BlitSettings settings = new BlitSettings();
    
    BlitPass blitPass;

    private RenderTargetIdentifier srcIdentifier, dstIdentifier;

    public override void Create() {
        FilterSettings filter = settings.filterSettings;
        blitPass = new BlitPass(settings.Event, filter.PassNames, settings, settings.PassTag, filter.RenderQueue, filter.LayerMask);

        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing) {
            Debug.LogWarning("Note that the \"After Rendering Post Processing\"'s Color target doesn't seem to work? (or might work, but doesn't contain the post processing) :( -- Use \"After Rendering\" instead!");
        }

        UpdateSrcIdentifier();
        UpdateDstIdentifier();
    }

    private void UpdateSrcIdentifier() {
        srcIdentifier = UpdateIdentifier(settings.srcType, settings.srcTextureId, settings.srcTextureObject);
    }

    private void UpdateDstIdentifier() {
        dstIdentifier = UpdateIdentifier(settings.dstType, settings.dstTextureId, settings.dstTextureObject);
    }

    private RenderTargetIdentifier UpdateIdentifier(Target type, string s, RenderTexture obj) {
        if (type == Target.RenderTextureObject) {
            return obj;
        } else if (type == Target.TextureID) {
            //RenderTargetHandle m_RTHandle = new RenderTargetHandle();
            //m_RTHandle.Init(s);
            //return m_RTHandle.Identifier();
            return s;
        }
        return new RenderTargetIdentifier();
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {

        if (settings.bloomSettings.blitMaterial == null) {
            Debug.LogWarningFormat("Missing Blit Material. {0} blit pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
            return;
        }

        if (settings.Event == RenderPassEvent.AfterRenderingPostProcessing) {
        } else if (settings.Event == RenderPassEvent.AfterRendering && renderingData.postProcessingEnabled) {
            // If event is AfterRendering, and src/dst is using CameraColor, switch to _AfterPostProcessTexture instead.
            if (settings.srcType == Target.CameraColor) {
                settings.srcType = Target.TextureID;
                settings.srcTextureId = "_AfterPostProcessTexture";
                UpdateSrcIdentifier();
            }
            if (settings.dstType == Target.CameraColor) {
                settings.dstType = Target.TextureID;
                settings.dstTextureId = "_AfterPostProcessTexture";
                UpdateDstIdentifier();
            }
        } else {
            // If src/dst is using _AfterPostProcessTexture, switch back to CameraColor
            if (settings.srcType == Target.TextureID && settings.srcTextureId == "_AfterPostProcessTexture") {
                settings.srcType = Target.CameraColor;
                settings.srcTextureId = "";
                UpdateSrcIdentifier();
            }
            if (settings.dstType == Target.TextureID && settings.dstTextureId == "_AfterPostProcessTexture") {
                settings.dstType = Target.CameraColor;
                settings.dstTextureId = "";
                UpdateDstIdentifier();
            }
        }
        
        var src = (settings.srcType == Target.CameraColor) ? renderer.cameraColorTarget : srcIdentifier;
        var dest = (settings.dstType == Target.CameraColor) ? renderer.cameraColorTarget : dstIdentifier;
        
        blitPass.Setup(src, dest);
        renderer.EnqueuePass(blitPass);
    }
}