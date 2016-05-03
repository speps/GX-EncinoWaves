Shader "Custom/EncinoOcean"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_DispTex ("Disp Texture", 2D) = "gray" {}
		_NormalMap ("Normalmap", 2D) = "bump" {}
		_Color ("Color", color) = (1,1,1,0)
		_SpecColor ("Spec color", color) = (0.5,0.5,0.5,0.5)
		_Displacement ("Displacement", Range(0, 1.0)) = 0.3
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

		float4 tess(appdata v0, appdata v1, appdata v2)
		{
			return UnityEdgeLengthBasedTessCull(v0.vertex, v1.vertex, v2.vertex, _EdgeLength, 0.0f);
		}

		sampler2D _DispTex;
		float _Displacement;
		float _DomainSize;
		float4 _SnappedWorldPosition;

		void disp(inout appdata v)
		{
			float2 uv = _SnappedWorldPosition.xz + v.texcoord.xy;
			float d = tex2Dlod(_DispTex, float4(uv, 0, 0)).r * _Displacement;
			v.vertex.y += d;
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
