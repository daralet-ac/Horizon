// Sun shadows (Ui/DepthShading.Shadows.cs): one of the game's world draws made again from the sun, into the shadow map.
// It's drawn with the game's own vertex declaration, so position and the first texture coordinate come from wherever
// the game put them. The matrix is the draw's World times the sun's view and its orthographic projection, by rows.
// Compiled by Shaders/build.cmd.
float4 toShadow[4] : register(c0);

struct Input {
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
};

struct Output {
    float4 position : POSITION;
    float2 uv : TEXCOORD0;
    float depth : TEXCOORD1;
};

Output main(Input input) {
    Output o;
    float3 p = input.position;
    o.position = p.x * toShadow[0] + p.y * toShadow[1] + p.z * toShadow[2] + toShadow[3];
    o.uv = input.uv;
    o.depth = o.position.z;
    return o;
}
