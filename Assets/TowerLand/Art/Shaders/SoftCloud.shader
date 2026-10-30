// Soft cartoon clouds: unlit, bright white tops fading to a light sky-blue underside, with a gentle sun-side rim.
Shader "TowerLand/Soft Cloud"
{
    Properties
    {
        _TopColor ("Top", Color) = (1, 1, 1, 1)
        _BottomColor ("Underside", Color) = (0.74, 0.86, 0.98, 1)
        _Softness ("Gradient Softness", Range(0.1, 2)) = 0.9
        _RimColor ("Rim", Color) = (1, 1, 1, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _BottomColor, _RimColor;
                half _Softness, _RimStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half fogFactor : TEXCOORD2; };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);   // a sea of clouds far below melts into the haze
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half t = smoothstep(-_Softness * 0.5, _Softness * 0.6, n.y);
                half3 colour = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * saturate(n.y + 0.4h);
                colour = lerp(colour, _RimColor.rgb, rim * _RimStrength);
                return half4(MixFog(colour, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
