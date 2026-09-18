// レーンの床。高さで色を変え、その上に格子を掛ける。
// 高さはワールド座標から取るので、平らな床でも起伏のある床でも同じものが使える。
// 傾きの向きではなく高さそのもので塗るので、うねった床でも谷と山が読める。
Shader "CrazyBowling/LaneFloor"
{
    Properties
    {
        [MainTexture] _BaseMap("格子のテクスチャ", 2D) = "white" {}
        [MainColor]   _BaseColor("床の色", Color) = (0.62, 0.42, 0.22, 1)

        _Smoothness("なめらかさ", Range(0, 1)) = 0.25
        _Metallic("金属っぽさ", Range(0, 1)) = 0

        [Header(Height Tint)]
        _HeightStrength("色分けの濃さ", Range(0, 1)) = 0.15
        _HeightCenter("色分けの基準の高さ m", Float) = 0
        _HeightSpan("色分けの高さの幅 m", Float) = 0.05
        _LowColor("低い側の色", Color) = (0.35, 0.55, 1.0, 1)
        _HighColor("高い側の色", Color) = (1.0, 0.55, 0.30, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        // 共通の宣言。どのパスからも同じ並びで見えるようにしておく
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _LowColor;
            half4 _HighColor;
            half _Smoothness;
            half _Metallic;
            half _HeightStrength;
            float _HeightCenter;
            float _HeightSpan;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // ガンマ空間との行き来。URP の Color.hlsl はパスによっては届かないので、
        // 依存を作らずここで済ませる
        half3 ToGamma(half3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
        half3 ToLinear(half3 c) { return pow(max(c, 0.0001), 2.2); }

        // 高さから床の色を作る。格子はこのあとで掛ける。
        // 混ぜ算はガンマ空間で行う。リニア空間で混ぜると、茶色は青成分がほぼ0なので
        // 「濃さ15%」でも青が何倍にもなってしまい、見た目が一気に紫になる
        half3 CalculateFloorColor(float worldY, half4 grid)
        {
            float span = max(abs(_HeightSpan), 0.0001);
            float t = saturate((worldY - _HeightCenter) / span + 0.5);

            half3 tintGamma = lerp(ToGamma(_LowColor.rgb), ToGamma(_HighColor.rgb), t);
            half3 mixedGamma = lerp(ToGamma(_BaseColor.rgb), tintGamma, _HeightStrength);

            return ToLinear(mixedGamma) * grid.rgb;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 grid = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = CalculateFloorColor(input.positionWS.y, grid);
                surfaceData.smoothness = _Smoothness;
                surfaceData.metallic = _Metallic;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;

                return color;
            }
            ENDHLSL
        }

        // 床が他のものに影を落とせるようにする
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // 深度だけを書くパス。被写界深度やSSAOを使うときに要る
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.0

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
