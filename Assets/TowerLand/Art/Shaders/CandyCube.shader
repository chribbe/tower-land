// Stylised "candy" shading for the tower cubes (mobile friendly, SRP Batcher compatible).
// - Saturated diffuse: the shade side darkens towards a richer version of the base colour (base^2), never grey.
// - Fake studio environment reflection (sky above / ground below, Fresnel weighted) + a rounded-rectangle
//   "softbox" reflection: gives the glossy, smooth look (bright top bevels, crisp highlight lines along edges).
// - A fixed view-space "studio" key highlight: tight, bright, and masked to the rounded edges/corners
//   (vertex colour R = edge, G = corner, written by BeveledCubeMesh) so it always lands on the upper corners.
// - Fake occlusion on bevels facing away from the key and on undersides, so seams read deeper.
Shader "TowerLand/Candy Cube"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)

        [Header(Diffuse)]
        _Wrap ("Light Wrap", Range(0, 1)) = 0.5
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.58, 0.80, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.75
        _FaceGradient ("Face Top-Bottom Gradient", Range(0, 0.5)) = 0.14
        _ShadeLevel ("Shade Side Brightness", Range(0, 1)) = 0.55
        _Saturation ("Shade Saturation (base^n)", Range(0, 1)) = 0.6
        _Warmth ("Warm Light Tint", Color) = (1, 0.97, 0.92, 1)
        _BevelLight ("Top Bevel Lightening", Range(0, 1)) = 0.5

        [Header(Reflection)]
        _EnvTop ("Env Sky", Color) = (0.86, 0.94, 1, 1)
        _EnvHorizon ("Env Horizon", Color) = (1, 0.98, 0.95, 1)
        _EnvBottom ("Env Ground", Color) = (0.25, 0.33, 0.2, 1)
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.55
        _F0 ("Base Reflectance (face-on)", Range(0, 0.5)) = 0.06
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
        _SoftboxDirection ("Softbox Direction (view space)", Vector) = (-0.35, 0.55, 0.75, 0)
        _SoftboxSize ("Softbox Size (x, y)", Vector) = (0.35, 0.16, 0, 0)
        _SoftboxSharpness ("Softbox Edge Sharpness", Range(0.01, 0.5)) = 0.08
        _SoftboxStrength ("Softbox Strength", Range(0, 2)) = 0.9

        [Header(Highlights)]
        _KeyDirection ("Key Highlight Direction (view space)", Vector) = (-0.5, 0.7, 0.55, 0)
        _HighlightColor ("Highlight Colour", Color) = (1, 1, 1, 1)
        _SpecPower ("Highlight Tightness", Range(4, 512)) = 70
        _SpecStrength ("Highlight Strength", Range(0, 4)) = 1.4
        _FaceSheen ("Flat Face Sheen", Range(0, 1)) = 0.08
        _CornerBoost ("Corner Boost", Range(0, 4)) = 1.6
        _BroadPower ("Gloss Spread", Range(1, 64)) = 10
        _BroadStrength ("Gloss Strength", Range(0, 2)) = 0.35
        _RimStrength ("Edge Rim", Range(0, 1)) = 0.18

        [Header(Crevices)]
        _CreviceColor ("Crevice Colour", Color) = (0.07, 0.06, 0.14, 1)
        _CreviceStart ("Crevice Start (depth from front)", Range(0, 1)) = 0.12
        _CreviceRange ("Crevice Range", Range(0.05, 1)) = 0.45
        _CreviceStrength ("Crevice Strength", Range(0, 1)) = 0.9
        _DeepValue ("Deep Shade Brightness (own hue)", Range(0, 1)) = 0.24
        _DeepChroma ("Deep Shade Extra Saturation", Range(0, 1)) = 0.35
        _DeepTint ("Deep Shade Cool Tint Amount", Range(0, 1)) = 0.3
        _Covered ("Covered By Outer Layer (0/1)", Range(0, 1)) = 0
        _CoveredDarkness ("Covered Darkness", Range(0, 1)) = 0.8
        _SideOutline ("Grazing Edge Outline", Range(0, 1)) = 0.65
        _OpenSides ("Exposed Sides Lose Crevice", Range(0, 1)) = 1

        [Header(Form Shadow)]
        _FormShadowTint ("Form Shadow Tint", Color) = (0.70, 0.71, 0.86, 1)
        _FormShadowStrength ("Form Shadow Strength", Range(0, 1)) = 1
        _FormShadowStart ("Start (facing light 1 .. -1 away)", Range(-1, 1)) = 0.15
        _FormShadowEnd ("Full Shadow At", Range(-1, 1)) = -0.85

        [Header(Occlusion)]
        _EdgeOcclusion ("Edge Occlusion", Range(0, 1)) = 0.4
        _BottomOcclusion ("Underside Occlusion", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Wrap;
            half4 _ShadowTint;
            half _AmbientStrength;
            half _FaceGradient;
            float4 _KeyDirection;
            half4 _HighlightColor;
            half _SpecPower;
            half _SpecStrength;
            half _FaceSheen;
            half _CornerBoost;
            half _BroadPower;
            half _BroadStrength;
            half _RimStrength;
            half _EdgeOcclusion;
            half _BottomOcclusion;
            half _ShadeLevel;
            half _Saturation;
            half4 _Warmth;
            half _BevelLight;
            half4 _CreviceColor;
            half _CreviceStart;
            half _CreviceRange;
            half _CreviceStrength;
            half _Covered;
            half _CoveredDarkness;
            half _SideOutline;
            half _OpenSides;
            half _DeepValue;
            half _DeepChroma;
            half _DeepTint;
            half4 _FormShadowTint;
            half _FormShadowStrength;
            half _FormShadowStart;
            half _FormShadowEnd;
            half4 _EnvTop;
            half4 _EnvHorizon;
            half4 _EnvBottom;
            half _Reflectivity;
            half _F0;
            half _FresnelPower;
            float4 _SoftboxDirection;
            float4 _SoftboxSize;
            half _SoftboxSharpness;
            half _SoftboxStrength;
        CBUFFER_END
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
                half4 color : COLOR;
                float2 uv1 : TEXCOORD1;          // x = sides with no neighbour (bits: 1 left -X, 2 right +X, 4 top, 8 bottom, 16 back -Z)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 mask : TEXCOORD2;          // r edge, g corner, b object-space height (-0.5..0.5), a depth from front face
                half fogFactor : TEXCOORD3;
                float2 outwardXZ : TEXCOORD4;    // world-space direction the cube faces around the tower (its +Z axis)
                half4 open : TEXCOORD5;          // exposed sides: left, right, top, bottom (1 = no neighbour there)
                float3 normalOS : TEXCOORD6;
                half openBack : TEXCOORD7;       // 1 = no inner-layer cube behind: the back face can be seen from inside the tower
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
                output.mask = half4(input.color.rg, input.positionOS.y, 0.5 - input.positionOS.z);   // cubes face +Z (outward)
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.outwardXZ = normalize(mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1)).xz + 1e-5);
                float bits = floor(input.uv1.x + 0.5);
                output.open = half4(fmod(bits, 2.0), fmod(floor(bits / 2.0), 2.0), fmod(floor(bits / 4.0), 2.0), fmod(floor(bits / 8.0), 2.0));
                output.openBack = floor(bits / 16.0);
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half edge = input.mask.r;
                half corner = input.mask.g;
                // Surfaces turned towards the tower axis with nothing behind them: seen from inside the hollow tower
                // through a hole, so the outside-only shading (seams, covered, form shadow) is lifted there.
                half inner = saturate(-normalize(input.normalOS).z * 1.5h) * input.openBack;

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half wrapped = saturate((dot(n, light.direction) + _Wrap) / (1 + _Wrap));
                half lit = wrapped * lerp(0.35h, 1.0h, light.shadowAttenuation);

                // Diffuse: lerp between a rich, saturated shade colour and the full base colour.
                half3 base = _BaseColor.rgb;
                // Hue-preserving shade: darken, then push saturation away from grey (multiplying the colour by itself
                // would drift yellow towards amber/mustard - the "tarnished" look).
                half3 shadeBase = base * _ShadeLevel;
                half shadeLuma = dot(shadeBase, half3(0.2126h, 0.7152h, 0.0722h));
                half3 shade = max(0.0h, lerp(shadeLuma.xxx, shadeBase, 1.0h + _Saturation * 0.5h));
                half3 colour = lerp(shade, base, lit) * _Warmth.rgb;
                colour *= 1.0h + _FaceGradient * (input.mask.b * 2.0h);         // lighter top, darker bottom of each cube

                // Camera-relative studio: key/softbox stay on the upper-left whatever the tower's rotation.
                float3 key = normalize(mul((float3x3)UNITY_MATRIX_I_V, _KeyDirection.xyz));
                half keyFacing = saturate(dot(n, key) * 0.5h + 0.5h);

                half occlusion = 1.0h - _EdgeOcclusion * edge * (1.0h - keyFacing);
                occlusion *= 1.0h - _BottomOcclusion * saturate(-n.y) * saturate(edge * 2.0h + 0.25h);
                colour *= occlusion;

                // Form shadow: the side of the tower turning away from the sun falls into a soft, cool shade across
                // whole cubes (per cube, from its facing around the cylinder), giving the tower its volume.
                float2 toSun = normalize(light.direction.xz + 1e-5);
                half facing = dot(input.outwardXZ, toSun) * (1.0h - 2.0h * inner);   // an inner face looks the other way
                half form = smoothstep(_FormShadowStart, _FormShadowEnd, facing) * _FormShadowStrength;
                colour *= lerp(1.0h, _FormShadowTint.rgb, form);

                // Upward-facing bevels (in screen terms) catch the light: a lighter, warmer tint of the base colour.
                float3 cameraUp = UNITY_MATRIX_I_V._m01_m11_m21;
                half upness = saturate(dot(n, cameraUp));
                colour = lerp(colour, saturate(base * 1.2h + 0.12h) * _Warmth.rgb, upness * saturate(edge * 1.6h) * _BevelLight);

                // Environment reflection: sky above, warm horizon, dark ground below; Fresnel weighted.
                float3 r = reflect(-v, n);
                half3 env = r.y > 0 ? lerp(_EnvHorizon.rgb, _EnvTop.rgb, saturate(r.y * 1.6h))
                                    : lerp(_EnvHorizon.rgb, _EnvBottom.rgb, saturate(-r.y * 4.0h));
                half fresnel = _F0 + (1.0h - _F0) * pow(1.0h - saturate(dot(n, v)), _FresnelPower);
                colour = lerp(colour, env, saturate(fresnel * _Reflectivity) * occlusion);

                // Softbox: a rounded-rectangle area light seen in reflection (view space) -> crisp edge highlights.
                float3 rv = mul((float3x3)UNITY_MATRIX_V, r);
                float3 box = normalize(_SoftboxDirection.xyz);
                float2 d = abs(rv.xy / max(rv.z, 0.05) - box.xy / box.z) - _SoftboxSize.xy;
                half boxDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
                half softbox = (1.0h - smoothstep(0.0h, _SoftboxSharpness, boxDist)) * step(0.0, rv.z);
                half shadowed = lerp(0.6h, 1.0h, light.shadowAttenuation);
                colour += _HighlightColor.rgb * softbox * _SoftboxStrength * (0.35h + fresnel) * shadowed;

                // Tight glint on the corners.
                float3 h = normalize(key + v);
                half spec = pow(saturate(dot(n, h)), _SpecPower);
                half specMask = lerp(_FaceSheen, 1.0h, saturate(edge * 1.5h)) + corner * _CornerBoost;
                colour += _HighlightColor.rgb * spec * specMask * _SpecStrength * shadowed;

                // Broad soft gloss over the lit corner.
                half gloss = pow(saturate(dot(n, h)), _BroadPower) * saturate(edge + corner + 0.35h);
                colour = lerp(colour, colour + (_HighlightColor.rgb - colour) * 0.6h, gloss * _BroadStrength * shadowed);

                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * edge * keyFacing;
                colour += base * rim * _RimStrength;

                // Crevices: the further a surface is from the cube's front face, the deeper it sits in the seam between
                // cubes -> fade to a dark, cool contact-shadow colour (replaces the old dark core cylinder).
                // Seams darken towards a deep version of the cube's own colour (plus a hint of cool tint) instead of a
                // fixed dark blue: mixing orange/yellow with blue reads brown/olive.
                half3 deep = base * lerp(1.0h, base, _DeepChroma) * _DeepValue + _CreviceColor.rgb * _DeepTint;
                half crevice = smoothstep(_CreviceStart, _CreviceStart + _CreviceRange, input.mask.a) * _CreviceStrength;
                // ...but only towards sides that still have a neighbour: a side facing a hole is exposed, not in a seam.
                float3 nOS = normalize(input.normalOS);
                half4 facingSide = half4(saturate(-nOS.x), saturate(nOS.x), saturate(nOS.y), saturate(-nOS.y));
                half towardOpen = dot(input.open, facingSide);
                half towardSeam = dot(1.0h - input.open, facingSide);
                half open = saturate((towardOpen - towardSeam) * 1.6h) * _OpenSides;
                crevice *= (1.0h - open) * (1.0h - inner);
                colour = lerp(colour, deep, crevice);
                // Vertical bevels seen at a grazing angle (cubes turning away around the cylinder) fade into the seam
                // colour, so neighbouring cubes stay visibly separated instead of merging into bands.
                float3 cameraRight = UNITY_MATRIX_I_V._m00_m10_m20;
                half grazing = 1.0h - saturate(dot(n, v));
                half outline = saturate(edge * 1.5h) * abs(dot(n, cameraRight)) * grazing * _SideOutline * (1.0h - open) * (1.0h - inner);
                colour = lerp(colour, deep, saturate(outline * 1.6h));

                // Cubes hidden behind an outer layer sit in shadow until exposed.
                // (Its inner face, open to the hollow core, is not hidden: seen through a hole from the other side.)
                colour = lerp(colour, deep, _Covered * _CoveredDarkness * (1.0h - inner));

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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

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

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 depthVert (Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half depthFrag () : SV_Target { return 0; }
            ENDHLSL
        }

        // Needed by renderers that run a depth-normals prepass (SSAO, Forward+ features).
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

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
                float2 octahedral = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0);
            #else
                return half4(n, 0);
            #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
