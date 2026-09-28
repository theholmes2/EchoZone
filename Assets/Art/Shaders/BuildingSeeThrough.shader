Shader "EchoZone/Building See Through"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
        [HideInInspector] _SeeThroughWholeFade("Whole Building Fade", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        // 투과되지 않는 픽셀만 깊이에 먼저 기록합니다. 이렇게 하면 한 건물 안의
        // 뒤쪽 벽과 창문이 앞면 위에 다시 그려지는 투명 정렬 문제를 막으면서도,
        // 캐릭터/FOV 마스크 영역은 깊이를 쓰지 않아 기존 시스루가 유지됩니다.
        Pass
        {
            Name "OpaqueDepthPrepass"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _SeeThroughWholeFade;
            CBUFFER_END

            float3 _SeeThroughCameraPosition;
            float3 _SeeThroughPlayerPosition;
            float _SeeThroughPlayerDepthBias;
            float3 _SeeThroughPlayerViewportPosition;
            float _SeeThroughPlayerScreenRadius;
            float _SeeThroughPlayerEdgeSoftness;
            float3 _SeeThroughPlayerForward;
            float3 _SeeThroughPlayerRight;
            float _SeeThroughFieldOfViewAngle;
            float _SeeThroughFieldOfViewSoftness;
            int _SeeThroughLosRayCount;
            float _SeeThroughLosDistances[64];

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.screenPosition = ComputeScreenPos(positions.positionCS);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float2 playerScreenDelta = screenUv - _SeeThroughPlayerViewportPosition.xy;
                playerScreenDelta.x *= _ScreenParams.x / _ScreenParams.y;
                float characterMask = 1.0 - smoothstep(
                    _SeeThroughPlayerScreenRadius - _SeeThroughPlayerEdgeSoftness,
                    _SeeThroughPlayerScreenRadius,
                    length(playerScreenDelta));

                float3 cameraToFragment = input.positionWS - _SeeThroughCameraPosition;
                float fragmentDistance = length(cameraToFragment);
                float3 cameraRay = cameraToFragment / max(fragmentDistance, 0.0001);
                float planeDistance = (_SeeThroughPlayerPosition.y - _SeeThroughCameraPosition.y) /
                                      min(cameraRay.y, -0.0001);
                float3 projectedGround = _SeeThroughCameraPosition + cameraRay * planeDistance;
                float3 playerToGround = projectedGround - _SeeThroughPlayerPosition;
                playerToGround.y = 0.0;
                float groundDistance = length(playerToGround);
                float signedAngle = atan2(
                    dot(playerToGround, _SeeThroughPlayerRight),
                    dot(playerToGround, _SeeThroughPlayerForward));
                float absoluteAngle = abs(signedAngle);

                float fovMask = 0.0;
                if (_SeeThroughLosRayCount > 1 &&
                    planeDistance > 0.0 &&
                    fragmentDistance < planeDistance - _SeeThroughPlayerDepthBias &&
                    absoluteAngle <= _SeeThroughFieldOfViewAngle)
                {
                    float rayPosition = saturate(
                        (signedAngle + _SeeThroughFieldOfViewAngle) /
                        max(_SeeThroughFieldOfViewAngle * 2.0, 0.0001)) *
                        (_SeeThroughLosRayCount - 1);
                    int lowerIndex = clamp((int)floor(rayPosition), 0, _SeeThroughLosRayCount - 1);
                    int upperIndex = min(lowerIndex + 1, _SeeThroughLosRayCount - 1);
                    float visibleDistance = lerp(
                        _SeeThroughLosDistances[lowerIndex],
                        _SeeThroughLosDistances[upperIndex],
                        frac(rayPosition));
                    fovMask = 1.0 - smoothstep(
                        visibleDistance - _SeeThroughFieldOfViewSoftness,
                        visibleDistance,
                        groundDistance);
                }

                float cameraToPlayerDistance = distance(
                    _SeeThroughCameraPosition,
                    _SeeThroughPlayerPosition);
                float characterDepthMask = fragmentDistance <
                    cameraToPlayerDistance - _SeeThroughPlayerDepthBias ? 1.0 : 0.0;
                float revealMask = saturate(max(fovMask, characterMask * characterDepthMask));
                revealMask = max(revealMask, step(0.5, _SeeThroughWholeFade));

                clip(0.5 - revealMask);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma target 3.5
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 screenPosition : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _SeeThroughWholeFade;
            CBUFFER_END

            float _MinAlpha;
            float3 _SeeThroughCameraPosition;
            float3 _SeeThroughPlayerPosition;
            float _SeeThroughPlayerDepthBias;
            float3 _SeeThroughPlayerViewportPosition;
            float _SeeThroughPlayerScreenRadius;
            float _SeeThroughPlayerEdgeSoftness;
            float3 _SeeThroughPlayerForward;
            float3 _SeeThroughPlayerRight;
            float _SeeThroughFieldOfViewAngle;
            float _SeeThroughFieldOfViewSoftness;
            int _SeeThroughLosRayCount;
            float _SeeThroughLosDistances[64];

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.screenPosition = ComputeScreenPos(positions.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float2 playerScreenDelta = screenUv - _SeeThroughPlayerViewportPosition.xy;
                playerScreenDelta.x *= _ScreenParams.x / _ScreenParams.y;
                float playerScreenDistance = length(playerScreenDelta);
                float characterMask = 1.0 - smoothstep(
                    _SeeThroughPlayerScreenRadius - _SeeThroughPlayerEdgeSoftness,
                    _SeeThroughPlayerScreenRadius,
                    playerScreenDistance);

                float3 cameraToFragment = input.positionWS - _SeeThroughCameraPosition;
                float fragmentDistance = length(cameraToFragment);
                float3 cameraRay = cameraToFragment / max(fragmentDistance, 0.0001);
                float planeDistance = (_SeeThroughPlayerPosition.y - _SeeThroughCameraPosition.y) /
                                      min(cameraRay.y, -0.0001);
                float3 projectedGround = _SeeThroughCameraPosition + cameraRay * planeDistance;
                float3 playerToGround = projectedGround - _SeeThroughPlayerPosition;
                playerToGround.y = 0.0;
                float groundDistance = length(playerToGround);
                float forwardDistance = dot(playerToGround, _SeeThroughPlayerForward);
                float sideDistance = dot(playerToGround, _SeeThroughPlayerRight);
                float signedAngle = atan2(sideDistance, forwardDistance);
                float absoluteAngle = abs(signedAngle);

                float fovMask = 0.0;
                if (_SeeThroughLosRayCount > 1 &&
                    planeDistance > 0.0 &&
                    fragmentDistance < planeDistance - _SeeThroughPlayerDepthBias &&
                    absoluteAngle <= _SeeThroughFieldOfViewAngle)
                {
                    float rayPosition = saturate(
                        (signedAngle + _SeeThroughFieldOfViewAngle) /
                        max(_SeeThroughFieldOfViewAngle * 2.0, 0.0001)) *
                        (_SeeThroughLosRayCount - 1);
                    int lowerIndex = clamp((int)floor(rayPosition), 0, _SeeThroughLosRayCount - 1);
                    int upperIndex = min(lowerIndex + 1, _SeeThroughLosRayCount - 1);
                    float visibleDistance = lerp(
                        _SeeThroughLosDistances[lowerIndex],
                        _SeeThroughLosDistances[upperIndex],
                        frac(rayPosition));
                    fovMask = 1.0 - smoothstep(
                        visibleDistance - _SeeThroughFieldOfViewSoftness,
                        visibleDistance,
                        groundDistance);
                }

                float cameraToPlayerDistance = distance(
                    _SeeThroughCameraPosition,
                    _SeeThroughPlayerPosition);
                float characterDepthMask = fragmentDistance <
                    cameraToPlayerDistance - _SeeThroughPlayerDepthBias ? 1.0 : 0.0;
                float revealMask = saturate(max(fovMask, characterMask * characterDepthMask));
                revealMask = max(revealMask, step(0.5, _SeeThroughWholeFade));
                float alpha = lerp(1.0, saturate(_MinAlpha), revealMask);

                baseSample.rgb = MixFog(baseSample.rgb, input.fogFactor);
                baseSample.a *= alpha;
                return baseSample;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
        }
    }
}
