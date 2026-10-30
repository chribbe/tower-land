// Stylised meadow ground: palette colour (from UVs, like the rest of the environment) modulated by world-space
// tiling detail (fine grass speckle + soft large patches), wrapped diffuse with real shadows, ambient and fog.
Shader "TowerLand/Meadow Ground"
{
    Properties
    {
        _BaseMap ("Palette", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 1.5)) = 1
        _DetailMap ("Detail (R fine, G patches, B medium)", 2D) = "grey" {}
        _FineScale ("Fine Tile Size (m)", Float) = 2.2
        _FineStrength ("Fine Strength", Range(0, 1)) = 0.55
        _MediumScale ("Medium Tile Size (m)", Float) = 9
        _MediumStrength ("Medium Strength", Range(0, 1)) = 0.35
        _PatchScale ("Patch Tile Size (m)", Float) = 38
        _PatchStrength ("Patch Strength", Range(0, 1)) = 0.6
        _PatchTint ("Patch Dark Tint", Color) = (0.78, 0.92, 0.70, 1)
        _FineFade ("Fine Detail Fade Distance (m)", Float) = 60
        _Wrap ("Light Wrap", Range(0, 1)) = 0.3
        _ShadowColor ("Shadow Colour", Color) = (0.55, 0.68, 0.62, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Saturation;
            float _FineScale;
            half _FineStrength;
            float _MediumScale;
            half _MediumStrength;
            float _PatchScale;
            half _PatchStrength;
            half4 _PatchTint;
            float _FineFade;
            half _Wrap;
            half4 _ShadowColor;
            half _AmbientStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                base = lerp(dot(base, half3(0.2126h, 0.7152h, 0.0722h)).xxx, base, _Saturation);

                float2 xz = input.positionWS.xz;
                half fine = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, xz / _FineScale).r;
                half medium = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, xz / _MediumScale + 0.37).b;
                half patch = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, xz / _PatchScale + 0.71).g;

                float distance = length(GetCameraPositionWS() - input.positionWS);
                half fineFade = saturate(1.0 - distance / _FineFade);   // avoids shimmering speckle far away
                base *= 1.0h + (fine - 0.5h) * 2.0h * _FineStrength * fineFade;
                base *= 1.0h + (medium - 0.5h) * 2.0h * _MediumStrength;
                base *= lerp(1.0h, _PatchTint.rgb, saturate((0.5h - patch) * 2.0h * _PatchStrength * 2.0h));
                base *= 1.0h + saturate((patch - 0.5h) * 2.0h) * _PatchStrength * 0.25h;   // sunny lighter patches

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half wrapped = saturate((dot(n, light.direction) + _Wrap) / (1 + _Wrap));
                half shadow = light.shadowAttenuation;
                half3 lit = light.color * wrapped * shadow;
                half3 ambient = SampleSH(n) * _AmbientStrength;
                half3 colour = base * (lit + ambient);
                colour *= lerp(_ShadowColor.rgb, 1.0h, shadow);         // cool, saturated shadows like the mockup

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            float4 depthVert (float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half depthFrag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex depthNormalsVert
            #pragma fragment depthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings depthNormalsVert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 depthNormalsFrag (Varyings input) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
