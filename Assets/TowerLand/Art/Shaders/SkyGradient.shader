// Stylised three-colour vertical gradient sky with a soft sun glow. Cheap enough for low-end mobile.
Shader "TowerLand/Sky Gradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.16, 0.48, 0.93, 1)
        _MidColor ("Middle", Color) = (0.42, 0.72, 0.98, 1)
        _HorizonColor ("Horizon", Color) = (0.80, 0.92, 0.99, 1)
        _BottomColor ("Below Horizon", Color) = (0.72, 0.86, 0.95, 1)
        _MidHeight ("Middle Height", Range(0.01, 0.99)) = 0.25
        _HorizonSharpness ("Horizon Sharpness", Range(1, 30)) = 8
        _SunColor ("Sun Glow", Color) = (1, 0.97, 0.88, 1)
        _SunDirection ("Sun Direction (world)", Vector) = (0.35, 0.55, -0.75, 0)
        _SunGlowSize ("Sun Glow Size", Range(1, 256)) = 24
        _SunGlowIntensity ("Sun Glow Intensity", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _MidColor, _HorizonColor, _BottomColor, _SunColor;
                float4 _SunDirection;
                half _MidHeight, _HorizonSharpness, _SunGlowSize, _SunGlowIntensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y;

                half3 sky = lerp(_MidColor.rgb, _TopColor.rgb, smoothstep(_MidHeight, 1.0, y));
                sky = lerp(_HorizonColor.rgb, sky, saturate(pow(saturate(y / _MidHeight), 1.0 / max(_HorizonSharpness * 0.25, 0.01))));
                half3 color = y >= 0 ? sky : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-y * 6));

                float sun = pow(saturate(dot(dir, normalize(_SunDirection.xyz))), _SunGlowSize);
                color += _SunColor.rgb * sun * _SunGlowIntensity;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
