// Far trees (Ui/DrawDistance.FarTrees.cs): the model's texture times the game's light for outdoor objects (its ambient
// and a share of the sun, as the billboards are lit), cut out where the game cuts out (leaves), then the game's fog.
// Compiled by Shaders/build.cmd.
sampler picture : register(s0);

float4 light : register(c0);    // the light (rgb), cut out (1 or 0)
float4 fogColour : register(c1); // rgb

float4 main(float2 uv : TEXCOORD0, float fogged : TEXCOORD1) : COLOR {
    float4 t = tex2D(picture, uv);

    if (light.w > 0.5) {
        clip(t.a - 0.5);
    }

    return float4(lerp(fogColour.rgb, t.rgb * light.rgb, fogged), 1);
}
