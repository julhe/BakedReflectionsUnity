// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/displayBakedReflection"
{
	Properties
	{
		_ReflectionArray ("Texture", 2D) = "white" {}
		_Smoothness("_Smoothness", Range(0,1)) = 1
		_sliceSize("_sliceSize", float) = 0
		_slicesPerAxis("_slicesPerAxis", float) = 0
		_MainTex("MainTex", 2D) = "white" { }
		_Gradient("_BumpMap", 2D) = "bump" { }	
		_BakedReflectionParams("slice Size", Vector) = (0, 0, 0)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			#pragma target 5.0
			#include "UnityCG.cginc"
			#include "BakedReflectionsCommon.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD1;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				half3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
				half3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
				half3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
				float3 worldPos : TEXCOORD4;
				UNITY_FOG_COORDS(5)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v, float3 normal : NORMAL, float4 tangent : TANGENT)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);

				half3 wNormal = UnityObjectToWorldNormal(normal);
				half3 wTangent = UnityObjectToWorldDir(tangent.xyz);

				// compute bitangent from cross product of normal and tangent
				half tangentSign = tangent.w * unity_WorldTransformParams.w;
				half3 wBitangent = cross(wNormal, wTangent) * tangentSign;

				// output the tangent space matrix
				o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
				o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
				o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			sampler2D _ReflectionArray;
			sampler2D _Gradient;
			float _Smoothness;
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the normal map, and decode from the Unity encoding
				half3 tnormal = UnpackNormal(tex2D(_Gradient, i.uv * 10));

				// transform normal from tangent to world space
				half3 worldNormal;
				float3x3 tangentToWorld = float3x3(i.tspace0, i.tspace1, i.tspace2);
				worldNormal = mul(tangentToWorld, tnormal);

				float3x3 worldToTangent = transpose(tangentToWorld);
			
				float3 viewDir = -normalize(UnityWorldSpaceViewDir(i.worldPos));
				float3 reflectionVector = reflect(viewDir, worldNormal);
				float3 tangentView = mul(worldToTangent, reflectionVector);

				float smoothness = _Smoothness;
				//return normalToLatLong(reflectionVector).xyxy;
				float3 reflection;
#ifdef BAKED_REFLECTIONS_HEMISPHERE_MODE
				reflection = SampleBakedReflection(_ReflectionArray, tangentView, i.uv, smoothness).rgb;
#else
				reflection = SampleBakedReflection(_ReflectionArray, reflectionVector, i.uv, smoothness).rgb;
#endif

				float4 outColor = float4(reflection, 1.0); 
			//	return float4(reflectionVector.xy * 0.5 + 0.5, 0, 1);
			//	outColor *= 0.67;
				float fresnel = saturate(0.67 + pow(1.0 - saturate(dot(viewDir, worldNormal)), 5));
				//UNITY_APPLY_FOG(i.fogCoord, outColor);

				return outColor;
			}
			ENDCG
		}
	}
}
