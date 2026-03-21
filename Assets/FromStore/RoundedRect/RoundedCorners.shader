
Shader "UI/RoundedCorners/RoundedCorners" {
    Properties {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        // --- Mask support ---
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Definition in Properties section is required to Mask works properly
        _WidthHeightRadius ("WidthHeightRadius", Vector) = (0,0,0,0)
        _OuterUV ("image outer uv", Vector) = (0, 0, 1, 1)
        // ---

		_BorderThickness("Border Thickness", Float) = 0
    }
    
    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        // --- Mask support ---
        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]
        // ---
        
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"          
            #include "SDFUtils.cginc"
            #include "ShaderSetup.cginc"
            
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            float4 _WidthHeightRadius;
            float4 _OuterUV;
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

			float _BorderThickness;

			fixed4 frag(v2f i) : SV_Target{
			   float2 uvSample = i.uv;
			   // UV 좌표 조정
			   uvSample.x = (uvSample.x - _OuterUV.x) / (_OuterUV.z - _OuterUV.x);
			   uvSample.y = (uvSample.y - _OuterUV.y) / (_OuterUV.w - _OuterUV.y);

			   half4 color = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;

			   #ifdef UNITY_UI_CLIP_RECT
			   color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
			   #endif

			   if (color.a <= 0) return color;

			   // 외부 박스의 알파값 계산
			   float outerAlpha = CalcAlpha(uvSample, _WidthHeightRadius.xy, _WidthHeightRadius.z);

			  
			   if (_BorderThickness != 0) {
				   // 내부 박스의 크기와 위치 조정
				   float2 shrunkUV = (uvSample - 0.5) * (_WidthHeightRadius.xy / (_WidthHeightRadius.xy - _BorderThickness * 2)) + 0.5;
				   float innerAlpha = CalcAlpha(shrunkUV, _WidthHeightRadius.xy - _BorderThickness * 2, max(0, _WidthHeightRadius.z - _BorderThickness));

				   outerAlpha = outerAlpha - innerAlpha;
			   }

			   #ifdef UNITY_UI_ALPHACLIP
			   clip(outerAlpha - 0.001);
			   #endif

			   return mixAlpha(tex2D(_MainTex, i.uv), i.color, outerAlpha);
			}

				ENDCG
        }
    }
}
