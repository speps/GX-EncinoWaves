Shader "Encino/EncinoOceanExtended"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Color ("Color", color) = (1,1,1,0)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf Standard addshadow fullforwardshadows nolightmap
		#pragma target 5.0
		#pragma enable_d3d11_debug_symbols

		struct Input
		{
			float2 uv_MainTex;
			float3 worldPos;
		};

		sampler2D _MainTex;
		sampler2D _DispTex;
		sampler2D _NormalMap;
		float _Choppiness;
		float _InvDomainSize;
		float _NormalTexelSize;
		float4 _Color;

		void surf(Input v, inout SurfaceOutputStandard o)
		{
			float2 uv = v.worldPos.xz * _InvDomainSize;
			float4 d = tex2D(_DispTex, uv);
			float2 uvd = v.worldPos.xz * _InvDomainSize + d.xz * -_Choppiness;
			float4 c = tex2D(_MainTex, uvd) * _Color;
			float4 grad = tex2D(_NormalMap, uvd);
			float3 n = normalize(float3(grad.xy, _NormalTexelSize));
			o.Albedo = c;
			o.Normal = n;
		}
		ENDCG
	}
	FallBack Off
}
