Shader "Hidden/CubemapToTexture2D"
{
	Properties
	{
		_MainTex("InCube", CUBE) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "BakedReflectionsCommon.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			float4 _vertexTransform;
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				o.vertex.xy = o.vertex.xy * 0.5 + 0.5;

				o.vertex.xy = _vertexTransform.xy * o.vertex.xy + _vertexTransform.zw;

				o.vertex.xy = o.vertex.xy * 2.0 - 1.0;
				o.uv = v.uv;
				return o;
			}
			


			samplerCUBE _MainTex;
			float4x4 normalToHemisphere;
			fixed4 frag (v2f i) : SV_Target
			{

#ifdef BAKED_REFLECTIONS_HEMISPHERE_MODE
				float3 normal = mul((float3x3)normalToHemisphere, hemioct_to_float32x3(i.uv * 2.0 - 1.0));
#else
				float3 normal = oct_to_float32x3(i.uv * 2.0 - 1.0);
#endif

				return texCUBE(_MainTex, normal);
			}
			ENDCG
		}
	}
}
