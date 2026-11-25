// Sharpening (Ui/DepthShading.Image.cs): contrast-adaptive, after AMD's FidelityFX CAS. Each pixel is pushed away from
// its four neighbours by an amount that shrinks where the neighbourhood already has strong contrast, so soft texture
// detail comes back without halos round edges that are already sharp. Over the 3D world only, before the interface.
// Compiled by Shaders/build.cmd into Resources/shaders/sharpen.ps.cso.
sampler frameColour : register(s1);

float4 strength : register(c12);   // how sharp (0-1)
float4 screen : register(c13);     // width, height, 1/width, 1/height

float3 At(float2 uv) {
    return tex2Dlod(frameColour, float4(uv, 0, 0)).rgb;
}

float4 main(float2 uv : TEXCOORD0) : COLOR {
    float2 pixel = screen.zw;
    float3 b = At(uv + float2(0, -pixel.y));
    float3 d = At(uv + float2(-pixel.x, 0));
    float3 e = At(uv);
    float3 f = At(uv + float2(pixel.x, 0));
    float3 h = At(uv + float2(0, pixel.y));

    float3 lowest = min(min(min(b, d), min(f, h)), e);
    float3 highest = max(max(max(b, d), max(f, h)), e);

    // room to sharpen before the brightest or darkest would clip, as a share of the brightest
    float3 room = saturate(min(lowest, 1 - highest) / max(highest, 0.0001));
    float3 weight = sqrt(room) * lerp(-0.125, -0.2, strength.x);
    float3 sharpened = (b * weight + d * weight + f * weight + h * weight + e) / (1 + 4 * weight);

    return float4(saturate(sharpened), 1);
}
