// Glow (Ui/DepthShading.Glow.cs): a blur one way (nine samples, Gaussian), times a strength. Run across the quarter-size
// picture of what glows, then down it onto the screen, added (blend one, one). Compiled by Shaders/build.cmd.
sampler picture : register(s1);

float4 settings : register(c0);  // the step between samples (u, v), strength

static const float Weights[5] = { 0.2270, 0.1945, 0.1216, 0.0541, 0.0162 };

float4 main(float2 uv : TEXCOORD0) : COLOR {
    float3 sum = tex2D(picture, uv).rgb * Weights[0];

    for (int i = 1; i < 5; i++) {
        float2 offset = settings.xy * i;
        sum += (tex2D(picture, uv + offset).rgb + tex2D(picture, uv - offset).rgb) * Weights[i];
    }

    return float4(sum * settings.z, 1);
}
