// Lamp shadows (Ui/DepthShading.LampShadows.cs): how far from the lamp, over its reach, cut out where the game cuts out
// (leaves, fences). Compiled by Shaders/build.cmd.
sampler picture : register(s0);

float4 cutOut : register(c0);  // 1 when the game alpha tests this draw

float4 main(float2 uv : TEXCOORD0, float3 fromLamp : TEXCOORD1) : COLOR {
    if (cutOut.x > 0.5) {
        clip(tex2D(picture, uv).a - 0.5);
    }

    return float4(length(fromLamp), 0, 0, 1);
}
