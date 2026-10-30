// Cartoon frosted glass (the win-sequence vacuum hose): the scene behind is refracted and blurred (camera opaque texture,
// enabled on the camera only while glass is on screen), lightly tinted, with a bright fresnel rim, a hard highlight
// stripe down the tube and a sun glint. Writes depth so overlapping hose sections don't flicker.
Shader "TowerLand/Frosted Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.72, 0.93, 1, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.22
        _Blur ("Blur Radius (screen)", Range(0, 0.03)) = 0.008
        _Refraction ("Refraction", Range(0, 0.1)) = 0.035
        _RimColor ("Rim", Color) = (0.9, 0.98, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.9
        _StripeColor ("Highlight Stripe", Color) = (1, 1, 1, 1)
        _StripeCenter ("Stripe Position", Range(-1, 1)) = -0.45
        _StripeWidth ("Stripe Width", Range(0, 0.5)) = 0.12
        _GlintStrength ("Sun Glint", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back
            Blend Off   // the scene behind is composited in the shader (opaque texture)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _RimColor, _StripeColor;
                half _TintStrength, _Blur, _Refraction, _RimPower, _RimStrength, _StripeCenter, _StripeWidth, _GlintStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(n, v));
                half fresnel = pow(1.0h - facing, _RimPower);

                // Refract: shift the scene lookup along the view-space normal, more at grazing angles (thick-looking walls).
                float3 nVS = TransformWorldToViewDir(n);
                float2 uv = input.screenPos.xy / input.screenPos.w;
                uv += nVS.xy * _Refraction * (0.4 + fresnel);

                // Frost: 8 taps on a disc + centre.
                half3 scene = SampleSceneColor(uv);
                const float2 taps[8] = { float2(1, 0), float2(0.707, 0.707), float2(0, 1), float2(-0.707, 0.707),
                                         float2(-1, 0), float2(-0.707, -0.707), float2(0, -1), float2(0.707, -0.707) };
                float2 radius = float2(_Blur * _ScreenParams.y / _ScreenParams.x, _Blur);
                [unroll] for (int i = 0; i < 8; i++)
                    scene += SampleSceneColor(uv + taps[i] * radius * ((i & 1) ? 0.6 : 1.0));
                scene /= 9.0;

                half3 colour = lerp(scene, scene * _Tint.rgb * 1.15h + _Tint.rgb * 0.08h, _TintStrength);
                colour = lerp(colour, _RimColor.rgb, saturate(fresnel * _RimStrength));

                // Cartoon highlight stripe running along the tube (view-space normal x), plus a sun glint.
                half stripe = smoothstep(_StripeWidth, _StripeWidth * 0.6, abs(nVS.x - _StripeCenter)) * smoothstep(-0.2, 0.3, nVS.y + 0.4);
                colour = lerp(colour, _StripeColor.rgb, stripe * 0.75h);
                Light sun = GetMainLight();
                half glint = pow(saturate(dot(n, normalize(sun.direction + v))), 90.0h);
                colour += sun.color * step(0.5h, glint) * _GlintStrength;

                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
