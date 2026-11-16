Shader "Editor/Dropshadow Blit" 
{
	Properties
	{
		_MainTex("Base Sprite", 2D) = "white" {}
		_DropshadowColor("Dropshadow Color", Color) = (0, 0, 0, 1)
		_DropshadowOffset("Dropshadow Offset", Vector) = (1, -1, 0, 0)
		[Toggle] _SingleChannel("R8 Texture", float) = 1
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
		}

		ZTest Always
		ZWrite off
		Cull Off
		Lighting Off

		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM
			
			#pragma target 2.0
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				half4 color : COLOR;
			};

			struct v2f 
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				half4 color : COLOR;
			};
			
			sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;
			
			half4 _DropshadowColor;
			half2 _DropshadowOffset;
			float _SingleChannel;
			
			v2f vert(appdata v)
			{
				v2f o;
				
				o.position = UnityObjectToClipPos(v.vertex.xyz);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;

				return o;
			}

			half4 GetColorFromPixelOffset(float2 pos, float2 offset) 
			{
				float2 newPos = pos + offset;
				float2 newUV = newPos * _MainTex_TexelSize.xy;
				return tex2D(_MainTex, newUV);
			}
			
			inline bool HasPixel(half4 value) 
			{
				return _SingleChannel ? value.r > 0 : value.a > 0;
			}
			
			inline half4 Eval(half4 original, half4 value, half4 newColor) 
			{
				return HasPixel(value) ? newColor : original;
			}
			
			half4 frag(v2f i) : SV_Target
			{
				half4 c = tex2D(_MainTex, i.uv);
				
				if (HasPixel(c)) return c;
				
				//Position in pixels
				float2 pos = i.uv * _MainTex_TexelSize.zw;
				
				half4 c2 = GetColorFromPixelOffset(pos, -_DropshadowOffset);
				c = Eval(c, c2, _DropshadowColor);
				
				return c;
			}

			ENDHLSL
		}
	}

	Fallback Off
}
