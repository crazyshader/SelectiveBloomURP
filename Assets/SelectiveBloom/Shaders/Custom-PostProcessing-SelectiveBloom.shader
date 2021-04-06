Shader "Hidden/Custom/PostProcessing/SelectiveBloom"
{
    Properties
    {
        [HideInInspector]_MainTex("Source", 2D) = "white" {}
    }

    HLSLINCLUDE

        #pragma multi_compile_local _ _USE_RGBM

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

        TEXTURE2D_X(_MainTex);
        TEXTURE2D_X(_BloomBaseTex);
        TEXTURE2D_X_FLOAT(_SceneColorTex);

        float4 _MainTex_TexelSize;
        float4 _BloomBaseTex_TexelSize;

        float _PrefilterOffs;
        half _Threshold;
        half3 _Curve;
        float _SampleScale;
        half _Intensity;
    
        // Brightness function
        half Brightness(half3 c)
        {
            return max(max(c.r, c.g), c.b);
        }
    
        // 3-tap median filter
        half3 Median(half3 a, half3 b, half3 c)
        {
            return a + b + c - min(min(a, b), c) - max(max(a, b), c);
        }
    
        // Clamp HDR value within a safe range
        half3 SafeHDR(half3 c) { return min(c, 65000); }
        half4 SafeHDR(half4 c) { return min(c, 65000); }
    
        // RGBM encoding/decoding
        half4 EncodeHDR(half3 color)
        {
        #if _USE_RGBM
            half4 outColor = EncodeRGBM(color);
        #else
            half4 outColor = half4(color, 1.0);
        #endif

        #if UNITY_COLORSPACE_GAMMA
            return half4(sqrt(outColor.xyz), outColor.w); // linear to γ
        #else
            return outColor;
        #endif
        }

        half3 DecodeHDR(half4 color)
        {
        #if UNITY_COLORSPACE_GAMMA
            color.xyz *= color.xyz; // γ to linear
        #endif

        #if _USE_RGBM
            return DecodeRGBM(color);
        #else
            return color.xyz;
        #endif
        }

        // Downsample with a 4x4 box filter
        half3 DownsampleFilter(float2 uv)
        {
            float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, +1, +1);

            half3 s;
            s  = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xw));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zw));

            return s * (1.0 / 4);
        }

        // Downsample with a 4x4 box filter + anti-flicker filter
        half3 DownsampleAntiFlickerFilter(float2 uv)
        {
            float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, +1, +1);

            half3 s1 = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xy));
            half3 s2 = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zy));
            half3 s3 = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xw));
            half3 s4 = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zw));

            // Karis's luma weighted average (using brightness instead of luma)
            half s1w = 1 / (Brightness(s1) + 1);
            half s2w = 1 / (Brightness(s2) + 1);
            half s3w = 1 / (Brightness(s3) + 1);
            half s4w = 1 / (Brightness(s4) + 1);
            half one_div_wsum = 1 / (s1w + s2w + s3w + s4w);

            return (s1 * s1w + s2 * s2w + s3 * s3w + s4 * s4w) * one_div_wsum;
        }
    
        half3 UpsampleFilter(float2 uv)
        {
        #if _BLOOM_HQ
            // 9-tap bilinear upsampler (tent filter)
            float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0) * _SampleScale;

            half3 s;
            s  = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv - d.xy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv - d.wy)) * 2;
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv - d.zy));

            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zw)) * 2;
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv       )) * 4;
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xw)) * 2;

            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.wy)) * 2;
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xy));

            return s * (1.0 / 16);
        #else
            // 4-tap bilinear upsampler
            float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, +1, +1) * (_SampleScale * 0.5);

            half3 s;
            s  = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zy));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xw));
            s += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zw));

            return s * (1.0 / 4);
        #endif
        }
    
        // Vertex shader
        
        struct Varyings_MultiTex
        {
            float4 positionCS    : SV_POSITION;
            float2 uvMain        : TEXCOORD0;
            float2 uvBase        : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        
        Varyings_MultiTex Vert_MultiTex(Attributes input)
        {
            Varyings_MultiTex output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uvMain = input.uv;
            output.uvBase = input.uv;
            #if UNITY_UV_STARTS_AT_TOP
                if (_BloomBaseTex_TexelSize.y < 0.0)
                    output.uvBase.y = 1.0 - input.uv.y;
            #endif
            
            return output;
        }
        
        // fragment shader
        
        half4 frag_prefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 d = _MainTex_TexelSize.xyx * float3(1, 1, 0);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
            uv += _MainTex_TexelSize.xy * _PrefilterOffs;
            half3 color0 = SafeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).xyz);
            half3 color1 = SafeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv - d.xz).xyz);
            half3 color2 = SafeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.xz).xyz);
            half3 color3 = SafeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv - d.zy).xyz);
            half3 color4 = SafeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv + d.zy).xyz);
            half3 color = Median(Median(color0.rgb, color1, color2), color3, color4);
            
        #if UNITY_COLORSPACE_GAMMA
            color = SRGBToLinear(color);
        #endif

            // Pixel brightness
            half br = Brightness(color);

            // Under-threshold part: quadratic curve
            half rq = clamp(br - _Curve.x, 0, _Curve.y);
            rq = _Curve.z * rq * rq;

            // Combine and apply the brightness response curve.
            color *= max(rq, br - _Threshold) / max(br, 1e-5);

            return EncodeHDR(color);
        }

        half4 frag_downsample1(Varyings input) : SV_Target
        {
            return EncodeHDR(DownsampleAntiFlickerFilter(input.uv));
        }
        
        half4 frag_downsample2(Varyings input) : SV_Target
        {
            return EncodeHDR(DownsampleFilter(input.uv));
        }
        
        half4 frag_upsample(Varyings_MultiTex input) : SV_Target
        {
            half3 base = DecodeHDR(SAMPLE_TEXTURE2D_X(_BloomBaseTex, sampler_LinearClamp, input.uvBase));
            half3 blur = UpsampleFilter(input.uvMain);
            return EncodeHDR(base + blur);
        }
        
        half4 frag_upsample_final(Varyings_MultiTex input) : SV_Target
        {
            half4 base = SAMPLE_TEXTURE2D_X(_SceneColorTex, sampler_LinearClamp, input.uvBase);
            half3 blur = UpsampleFilter(input.uvMain);
        #if UNITY_COLORSPACE_GAMMA
            base.rgb = SRGBToLinear(base.rgb);
        #endif
            half3 cout = base.rgb + blur * _Intensity;
        #if UNITY_COLORSPACE_GAMMA
            cout = LinearToSRGB(cout);
        #endif
            return half4(cout, base.a);
        }
        
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        // Pass 0
        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma vertex Vert
                #pragma fragment frag_prefilter
            ENDHLSL
        }

        // Pass 1
        Pass
        {
            Name "Bloom First level downsampler"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment frag_downsample1
            ENDHLSL
        }

        // Pass 2
        Pass
        {
            Name "Bloom Second level downsampler"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment frag_downsample2
            ENDHLSL
        }

        // Pass 3
        Pass
        {
            Name "Bloom Upsample"

            HLSLPROGRAM
                #pragma vertex Vert_MultiTex
                #pragma fragment frag_upsample
                #pragma multi_compile_local _ _BLOOM_HQ
            ENDHLSL
        }
        
        
        // Pass 4
        Pass
        {
            Name "Bloom Combiner"

            HLSLPROGRAM
                #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
                #pragma vertex Vert_MultiTex
                #pragma fragment frag_upsample_final
                #pragma multi_compile_local _ _BLOOM_HQ
            ENDHLSL
        }
    }
}
