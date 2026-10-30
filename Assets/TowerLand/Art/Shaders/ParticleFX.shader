// Procedural additive particle sprite (no texture): soft glow, 4-point sparkle star, or expanding ring.
// Colour/alpha come from the particle vertex colour; HDR intensity feeds the bloom.
Shader "TowerLand/Particle FX"
{
    Properties
    {
        [KeywordEnum(Glow, Star, Ring)] _Shape ("Shape", Float) = 0
        _Intensity ("Intensity", Range(0.5, 6)) = 2
        _RingWidth ("Ring Width", Range(0.02, 0.4)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SHAPE_GLOW _SHAPE_STAR _SHAPE_RING
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _RingWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                half a;
            #if defined(_SHAPE_STAR)
                // thin cross rays + bright core
                float rays = max(saturate(1.0 - abs(p.x) * 9.0) * saturate(1.0 - abs(p.y)),
                                 saturate(1.0 - abs(p.y) * 9.0) * saturate(1.0 - abs(p.x)));
                a = saturate(rays * rays + pow(saturate(1.0 - r), 6.0) * 2.0);
            #elif defined(_SHAPE_RING)
                a = saturate(1.0 - abs(r - (1.0 - _RingWidth)) / _RingWidth);
                a *= a;
            #else
                a = pow(saturate(1.0 - r), 2.2);
            #endif
                a *= input.color.a;
                return half4(input.color.rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
