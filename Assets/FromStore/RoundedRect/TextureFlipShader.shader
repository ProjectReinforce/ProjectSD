Shader "Custom/TextureFlipShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_FlipHorizontal("Flip Horizontal", Float) = 0.0
		_FlipVertical("Flip Vertical", Float) = 0.0
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				// include UnityCG.cginc for common functions
				#include "UnityCG.cginc"

				struct appdata_t
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					UNITY_FOG_COORDS(1)
					float4 vertex : SV_POSITION;
				};

				sampler2D _MainTex;
				float _FlipHorizontal;
				float _FlipVertical;

				v2f vert(appdata_t IN)
				{
					v2f OUT;
					OUT.vertex = UnityObjectToClipPos(IN.vertex);
					OUT.uv = IN.uv;
					return OUT;
				}

				fixed4 frag(v2f IN) : SV_Target
				{
					// Flip UV coordinates
					float2 flippedUV = IN.uv;
					flippedUV.x = _FlipHorizontal > 0.5 ? (1.0 - flippedUV.x) : flippedUV.x;
					flippedUV.y = _FlipVertical > 0.5 ? (1.0 - flippedUV.y) : flippedUV.y;

					// Sample texture
					fixed4 col = tex2D(_MainTex, flippedUV);

					return col;
				}
				ENDCG
			}
		}
}
