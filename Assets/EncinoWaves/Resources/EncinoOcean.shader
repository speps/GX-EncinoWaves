Shader "Custom/EncinoOcean"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_HeightTex ("Height Texture", 2D) = "black" {}
		_DispXTex ("Disp X Texture", 2D) = "black" {}
		_DispYTex ("Disp Y Texture", 2D) = "black" {}
		_NormalMap ("Normalmap", 2D) = "bump" {}
		_Color ("Color", color) = (1,1,1,0)
		_SpecColor ("Spec color", color) = (0.5,0.5,0.5,0.5)
		_Displacement ("Displacement", Range(0, 1.0)) = 0.3
		_Choppiness ("Choppiness", Range(0, 10.0)) = 1
		_EdgeLength ("Tessellation", Range(1,128)) = 4
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf BlinnPhong addshadow fullforwardshadows vertex:disp tessellate:tess nolightmap
		#pragma target 5.0
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

		float4 tess(appdata v0, appdata v1, appdata v2)
		{
			return UnityEdgeLengthBasedTessCull(v0.vertex, v1.vertex, v2.vertex, _EdgeLength, _Displacement);
		}

		sampler2D _HeightTex;
		sampler2D _DispXTex;
		sampler2D _DispYTex;
		float _Choppiness;
		float _DomainSize;
		float4 _SnappedWorldPosition;

		void disp(inout appdata v)
		{
			float2 uv = _SnappedWorldPosition.xz + v.texcoord.xy;
			float4 d = float4(
				tex2Dlod(_DispXTex, float4(uv, 0, 0)).r * _Choppiness,
				tex2Dlod(_HeightTex, float4(uv, 0, 0)).r * _Displacement,
				tex2Dlod(_DispYTex, float4(uv, 0, 0)).r * _Choppiness,
				0
			);
			v.vertex += d;
		}

		struct Input
		{
			float2 uv_MainTex;
		};

		sampler2D _MainTex;
		sampler2D _NormalMap;
		float4 _Color;

		void surf(Input IN, inout SurfaceOutput o)
		{
			float2 uv = _SnappedWorldPosition.xz + IN.uv_MainTex;
			float4 c = tex2D(_MainTex, uv) * _Color;
			o.Albedo = c.rgb;
			o.Specular = 0.2;
			o.Gloss = 1.0;
			//o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
		}
		ENDCG
	}
	FallBack Off
}
