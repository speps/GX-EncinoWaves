Shader "Encino/EncinoOceanExtended"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Cube("Reflection Cubemap", Cube) = "_Skybox" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 300
		Cull Off

		CGPROGRAM
		#pragma surface surf Standard nolightmap
		#pragma target 5.0
		#pragma enable_d3d11_debug_symbols
		#include "EncinoOcean.cginc"

		struct Input
		{
			float2 uv_MainTex;
			float3 worldPos;
			float3 worldRefl;
			INTERNAL_DATA
		};

		sampler2D _MainTex;
		sampler2D _DispTex;
		sampler2D _NormalMap;
		float3 _SnappedWorldPosition;
		float3 _ViewOrigin;
		float _Choppiness;
		float _DomainSize;
		float _InvDomainSize;
		float _NormalTexelSize;
		float4 _Color;
		float4 _ColorFoam;
		float _Metal;
		float _Smoothness;

		float computeWeight(float3 worldPos)
		{
			float d = distance(worldPos, float3(_SnappedWorldPosition.x, _ViewOrigin.y, _SnappedWorldPosition.z)) - _DomainSize * 0.5f;
			float w = saturate(d * _InvDomainSize * 1.0f);
			return smoothstep(0.0f, 0.1f, w);
		}

		void surf(Input v, inout SurfaceOutputStandard o)
		{
			float2 uv = v.worldPos.xz * _InvDomainSize;
			float4 d = tex2D(_DispTex, uv);
			float2 uvd = v.worldPos.xz * _InvDomainSize + d.xz * -_Choppiness;
			float4 grad = tex2D(_NormalMap, uvd);
			float foam = grad.w * grad.w;
			float4 c = tex2D(_MainTex, uvd) * lerp(_Color, _ColorFoam, foam);
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
