#include "UnityStandardBRDF.cginc"

samplerCUBE _Cube;

half4 LightingOcean(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
	half3 h = normalize(lightDir + viewDir);
	float nh = dot(s.Normal, h);
	nh *= nh;
	float spec = pow(nh, s.Specular);

	float3 upwelling = float3(0, 0.2, 0.3);
	float3 air = float3(0.1,0.1,0.1);
	float nSnell = 1.34;
	float Kdiffuse = 0.91;
	float reflectivity;
	float3 nI = normalize(viewDir);
	float3 nN = normalize(s.Normal);
	float costhetai = abs(dot(nI, nN));
	float thetai = acos(costhetai);
	float sinthetat = sin(thetai)/nSnell;
	float thetat = asin(sinthetat);
	if(thetai == 0.0)
	{
		reflectivity = (nSnell - 1)/(nSnell + 1);
		reflectivity = reflectivity * reflectivity;
	}
	else
	{
		float fs = sin(thetat - thetai) / sin(thetat + thetai);
		float ts = tan(thetat - thetai) / tan(thetat + thetai);
		reflectivity = 0.5 * (fs*fs + ts*ts);
	}
	float3 sky = texCUBE(_Cube, nN);
	float3 sun = texCUBE(_Cube, lightDir);

	float3 Ci = reflectivity * sky + (1-reflectivity) * upwelling + spec * reflectivity * sun;

	return float4(Ci * atten, 1);
}
