Shader "Encino/EncinoOcean"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Color ("Color", color) = (1,1,1,0)
		_Displacement ("Displacement", Range(0, 1.0)) = 0.3
		_EdgeLength ("Tessellation", Range(1,128)) = 4
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf Standard addshadow fullforwardshadows vertex:vert tessellate:tess nolightmap
		#pragma target 5.0
		#pragma enable_d3d11_debug_symbols
		#include "Tessellation.cginc"

		struct appdata
		{
			float4 vertex : POSITION;
			float4 tangent : TANGENT;
			float3 normal : NORMAL;
			float2 texcoord : TEXCOORD0;
		};

		float _EdgeLength;
		float _Displacement;
		float _Choppiness;

		float4 tess(appdata v0, appdata v1, appdata v2)
		{
			return UnityEdgeLengthBasedTess(v0.vertex, v1.vertex, v2.vertex, _EdgeLength);
		}

		sampler2D _DispTex;
		float4 _SnappedWorldPosition;

		struct Input
		{
			float2 uv_MainTex;
		};

		void vert(inout appdata v)
		{
			float2 uv = _SnappedWorldPosition.xz + v.texcoord.xy;
			float3 displacement = tex2Dlod(_DispTex, float4(uv, 0, 0)).xyz;
			v.vertex.xyz += displacement * float3(_Choppiness, _Displacement, _Choppiness);
		}

		sampler2D _MainTex;
		sampler2D _NormalMap;
		float _NormalTexelSize;
		float4 _Color;

		void surf(Input v, inout SurfaceOutputStandard o)
		{
			float2 uv = _SnappedWorldPosition.xz + v.uv_MainTex;
			float4 c = tex2D(_MainTex, uv) * _Color;
			float4 grad = tex2D(_NormalMap, uv);
			float3 n = normalize(float3(grad.xy, _NormalTexelSize));
			o.Albedo = c;
			o.Normal = n;
		}
		ENDCG
	}
	FallBack Off
}
