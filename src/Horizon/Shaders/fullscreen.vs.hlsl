// A full-screen triangle strip for Ui/DepthShading.cs: positions are already in clip space; Direct3D 9's half-pixel
// offset is taken off here (c0: one pixel in clip space, x and y), so a texel lands on its pixel.
// Compiled by Shaders/build.cmd into Resources/shaders/fullscreen.vs.cso.
float4 halfPixel : register(c0);

struct Output {
    float4 position : POSITION;
    float2 uv : TEXCOORD0;
};

Output main(float4 position : POSITION, float2 uv : TEXCOORD0) {
    Output o;
    o.position = float4(position.x - halfPixel.x, position.y + halfPixel.y, 0, 1);
    o.uv = uv;
    return o;
}
