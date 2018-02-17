// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/ExtractPoisition"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Cull Off
		Pass
		{
			CGPROGRAM
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#pragma vertex vert
			//#pragma geometry geom
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD1;
			};

			struct v2f
			{
				float3 worldPosition : COLOR;
				float3 worldNormal : COLOR1;
				float4 worldTangent: COLOR2;
				float4 pos : SV_POSITION;
			};
			

			v2f vert (appdata v)
			{
				v2f o;
				float2 fullscreenUV = v.uv;
				fullscreenUV = fullscreenUV * 2.0 - 1.0;

				o.pos = float4(fullscreenUV, 0, 1);
				
				o.worldPosition = mul(unity_ObjectToWorld, v.vertex).rgb;
				o.worldNormal = UnityObjectToWorldNormal(v.normal).rgb;

				o.worldTangent.w = v.tangent.w * unity_WorldTransformParams.w;
				o.worldTangent.xyz = UnityObjectToWorldDir(v.tangent.xyz) ;
				return o;
			}

			[maxvertexcount(3)]
			void geom(triangle v2f p[3], inout TriangleStream<v2f> triStream)
			{
				v2f p0 = p[0];
				v2f p1 = p[1];
				v2f p2 = p[2];
				// "do" conservative raserization 
				// source: https://github.com/otaku690/SparseVoxelOctree/blob/master/WIN/SVO/shader/voxelize.geom.glsl
				//Next we enlarge the triangle to enable conservative rasterization
				float4 AABB;
				float2 hPixel = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
				float pl = 1.4142135637309 / (max(_ScreenParams.x, _ScreenParams.y) / 2.0);

				//calculate AABB of this triangle
				AABB.xy = p0.pos.xy;
				AABB.zw = p0.pos.xy;

				AABB.xy = min(p1.pos.xy, AABB.xy);
				AABB.zw = max(p1.pos.xy, AABB.zw);

				AABB.xy = min(p2.pos.xy, AABB.xy);
				AABB.zw = max(p2.pos.xy, AABB.zw);

				//Enlarge half-pixel
				AABB.xy -= hPixel;
				AABB.zw += hPixel;

				//find 3 triangle edge plane
				float3 e0 = float3(p1.pos.xy - p0.pos.xy, 0);
				float3 e1 = float3(p2.pos.xy - p1.pos.xy, 0);
				float3 e2 = float3(p0.pos.xy - p2.pos.xy, 0);
				float3 n0 = cross(e0, float3(0, 0, 1));
				float3 n1 = cross(e1, float3(0, 0, 1));
				float3 n2 = cross(e2, float3(0, 0, 1));

				//dilate the triangle
				// julian: I can't figure out why the dilate-offset sometimes produces insane distorted triangels
				// so I normalize the offset, which works pretty well so far
				p0.pos.xy += pl*normalize((e2.xy / dot(e2.xy, n0.xy)) + (e0.xy / dot(e0.xy, n2.xy)));
				p1.pos.xy += pl*normalize((e0.xy / dot(e0.xy, n1.xy)) + (e1.xy / dot(e1.xy, n0.xy)));
				p2.pos.xy += pl*normalize((e1.xy / dot(e1.xy, n2.xy)) + (e2.xy / dot(e2.xy, n1.xy)));

				triStream.Append(p0);
				triStream.Append(p1);
				triStream.Append(p2);

				triStream.RestartStrip();
			}

			struct RenderTargets
			{
				float4 worldPos : SV_Target0;
				float4 worldNormal : SV_Target1;
				float4 worldTangent : SV_Target2;
			};


			RenderTargets frag (v2f i)
			{
				float3 worldPos = i.worldPosition;

				// calculate derivate to get the position that is exacly on uv
				float2x3 ddWorldspace = float2x3(ddx(worldPos), ddy(worldPos));
				float3 offset = ddWorldspace[0] + ddWorldspace[1];
				offset /= 2.0;
				
				//worldPos -= offset;
				RenderTargets o;
				o.worldPos = float4(worldPos /*+ i.worldNormal * 1.05*/, 1) ;
				o.worldNormal = float4(i.worldNormal, 1);
				o.worldTangent = i.worldTangent;
				return o;
			}
			ENDCG
		}
	}
}
