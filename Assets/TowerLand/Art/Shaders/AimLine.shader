// Soft glowing ribbon for the aim trajectory (LineRenderer, view-aligned, UV x along the line, y across it):
// bright core, soft halo, drawn on top of the scene. Vertex colour = tint/alpha set by the Shooter.
// With _Dot = 1 the same look is drawn as a round dot on a camera-facing quad (the cap where the throw will hit).
Shader "TowerLand/Aim Line"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Core Intensity (HDR, feeds bloom)", Range(1, 4)) = 1.8
        _Core ("Core Half-Width (of the ribbon)", Range(0.05, 0.5)) = 0.24
        _Halo ("Halo Strength", Range(0, 1)) = 0.35
        _FadeIn ("Fade In (along the line)", Range(0, 0.5)) = 0.08
        _FadeOut ("Fade Out (along the line)", Range(0, 0.5)) = 0.06
        [Toggle] _Dot ("Round Dot (cap quad)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _Core;
                half _Halo;
                half _FadeIn;
                half _FadeOut;
                half _Dot;
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
                bool isDot = _Dot > 0.5h;
                half r = isDot ? length(input.uv - 0.5h) : abs(input.uv.y - 0.5h);
                half core = 1.0h - smoothstep(_Core - 0.04h, _Core, r);
                half halo = (1.0h - smoothstep(_Core, 0.5h, r)) * _Halo;
                half ends = isDot ? 1.0h : smoothstep(0.0h, max(_FadeIn, 1e-3h), input.uv.x) * smoothstep(1.0h, 1.0h - max(_FadeOut, 1e-3h), input.uv.x);
                half4 tint = _BaseColor * input.color;
                half alpha = saturate(core + halo) * ends * tint.a;
                half3 rgb = tint.rgb;
                // the cap gets a thin white rim so it stays readable on a patch of its own colour
                if (isDot) rgb = lerp(rgb, half3(1.0h, 1.0h, 1.0h), smoothstep(_Core - 0.1h, _Core - 0.07h, r));
                return half4(rgb * lerp(1.0h, _Intensity, core), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
