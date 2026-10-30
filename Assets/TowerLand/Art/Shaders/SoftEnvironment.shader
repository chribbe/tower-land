// Soft "fake GI" lighting for the World environment props (palette-textured low-poly meshes):
// - wrapped diffuse so light rolls softly around smooth-shaded shapes,
// - hemisphere bounce ambient: sky colour from above, green bounce light from the meadow below,
// - vertex colour: RGB = baked colour tint (sRGB, blends across bevels), A = baked ambient occlusion (Blender),
// - tinted, lifted shadows instead of hard grey ones, a little foliage translucency, and fog.
Shader "TowerLand/Soft Environment"
{
    Properties
    {
        _BaseMap ("Palette", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)

        [Header(Direct)]
        _Wrap ("Light Wrap", Range(0, 1)) = 0.55
        _DirectStrength ("Sun Strength", Range(0, 2)) = 0.85
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.72, 0.86, 1)
        _ShadowLift ("Shadow Lift", Range(0, 1)) = 0.35

        [Header(Fake GI)]
        _SkyAmbient ("Sky Ambient (from above)", Color) = (0.62, 0.76, 0.98, 1)
        _GroundBounce ("Ground Bounce (from below)", Color) = (0.50, 0.66, 0.28, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.85
        _AOStrength ("Baked AO Strength", Range(0, 1)) = 1
        _AOTint ("AO Tint", Color) = (0.55, 0.62, 0.78, 1)
        _Translucency ("Back-light Translucency", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Wrap;
            half _DirectStrength;
            half4 _ShadowColor;
            half _ShadowLift;
            half4 _SkyAmbient;
            half4 _GroundBounce;
            half _AmbientStrength;
            half _AOStrength;
            half4 _AOTint;
            half _Translucency;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 tint : TEXCOORD3;   // rgb colour tint (linear), a = AO
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.tint = half4(SRGBToLinear(input.color.rgb), input.color.a);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * input.tint.rgb;

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half shadow = lerp(_ShadowLift, 1.0h, light.shadowAttenuation);
                half3 direct = light.color * wrapped * shadow * _DirectStrength;
                direct *= lerp(_ShadowColor.rgb, 1.0h, saturate(wrapped * light.shadowAttenuation * 1.5h));
                // Thin foliage glows a little when lit from behind.
                direct += light.color * saturate(-ndl) * _Translucency * light.shadowAttenuation * base;

                // Hemisphere bounce: sky from above, sunlit meadow from below.
                half3 ambient = lerp(_GroundBounce.rgb, _SkyAmbient.rgb, n.y * 0.5h + 0.5h) * _AmbientStrength;

                half ao = lerp(1.0h, input.tint.a, _AOStrength);
                half3 aoTint = lerp(_AOTint.rgb, 1.0h, ao);   // occluded areas go cool and soft, not black
                half3 colour = base * (direct * lerp(0.6h, 1.0h, ao) + ambient * ao) * aoTint;

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 shadowVert (Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }
            half4 shadowFrag () : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 depthVert (Attributes input) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(input); return TransformObjectToHClip(input.positionOS.xyz); }
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings depthNormalsVert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
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
