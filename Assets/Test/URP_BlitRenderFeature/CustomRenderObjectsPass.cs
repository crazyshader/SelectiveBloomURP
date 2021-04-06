using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

//namespace UnityEngine.Experimental.Rendering.Universal
//{
public class CustomRenderObjectsPass : ScriptableRenderPass
    {

    static class ShaderConstants
    {
        public static int _BloomBaseTex;
        public static int[] _BloomMipUp;
        public static int[] _BloomMipDown;
    }


    RenderQueueType renderQueueType;
        FilteringSettings m_FilteringSettings;
    //CustomRenderObjects.CustomCameraSettings m_CameraSettings;

    public CustomRenderObjects.BloomSettings BloomSettings { get; set;  }
    private const int m_MaxIterations = 16;
    private readonly GraphicsFormat m_DefaultHDRFormat;
    private bool m_UseRGBM;

    private Vector2[] m_BloomTextureSize;
    private RenderTargetIdentifier source { get; set; }
    private RenderTargetIdentifier destination { get; set; }
    RenderTargetHandle m_PrefilteredColorTexture;


    string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;

        public Material overrideMaterial { get; set; }
        public int overrideMaterialPassIndex { get; set; }

        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

        //public void SetDetphState(bool writeEnabled, CompareFunction function = CompareFunction.Less)
        //{
        //    m_RenderStateBlock.mask |= RenderStateMask.Depth;
        //    m_RenderStateBlock.depthState = new DepthState(writeEnabled, function);
        //}

        //public void SetStencilState(int reference, CompareFunction compareFunction, StencilOp passOp, StencilOp failOp, StencilOp zFailOp)
        //{
        //    StencilState stencilState = StencilState.defaultValue;
        //    stencilState.enabled = true;
        //    stencilState.SetCompareFunction(compareFunction);
        //    stencilState.SetPassOperation(passOp);
        //    stencilState.SetFailOperation(failOp);
        //    stencilState.SetZFailOperation(zFailOp);

        //    //m_RenderStateBlock.mask |= RenderStateMask.Stencil;
        //    //m_RenderStateBlock.stencilReference = reference;
        //    //m_RenderStateBlock.stencilState = stencilState;
        //}

        //RenderStateBlock m_RenderStateBlock;

        public CustomRenderObjectsPass(string profilerTag, RenderPassEvent renderPassEvent, string[] shaderTags, RenderQueueType renderQueueType, int layerMask/*, CustomRenderObjects.CustomCameraSettings cameraSettings*/)
        {
            m_ProfilerTag = profilerTag;
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            this.renderPassEvent = renderPassEvent;
            this.renderQueueType = renderQueueType;
            this.overrideMaterial = null;
            this.overrideMaterialPassIndex = 0;
            RenderQueueRange renderQueueRange = (renderQueueType == RenderQueueType.Transparent)
                ? RenderQueueRange.transparent
                : RenderQueueRange.opaque;
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

        //m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        //m_CameraSettings = cameraSettings;

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

        m_PrefilteredColorTexture.Init("_PrefilteredColorTexture");

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

    public void Setup(RenderTargetIdentifier source, RenderTargetIdentifier destination)
    {
        this.source = source;
        this.destination = destination;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = (renderQueueType == RenderQueueType.Transparent)
                ? SortingCriteria.CommonTransparent
                : renderingData.cameraData.defaultOpaqueSortFlags;

            DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
            //drawingSettings.overrideMaterial = overrideMaterial;
            //drawingSettings.overrideMaterialPassIndex = overrideMaterialPassIndex;

            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            // In case of camera stacking we need to take the viewport rect from base camera
            Rect pixelRect = renderingData.cameraData.camera.pixelRect;
            float cameraAspect = (float) pixelRect.width / (float) pixelRect.height;
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.ClearRenderTarget(false, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings/*,
                    ref m_RenderStateBlock*/);

            RenderTextureDescriptor renderTextureDesc = renderingData.cameraData.cameraTargetDescriptor;
            int tw = renderTextureDesc.width >> 1;
            int th = renderTextureDesc.height >> 1;
            var logh = Mathf.Log(Mathf.Min(tw, th), 2) + BloomSettings.Radius - 8;
            var logh_i = (int)logh;
            var iterations = Mathf.Clamp(logh_i, 1, m_MaxIterations);
            float threshold = Mathf.GammaToLinearSpace(BloomSettings.Threshold);
            overrideMaterial.SetFloat("_Threshold", threshold);
            var knee = threshold * BloomSettings.SoftKnee + 1e-5f;
            var curve = new Vector3(threshold - knee, knee * 2, 0.25f / knee);
            overrideMaterial.SetVector("_Curve", curve);
            overrideMaterial.SetFloat("_PrefilterOffs", !BloomSettings.HighQuality ? -0.5f : 0.0f);
            overrideMaterial.SetFloat("_SampleScale", 0.5f + logh - logh_i);
            overrideMaterial.SetFloat("_Intensity", Mathf.Max(0, BloomSettings.Intensity));

            CoreUtils.SetKeyword(overrideMaterial, "_BLOOM_HQ", BloomSettings.HighQuality);
            CoreUtils.SetKeyword(overrideMaterial, "_USE_RGBM", m_UseRGBM);

            overrideMaterial.EnableKeyword("_BLOOM_HQ");

            var materialPass = 0;
            renderTextureDesc.graphicsFormat = m_DefaultHDRFormat;
            renderTextureDesc.depthBufferBits = 0;
            cmd.GetTemporaryRT(m_PrefilteredColorTexture.id, renderTextureDesc, FilterMode.Bilinear);
            Blit(cmd, source, m_PrefilteredColorTexture.id, overrideMaterial, materialPass);

            renderTextureDesc.width = tw;
            renderTextureDesc.height = th;

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
                Blit(cmd, lastColorTexture, mipDown, overrideMaterial, materialPass);
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
                Blit(cmd, lastColorTexture, mipUp, overrideMaterial, materialPass);
                lastColorTexture = mipUp;
            }

            materialPass = 4;
            Blit(cmd, lastColorTexture, destination, overrideMaterial, materialPass);

            cmd.ReleaseTemporaryRT(m_PrefilteredColorTexture.id);
            for (int k = 0; k < iterations; k++)
            {
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[k]);
                cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[k]);
            }

            //if (m_CameraSettings.overrideCamera)
            //{
            //    Matrix4x4 projectionMatrix = Matrix4x4.Perspective(m_CameraSettings.cameraFieldOfView, cameraAspect,
            //        camera.nearClipPlane, camera.farClipPlane);

            //    Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            //    Vector4 cameraTranslation = viewMatrix.GetColumn(3);
            //    viewMatrix.SetColumn(3, cameraTranslation + m_CameraSettings.offset);

            //    cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            //    context.ExecuteCommandBuffer(cmd);
            //}


                //if (m_CameraSettings.overrideCamera && m_CameraSettings.restoreCamera)
                //{
                //    cmd.Clear();
                //    cmd.SetViewProjectionMatrices(cameraData.camera.worldToCameraMatrix, cameraData.camera.projectionMatrix);
                //}
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        //cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
        cmd.ReleaseTemporaryRT(m_PrefilteredColorTexture.id);
        for (int k = 0; k < m_MaxIterations; k++)
        {
            cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[k]);
            cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[k]);
        }

        //if (settings.dstType == Target.TextureID)
        //{
        //    cmd.ReleaseTemporaryRT(m_DestinationTexture.id);
        //}
        //if (source == destination || (settings.srcType == settings.dstType && settings.srcType == Target.CameraColor))
        //{
        //    cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
        //}
    }
}
//}
