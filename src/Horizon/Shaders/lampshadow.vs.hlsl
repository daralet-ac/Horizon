// Lamp shadows (Ui/DepthShading.LampShadows.cs): one of the game's world draws made again from a lamp, into one face of
// its cube shadow map. The place in clip space is the draw's World times the face's view and projection; the place
// relative to the lamp, the draw's World less the lamp, over its reach, for the distance the face stores.
// Compiled by Shaders/build.cmd.
float4 toFace[4] : register(c0);   // rows: World times the face's view and projection
float4 world[4] : register(c4);    // rows: the draw's World
float4 lamp : register(c8);        // the lamp's place in the device's world (xyz), 1 / its reach

struct Input {
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
};

struct Output {
    float4 position : POSITION;
    float2 uv : TEXCOORD0;
    float3 fromLamp : TEXCOORD1;
};

Output main(Input input) {
    float3 p = input.position;
    Output o;
    o.position = p.x * toFace[0] + p.y * toFace[1] + p.z * toFace[2] + toFace[3];
    float3 w = (p.x * world[0] + p.y * world[1] + p.z * world[2] + world[3]).xyz;
    o.fromLamp = (w - lamp.xyz) * lamp.w;
    o.uv = input.uv;
    return o;
}
