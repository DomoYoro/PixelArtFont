Shader "UI/PixelArtFont" 
{
	Properties
	{
		_MainTex("Font Atlas", 2D) = "white" {}
		
		//Req for UI masking
		_Stencil("Stencil ID", Float) = 0
		_StencilComp("StencilComp", Float) = 8
		_StencilOp("StencilOp", Float) = 0
		_StencilReadMask("StencilReadMask", Float) = 255
		_StencilWriteMask("StencilWriteMask", Float) = 255
		_CullMode ("Cull Mode", Float) = 0
		_ColorMask("ColorMask", Float) = 15
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		ZTest [unity_GUIZTestMode] //Req for UI masking
		ZWrite off
		Cull [_CullMode]
		Lighting Off

		Blend SrcAlpha OneMinusSrcAlpha

		//Req for UI masking
		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		ColorMask [_ColorMask]
		
		Pass
		{
			Name "PixelArtFont"
			HLSLPROGRAM
						
			#pragma target 2.0
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "UnityUI.cginc"
			
			float4 _ClipRect;
			sampler2D _MainTex;
			float4 _MainTex_ST;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f 
			{
				float4 position : SV_POSITION;
				float4 worldPosition : TEXCOORD1;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.worldPosition = v.vertex;
				o.position = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 c = tex2D(_MainTex, i.uv) * i.color;
				
				//Req for UI rect masking
				c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
				
				#ifdef UNITY_UI_ALPHACLIP
				clip (c.a - 0.001);
				#endif
				
				return c;
			}

			ENDHLSL
		}
	}
	Fallback Off
}
