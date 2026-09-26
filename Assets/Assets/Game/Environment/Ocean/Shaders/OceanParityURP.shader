Shader "AgeOfSailRTS/Ocean/Ocean Parity Static"
{
    Properties
    {
        [Header(Blender Source Colors)]
        [MainColor] _BaseColor ("Base Color", Color) = (0, 0.296138316, 1, 1)
        _CrestColor ("Crest Color", Color) = (0.0630086586, 0.623961091, 0.806952596, 1)
        _FresnelColor ("Fresnel Highlight Color", Color) = (0.270494998, 0.973446071, 1, 1)
        _TransmissionColor ("Transmission Color", Color) = (0, 0.879623294, 1, 1)
        _FarOceanColor ("Far Ocean Color", Color) = (0.590620279, 0.830767214, 0.930094957, 1)

        [Header(Crest Height)]
        _CrestHeightScale ("Object Height Scale", Float) = 1
        _CrestHeightOffset ("Object Height Offset", Float) = 0
        _CrestStrength ("Crest Strength", Range(0, 1)) = 1

        [Header(Stylized Lighting)]
        _FresnelIOR ("Fresnel IOR", Range(1, 2.5)) = 1.5
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 1
        _ShadowStrength ("Broad Shadow Strength", Range(0, 1)) = 0.5
        _BroadHighlightPower ("Broad Highlight Signal Power", Range(0.1, 16)) = 4
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.800000012
        _HighlightSignalPower ("Sharp Highlight Signal Power", Range(0.1, 32)) = 12
        _TransmissionStrength ("Transmission Strength", Range(0, 1)) = 1
        _TransmissionGlossPower ("Transmission Gloss Signal Power", Range(0.1, 8)) = 1
        _TransmissionNormalFlattening ("Transmission Normal Smoothing Approximation", Range(0, 1)) = 0.5
        _MainLightShadowInfluence ("Main Light Shadow Influence", Range(0, 1)) = 0.15

        [Header(Wave Foam)]
        _WaveFoamBrightness ("Wave Foam Brightness", Range(0, 2)) = 1
        _FoamAttributeScale ("Foam Attribute Coverage Scale", Range(0, 2)) = 0.6
        _WaveFoamStrength ("Wave Foam Strength", Range(0, 1)) = 0.4
        _FoamNoiseScale ("Foam Noise Scale per Meter", Float) = 0.12
        _FoamNoiseStrength ("Foam Noise Strength", Range(0, 2)) = 1
        _FoamNoiseOffset ("Foam Noise Offset", Vector) = (0, 0, 0, 0)
        _FoamDistanceFadeStart ("Foam Fade Start", Float) = 35
        _FoamDistanceFadeEnd ("Foam Fade End", Float) = 100

        [Header(Distance Unification)]
        _DistanceFadeStart ("Distance Fade Start", Float) = 0
        _DistanceFadeEnd ("Distance Fade End", Float) = 500
        _DistanceFadeStrength ("Distance Fade Strength", Range(0, 1)) = 1
        _BlenderDistanceDepth ("Blender Distance Depth Reference", Float) = -0.799999237

        [Header(Blender Values Reserved for Later Phases)]
        _ContactFoamRange ("Contact Foam Range (Reserved)", Float) = 1
        _ContactFoamEvolutionSpeed ("Contact Foam Evolution Speed (Reserved)", Float) = 0.700000048
        _ContactFoamStrength ("Contact Foam Strength (Reserved)", Float) = 0.100000024
        _ContactFoamColor ("Contact Foam Color (Reserved)", Color) = (0.622619152, 0.622619152, 0.622619152, 1)
        _WaterTransparencyRange ("Water Transparency Range (Reserved)", Float) = 3.39999986
        _WaterReflectionFactor ("Water Reflection Factor (Reserved)", Range(0, 1)) = 0

        [Header(Captured Blender Ramp LUTs)]
        [NoScaleOffset] _Ramp01 ("Ramp 01 - Contact Foam Shape", 2D) = "white" {}
        [NoScaleOffset] _Ramp02 ("Ramp 02 - Crest Height", 2D) = "white" {}
        [NoScaleOffset] _Ramp03 ("Ramp 03 - Broad Highlight", 2D) = "white" {}
        [NoScaleOffset] _Ramp04 ("Ramp 04 - Shadow Tone", 2D) = "white" {}
        [NoScaleOffset] _Ramp05 ("Ramp 05 - Fresnel Shadow", 2D) = "white" {}
        [NoScaleOffset] _Ramp06 ("Ramp 06 - Fresnel Highlight", 2D) = "white" {}
        [NoScaleOffset] _Ramp07 ("Ramp 07 - Transmission", 2D) = "white" {}
        [NoScaleOffset] _Ramp08 ("Ramp 08 - Sharp Highlight", 2D) = "white" {}
        [NoScaleOffset] _Ramp09 ("Ramp 09 - Wave Foam Threshold", 2D) = "white" {}
        [NoScaleOffset] _Ramp10 ("Ramp 10 - Wave Foam Shape", 2D) = "white" {}
        [NoScaleOffset] _Ramp11 ("Ramp 11 - Transmission Gloss", 2D) = "white" {}

        [Header(Debug)]
        [IntRange] _DebugMode ("Debug View (0 Final, 1 Base, 2 Crest, 3 Fresnel, 4 Highlight, 5 Transmission, 6 Foam, 7 Distance)", Range(0, 7)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "OceanParityForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 vertexColor : TEXCOORD2;
                float objectHeight : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_Ramp01);
            TEXTURE2D(_Ramp02);
            TEXTURE2D(_Ramp03);
            TEXTURE2D(_Ramp04);
            TEXTURE2D(_Ramp05);
            TEXTURE2D(_Ramp06);
            TEXTURE2D(_Ramp07);
            TEXTURE2D(_Ramp08);
            TEXTURE2D(_Ramp09);
            TEXTURE2D(_Ramp10);
            TEXTURE2D(_Ramp11);
            SAMPLER(sampler_Ramp02);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CrestColor;
                half4 _FresnelColor;
                half4 _TransmissionColor;
                half4 _FarOceanColor;
                half4 _ContactFoamColor;
                float4 _FoamNoiseOffset;
                float _CrestHeightScale;
                float _CrestHeightOffset;
                float _CrestStrength;
                float _FresnelIOR;
                float _FresnelStrength;
                float _ShadowStrength;
                float _BroadHighlightPower;
                float _HighlightStrength;
                float _HighlightSignalPower;
                float _TransmissionStrength;
                float _TransmissionGlossPower;
                float _TransmissionNormalFlattening;
                float _MainLightShadowInfluence;
                float _WaveFoamBrightness;
                float _FoamAttributeScale;
                float _WaveFoamStrength;
                float _FoamNoiseScale;
                float _FoamNoiseStrength;
                float _FoamDistanceFadeStart;
                float _FoamDistanceFadeEnd;
                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                float _DistanceFadeStrength;
                float _BlenderDistanceDepth;
                float _ContactFoamRange;
                float _ContactFoamEvolutionSpeed;
                float _ContactFoamStrength;
                float _WaterTransparencyRange;
                float _WaterReflectionFactor;
                float _DebugMode;
            CBUFFER_END

            #define SAMPLE_RAMP(textureName, value) \
                SAMPLE_TEXTURE2D(textureName, sampler_Ramp02, float2(saturate(value), 0.5)).r

            float Hash21(float2 coordinate)
            {
                coordinate = frac(coordinate * float2(123.34, 456.21));
                coordinate += dot(coordinate, coordinate + 45.32);
                return frac(coordinate.x * coordinate.y);
            }

            float ValueNoise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 local = frac(coordinate);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float InverseLerpClamped(float startValue, float endValue, float value)
            {
                return saturate((value - startValue) / max(abs(endValue - startValue), 0.0001));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.vertexColor = input.color;
                output.objectHeight = input.positionOS.y;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lightDirectionWS = normalize(mainLight.direction);
                half3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);

                half ndotV = saturate(dot(normalWS, viewDirectionWS));
                half ndotH = saturate(dot(normalWS, halfDirectionWS));

                float crestInput = input.objectHeight * _CrestHeightScale + _CrestHeightOffset;
                half crestMask = (1.0h - SAMPLE_RAMP(_Ramp02, crestInput)) * _CrestStrength;
                half3 waterColor = lerp(_BaseColor.rgb, _CrestColor.rgb, saturate(crestMask));

                float safeIOR = max(_FresnelIOR, 1.0001);
                float f0Ratio = (1.0 - safeIOR) / (1.0 + safeIOR);
                float f0 = f0Ratio * f0Ratio;
                half oneMinusNdotV = 1.0h - ndotV;
                half oneMinusNdotV2 = oneMinusNdotV * oneMinusNdotV;
                half oneMinusNdotV5 = oneMinusNdotV2 * oneMinusNdotV2 * oneMinusNdotV;
                half fresnelSignal = saturate(f0 + (1.0 - f0) * oneMinusNdotV5);

                half broadHighlightSignal = pow(max(ndotH, 0.0001h), _BroadHighlightPower);
                half broadHighlightMask = SAMPLE_RAMP(_Ramp03, broadHighlightSignal);
                half broadFresnelMask = SAMPLE_RAMP(_Ramp05, fresnelSignal);
                half shadowSeed = lerp(broadFresnelMask, 1.0h, broadHighlightMask);
                half shadowTone = SAMPLE_RAMP(_Ramp04, shadowSeed);
                half shadowAttenuation = lerp(1.0h, mainLight.shadowAttenuation, _MainLightShadowInfluence);
                waterColor *= lerp(1.0h, shadowTone * shadowAttenuation, _ShadowStrength);

                half fresnelMask = SAMPLE_RAMP(_Ramp06, fresnelSignal) * _FresnelStrength;
                waterColor = lerp(waterColor, _FresnelColor.rgb, saturate(fresnelMask));

                // Blender used a separately blurred named normal. The FBX does not carry that
                // vector attribute, so flattening toward world up is the explicit Phase A proxy.
                half3 transmissionNormalWS = normalize(lerp(
                    normalWS,
                    half3(0.0h, 1.0h, 0.0h),
                    _TransmissionNormalFlattening));
                half signedTransmissionNdotL = dot(transmissionNormalWS, lightDirectionWS);
                half transmissionFacing = SAMPLE_RAMP(
                    _Ramp07,
                    signedTransmissionNdotL * 0.5h + 0.5h);
                half transmissionNdotH = saturate(dot(transmissionNormalWS, halfDirectionWS));
                half transmissionGlossSignal = pow(
                    max(transmissionNdotH, 0.0001h),
                    _TransmissionGlossPower);
                half transmissionGloss = SAMPLE_RAMP(_Ramp11, transmissionGlossSignal);
                half transmissionMask = saturate(transmissionFacing * transmissionGloss * _TransmissionStrength);
                waterColor = lerp(waterColor, _TransmissionColor.rgb, transmissionMask);

                half sharpHighlightSignal = pow(max(ndotH, 0.0001h), _HighlightSignalPower);
                half highlightMask = SAMPLE_RAMP(_Ramp08, sharpHighlightSignal) * shadowAttenuation;
                half3 highlightColor = _HighlightStrength.xxx * mainLight.color;
                waterColor = lerp(waterColor, highlightColor, saturate(highlightMask));

                half exportedFoam = input.vertexColor.r;
                half foamThreshold = SAMPLE_RAMP(_Ramp09, exportedFoam * _FoamAttributeScale);
                float foamNoise = ValueNoise(input.positionWS.xz * _FoamNoiseScale + _FoamNoiseOffset.xy);
                float foamNoiseRemap = lerp(-0.599999964, 1.0, foamNoise);
                half foamShape = SAMPLE_RAMP(_Ramp10, foamThreshold * foamNoiseRemap * _FoamNoiseStrength);
                float cameraDistance = distance(_WorldSpaceCameraPos, input.positionWS);
                half foamDistanceFade = 1.0h - InverseLerpClamped(
                    _FoamDistanceFadeStart,
                    _FoamDistanceFadeEnd,
                    cameraDistance);
                half foamMask = saturate(foamShape * _WaveFoamStrength * foamDistanceFade);
                waterColor = lerp(waterColor, _WaveFoamBrightness.xxx, foamMask);

                float viewDepth = -TransformWorldToView(input.positionWS).z;
                half distanceFade = InverseLerpClamped(
                    _DistanceFadeStart,
                    _DistanceFadeEnd,
                    viewDepth) * _DistanceFadeStrength;
                waterColor = lerp(waterColor, _FarOceanColor.rgb, saturate(distanceFade));

                if (_DebugMode > 0.5 && _DebugMode < 1.5)
                    return half4(_BaseColor.rgb, 1.0h);
                if (_DebugMode > 1.5 && _DebugMode < 2.5)
                    return half4(crestMask.xxx, 1.0h);
                if (_DebugMode > 2.5 && _DebugMode < 3.5)
                    return half4(fresnelMask.xxx, 1.0h);
                if (_DebugMode > 3.5 && _DebugMode < 4.5)
                    return half4(highlightMask.xxx, 1.0h);
                if (_DebugMode > 4.5 && _DebugMode < 5.5)
                    return half4(transmissionMask.xxx, 1.0h);
                if (_DebugMode > 5.5 && _DebugMode < 6.5)
                    return half4(foamMask.xxx, 1.0h);
                if (_DebugMode > 6.5)
                    return half4(distanceFade.xxx, 1.0h);

                return half4(MixFog(waterColor, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
