Shader "ProjectSD/UI/KawaseBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Blur Offset", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            Name "KawaseBlur"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Offset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float o = _Offset;

                half4 c = 0;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( o,  o) * texel);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-o,  o) * texel);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( o, -o) * texel);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-o, -o) * texel);
                return c * 0.25;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
