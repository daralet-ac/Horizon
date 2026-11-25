// Grass and reeds (Ui/Grass.cs): the blade picture times its ground's colour and the light, cut out where the picture
// is clear and, toward the radius's edge, where the fade has thinned it. Compiled by Shaders/build.cmd.
sampler blade : register(s0);

float4 main(float4 colour : COLOR0, float2 uv : TEXCOORD0) : COLOR {
    float4 picture = tex2D(blade, uv);
    clip(picture.a * colour.a - 0.5);
    return float4(picture.rgb * colour.rgb, 1);
}
