﻿Shader "Encino/EncinoOcean"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Displacement("Displacement", Range(0, 1.0)) = 0.3
		_EdgeLength("Tessellation", Range(1,128)) = 4
		_Cube("Reflection Cubemap", Cube) = "_Skybox" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf Standard vertex:vert tessellate:tess nolightmap
		#pragma target 5.0
		#pragma enable_d3d11_debug_symbols
		#include "Tessellation.cginc"
		#include "EncinoOcean.cginc"

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
		float3 _SnappedWorldPosition;
		float3 _SnappedUVPosition;
		float3 _ViewOrigin;
		float _DomainSize;
		float _InvDomainSize;

		struct Input
		{
			float2 uv_MainTex;
			float3 worldPos;
			float3 worldRefl;
			INTERNAL_DATA
		};

		float computeWeight(float3 worldPos)
		{
			float d = distance(worldPos, float3(_SnappedWorldPosition.x, _ViewOrigin.y, _SnappedWorldPosition.z));
			float w = saturate(d * _InvDomainSize * 2.0f);
			return smoothstep(0.0f, 0.1f, 1.0f - w);
		}

		void vert(inout appdata v)
		{
			float2 uv = _SnappedUVPosition.xz + v.texcoord.xy;
			float3 displacement = tex2Dlod(_DispTex, float4(uv, 0, 0)).xyz;
			float w = computeWeight(mul(unity_ObjectToWorld, v.vertex));
			v.vertex.xyz += displacement * float3(_Choppiness, _Displacement * w, _Choppiness);
		}

		sampler2D _MainTex;
		sampler2D _NormalMap;
		float _NormalTexelSize;
		float4 _Color;
		float4 _ColorFoam;
		float _Metal;
		float _Smoothness;

		void surf(Input v, inout SurfaceOutputStandard o)
		{
			float2 uv = _SnappedUVPosition.xz + v.uv_MainTex;
			float4 grad = tex2D(_NormalMap, uv);
			float foam = grad.w * grad.w;
			float4 c = tex2D(_MainTex, uv) * lerp(_Color, _ColorFoam, foam);
			float3 n = normalize(float3(grad.xy, _NormalTexelSize));
			float w = computeWeight(v.worldPos);
			if (w == 0.0f)
			{
				discard;
			}
			o.Albedo = c;
			o.Normal = n;
			o.Smoothness = lerp(_Smoothness, 0, foam);
		}
		ENDCG
	}
	FallBack Off
}
