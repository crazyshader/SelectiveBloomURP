using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine;

//namespace UnityEngine.Experimental.Rendering.Universal
//{
public enum RenderQueueType
    {
        Opaque,
        Transparent,
    }

    public class CustomRenderObjects : ScriptableRendererFeature
    {
        [System.Serializable]
        public class RenderObjectsSettings
        {
            public string passTag = "RenderObjectsFeature";
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

            public FilterSettings filterSettings = new FilterSettings();

            public Material overrideMaterial = null;
            public int overrideMaterialPassIndex = 0;

            public bool overrideDepthState = false;
            public CompareFunction depthCompareFunction = CompareFunction.LessEqual;
            public bool enableWrite = true;

        //public StencilStateData stencilSettings = new StencilStateData();

        //public CustomCameraSettings cameraSettings = new CustomCameraSettings();

        public BloomSettings bloomSettings = new BloomSettings();
        }

        [System.Serializable]
        public class FilterSettings
        {
            // TODO: expose opaque, transparent, all ranges as drop down
            public RenderQueueType RenderQueueType;
            public LayerMask LayerMask;
            public string[] PassNames;

            public FilterSettings()
            {
                RenderQueueType = RenderQueueType.Opaque;
                LayerMask = 0;
            }
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

    //[System.Serializable]
    //public class CustomCameraSettings
    //{
    //    public bool overrideCamera = false;
    //    public bool restoreCamera = true;
    //    public Vector4 offset;
    //    public float cameraFieldOfView = 60.0f;
    //}

    public RenderObjectsSettings settings = new RenderObjectsSettings();

        CustomRenderObjectsPass renderObjectsPass;

        public override void Create()
        {
            FilterSettings filter = settings.filterSettings;
            renderObjectsPass = new CustomRenderObjectsPass(settings.passTag, settings.Event, filter.PassNames,
                filter.RenderQueueType, filter.LayerMask/*, settings.cameraSettings*/);

            renderObjectsPass.overrideMaterial = settings.overrideMaterial;
            renderObjectsPass.overrideMaterialPassIndex = settings.overrideMaterialPassIndex;

        renderObjectsPass.BloomSettings = settings.bloomSettings;

            //if (settings.overrideDepthState)
            //    renderObjectsPass.SetDetphState(settings.enableWrite, settings.depthCompareFunction);

            //if (settings.stencilSettings.overrideStencilState)
            //    renderObjectsPass.SetStencilState(settings.stencilSettings.stencilReference,
            //        settings.stencilSettings.stencilCompareFunction, settings.stencilSettings.passOperation,
            //        settings.stencilSettings.failOperation, settings.stencilSettings.zFailOperation);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
        renderObjectsPass.Setup(renderer.cameraColorTarget, renderer.cameraColorTarget);
            renderer.EnqueuePass(renderObjectsPass);
        }
    }
//}

