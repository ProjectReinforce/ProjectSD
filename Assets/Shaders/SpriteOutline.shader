// Sweepin' Dreams - URP 2D Sprite Outline Shader
// 사용법:
// 1. 이 파일을 Assets/Shaders/ 에 배치
// 2. 새 Material 생성 → Shader를 "SwDreams/SpriteOutline" 으로 설정
// 3. 캐릭터/적 SpriteRenderer의 Material에 적용
// 4. 인스펙터에서 _OutlineColor, _OutlineThickness 조정
//
// 특징:
// - URP 2D Renderer 호환
// - 스프라이트 외곽선만 그림 (투명 영역에 인접한 불투명 픽셀 경계)
// - 런타임에서 MaterialPropertyBlock으로 개별 제어 가능
// - _EnableOutline 토글로 on/off
// - _FlashColor + _FlashAmount로 피격 플래시도 지원 (Color 방식보다 깔끔)

Shader "SwDreams/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0, 1, 1, 1)
        _OutlineThickness ("Outline Thickness (px)", Range(0, 5)) = 1
        [Toggle] _EnableOutline ("Enable Outline", Float) = 1

        [Header(Hit Flash)]
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0

        // Unity sprite defaults
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // xy = 1/width, 1/height

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineThickness;
                float _EnableOutline;
                float4 _FlashColor;
                float _FlashAmount;
                float4 _RendererColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                mainColor *= input.color;

                // 아웃라인 처리
                if (_EnableOutline > 0.5 && _OutlineThickness > 0)
                {
                    // 현재 픽셀이 투명이면 주변 확인
                    if (mainColor.a < 0.1)
                    {
                        float2 offsets[8] = {
                            float2( 1,  0),
                            float2(-1,  0),
                            float2( 0,  1),
                            float2( 0, -1),
                            float2( 1,  1),
                            float2(-1, -1),
                            float2( 1, -1),
                            float2(-1,  1)
                        };

                        float maxAlpha = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            float2 sampleUV = input.uv + offsets[i] * _MainTex_TexelSize.xy * _OutlineThickness;
                            half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                            maxAlpha = max(maxAlpha, sample.a);
                        }

                        // 주변에 불투명 픽셀이 있으면 → 아웃라인
                        if (maxAlpha > 0.1)
                        {
                            mainColor = _OutlineColor;
                            mainColor.a *= maxAlpha;
                        }
                    }
                }

                // 피격 플래시: 원래 색상에 FlashColor를 FlashAmount만큼 덮어씌움
                if (_FlashAmount > 0)
                {
                    mainColor.rgb = lerp(mainColor.rgb, _FlashColor.rgb, _FlashAmount);
                }

                return mainColor;
            }
            ENDHLSL
        }

        // UniversalForward pass (씬 뷰 등에서 폴백)
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineThickness;
                float _EnableOutline;
                float4 _FlashColor;
                float _FlashAmount;
                float4 _RendererColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                mainColor *= input.color;

                if (_EnableOutline > 0.5 && _OutlineThickness > 0)
                {
                    if (mainColor.a < 0.1)
                    {
                        float2 offsets[8] = {
                            float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                            float2(1,1), float2(-1,-1), float2(1,-1), float2(-1,1)
                        };
                        float maxAlpha = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            float2 sampleUV = input.uv + offsets[i] * _MainTex_TexelSize.xy * _OutlineThickness;
                            maxAlpha = max(maxAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).a);
                        }
                        if (maxAlpha > 0.1)
                        {
                            mainColor = _OutlineColor;
                            mainColor.a *= maxAlpha;
                        }
                    }
                }

                if (_FlashAmount > 0)
                    mainColor.rgb = lerp(mainColor.rgb, _FlashColor.rgb, _FlashAmount);

                return mainColor;
            }
            ENDHLSL
        }
    }
}
