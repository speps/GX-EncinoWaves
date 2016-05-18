half4 LightingSimpleSpecular(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
	half3 h = normalize(lightDir + viewDir);

	half diff = max(0, dot(s.Normal, lightDir));

	float nh = max(0, dot(s.Normal, h));
	float spec = pow(nh, 48.0);

	half4 c;
	c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * spec) * atten;
	c.a = s.Alpha;
	return c;
}