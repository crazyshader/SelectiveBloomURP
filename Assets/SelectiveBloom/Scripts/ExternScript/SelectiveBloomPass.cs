using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Framework.Rendering
{
    public class SelectiveBloomPass : ScriptableRenderPass
    {
        private static class ShaderConstants
        {
            public static int BloomTempTex;
            public static int BloomBaseTex;
            public static int[] BloomDownTex;
            public static int[] BloomUpTex;
        }

        private readonly ProfilingSampler m_ProfilingSampler;
        private readonly RenderQueueType m_RenderQueueType;
        private FilteringSettings m_FilteringSettings;
        private readonly SelectiveBloom.SelectiveBloomSettings m_SelectiveBloomSettings;

        private readonly GraphicsFormat m_DefaultHDRFormat;
        private readonly bool m_UseRGBM;
        private readonly Material m_BloomMaterial;

        private const int m_MaxIterations = 16;
        private Vector2[] m_BloomTextureSize = new Vector2[m_MaxIterations];
        private RenderTargetIdentifier m_SourceTarget;
        private RenderTargetIdentifier m_DestinationTarget;
        private List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

        public SelectiveBloomPass(SelectiveBloom.SelectiveBloomSettings selectiveBloomSettings)
        {
            this.renderPassEvent = selectiveBloomSettings.Event;
            m_SelectiveBloomSettings = selectiveBloomSettings;
            m_RenderQueueType = selectiveBloomSettings.FilterSettings.RenderQueue;
            m_ProfilingSampler = new ProfilingSampler(selectiveBloomSettings.PassTag);
            m_BloomMaterial = selectiveBloomSettings.BloomSettings.BloomMaterial;

            RenderQueueRange renderQueueRange = RenderQueueRange.all;
            switch (m_RenderQueueType)
            {
                case RenderQueueType.Opaque:
                    renderQueueRange = RenderQueueRange.opaque;
                    break;
                case RenderQueueType.Transparent:
                    renderQueueRange = RenderQueueRange.transparent;
                    break;
                default:
                    break;
            }
            m_FilteringSettings = new FilteringSettings(renderQueueRange, selectiveBloomSettings.FilterSettings.LayerMask);

            var passNames = selectiveBloomSettings.FilterSettings.PassNames;
            if (passNames != null && passNames.Length > 0)
            {
                foreach (var passName in passNames)
                {
                    m_ShaderTagIdList.Add(new ShaderTagId(passName));
                }
            }
            else
            {
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }

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
            // 解决真机上显示异常，暂强制设置为该格式
            m_DefaultHDRFormat = GraphicsFormat.R8G8B8A8_SRGB;

            ShaderConstants.BloomBaseTex = Shader.PropertyToID("_BloomBaseTex");
            ShaderConstants.BloomTempTex = Shader.PropertyToID("_BloomTempTex");

            ShaderConstants.BloomUpTex = new int[m_MaxIterations];
            ShaderConstants.BloomDownTex = new int[m_MaxIterations];
            for (int i = 0; i < m_MaxIterations; i++)
            {
                ShaderConstants.BloomDownTex[i] = Shader.PropertyToID("_BloomDownTex" + i);
                ShaderConstants.BloomUpTex[i] = Shader.PropertyToID("_BloomUpTex" + i);
            }
        }

        public void Setup(RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            this.m_SourceTarget = source;
            this.m_DestinationTarget = destination;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_BloomMaterial == null)
            {
                return;
            }

            SortingCriteria sortingCriteria = (m_RenderQueueType == RenderQueueType.Transparent)
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(m_SelectiveBloomSettings.PassTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.ClearRenderTarget(false, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);

                var bloomSettings = m_SelectiveBloomSettings.BloomSettings;
                RenderTextureDescriptor renderTextureDesc = renderingData.cameraData.cameraTargetDescriptor;
                int tw = renderTextureDesc.width;
                int th = renderTextureDesc.height;
                var logh = Mathf.Log(Mathf.Min(tw >> 1, th >> 1), 2) + bloomSettings.Radius - 8;
                var logh_i = (int)logh;
                var iterations = Mathf.Clamp(logh_i, 1, m_MaxIterations);
                float threshold = Mathf.GammaToLinearSpace(bloomSettings.Threshold);
                m_BloomMaterial.SetFloat("_Threshold", threshold);
                var knee = threshold * bloomSettings.SoftKnee + 1e-5f;
                var curve = new Vector3(threshold - knee, knee * 2, 0.25f / knee);
                m_BloomMaterial.SetVector("_Curve", curve);
                m_BloomMaterial.SetFloat("_PrefilterOffs", !bloomSettings.HighQuality ? -0.5f : 0.0f);
                m_BloomMaterial.SetFloat("_SampleScale", 0.5f + logh - logh_i);
                m_BloomMaterial.SetFloat("_Intensity", Mathf.Max(0, bloomSettings.Intensity));
                CoreUtils.SetKeyword(m_BloomMaterial, "_BLOOM_HQ", bloomSettings.HighQuality);
                CoreUtils.SetKeyword(m_BloomMaterial, "_USE_RGBM", m_UseRGBM);

                renderTextureDesc.graphicsFormat = m_DefaultHDRFormat;
                renderTextureDesc.depthBufferBits = 0;
                var tempRT = ShaderConstants.BloomTempTex;
                cmd.GetTemporaryRT(tempRT, renderTextureDesc, FilterMode.Bilinear);
                Blit(cmd, m_SourceTarget, tempRT, m_BloomMaterial, 0);

                var lastColorTexture = tempRT;
                for (int i = 0; i < iterations; i++)
                {
                    tw = Mathf.Max(1, tw >> 1);
                    th = Mathf.Max(1, th >> 1);
                    renderTextureDesc.width = tw;
                    renderTextureDesc.height = th;
                    m_BloomTextureSize[i] = new Vector2(tw, th);
                    int mipDown = ShaderConstants.BloomDownTex[i];
                    cmd.GetTemporaryRT(mipDown, renderTextureDesc, FilterMode.Bilinear);
                    Blit(cmd, lastColorTexture, mipDown, m_BloomMaterial, (i == 0) ? 1 : 2);
                    lastColorTexture = mipDown;
                }

                for (int j = iterations - 2; j >= 0; j--)
                {
                    var basetex = ShaderConstants.BloomDownTex[j];
                    int mipUp = ShaderConstants.BloomUpTex[j];
                    cmd.SetGlobalTexture(ShaderConstants.BloomBaseTex, basetex);
                    renderTextureDesc.width = (int)m_BloomTextureSize[j].x;
                    renderTextureDesc.height = (int)m_BloomTextureSize[j].y;
                    cmd.GetTemporaryRT(mipUp, renderTextureDesc, FilterMode.Bilinear);
                    Blit(cmd, lastColorTexture, mipUp, m_BloomMaterial, 3);
                    lastColorTexture = mipUp;
                }

                Blit(cmd, lastColorTexture, m_DestinationTarget, m_BloomMaterial, 4);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(ShaderConstants.BloomTempTex);
            for (int k = 0; k < m_MaxIterations; k++)
            {
                cmd.ReleaseTemporaryRT(ShaderConstants.BloomDownTex[k]);
                cmd.ReleaseTemporaryRT(ShaderConstants.BloomUpTex[k]);
            }
        }
    }
}
