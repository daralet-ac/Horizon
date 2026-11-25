// Sun shadows (Ui/DepthShading.Shadows.cs): the shadow map's depth, from the sun, 0 near it and 1 far. A draw the game
// cuts out by alpha (leaves, fences) is cut out here too, so its shadow has the same holes. Compiled by Shaders/build.cmd.
sampler picture : register(s0);

float4 cutOut : register(c0);  // 1 when the game alpha tests this draw

float4 main(float2 uv : TEXCOORD0, float depth : TEXCOORD1) : COLOR {
    if (cutOut.x > 0.5) {
        clip(tex2D(picture, uv).a - 0.5);
    }

    return float4(depth, 0, 0, 1);
}
