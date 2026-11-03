Shader "VR/CubeDirectionIndicator"
{
    Properties
    {
        _BaseColor       ("Base Color",          Color)        = (0.5, 0.5, 0.5, 1)
        _EmissionColor   ("Emission Color",       Color)        = (1, 1, 1, 1)
        _GlowIntensity   ("Top Glow Intensity",  Range(0, 8))  = 3.0
        _GlowFalloff     ("Glow Falloff",         Range(1, 20)) = 5.0
        _GlowBias        ("Glow Bias",            Range(-1, 1)) = 0.1
        _Smoothness      ("Smoothness",           Range(0, 1))  = 0.5
        _Metallic        ("Metallic",             Range(0, 1))  = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Forward Lit pass ──────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _GlowIntensity;
                float  _GlowFalloff;
                float  _GlowBias;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;   // local-space position for gradient
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ── Direction gradient ────────────────────────────────────
                // Cube mesh vertex Y ≈ -0.09 … +0.09  (0.18 size before 2x scale)
                // Normalize to 0..1 from bottom to top in LOCAL space
                float localY   = IN.positionOS.y / 0.09;           // -1 … +1
                float t        = saturate(localY * 0.5 + 0.5 + _GlowBias);
                float topGlow  = pow(t, _GlowFalloff) * _GlowIntensity;

                // ── Emission with directional glow ───────────────────────
                float3 emission = _EmissionColor.rgb * (1.0 + topGlow);

                // ── PBR lighting (simplified single directional) ─────────
                float3 normalWS  = normalize(IN.normalWS);
                Light  mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));

                float  NdotL     = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse   = _BaseColor.rgb * mainLight.color.rgb * NdotL;
                float3 ambient   = _BaseColor.rgb * SampleSH(normalWS);

                float3 finalColor = diffuse + ambient + emission;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _GlowIntensity;
                float  _GlowFalloff;
                float  _GlowBias;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END
            ENDHLSL
        }

        // ── Depth Only ────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _GlowIntensity;
                float  _GlowFalloff;
                float  _GlowBias;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
