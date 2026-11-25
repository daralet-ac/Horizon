// Far trees (Ui/DrawDistance.FarTrees.cs): a scenery model's own geometry, one copy for each tree (hardware instancing).
// The model's vertices are in its own axes (x, y on the ground, z up); each copy's place, height, size and turn come from
// the second stream: (east, height, north, scale) and (cos, sin) of its turn, as the billboards turn them. The device's
// world is (x east, height, y north) from your landblock's south-west corner. The game's linear fog, which shaders don't
// get from the device, is worked out here from the view distance. Compiled by Shaders/build.cmd.
float4 viewProjection[4] : register(c0);  // rows
float4 fog : register(c4);                // start, end, on (1 or 0)

struct Input {
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
    float4 place : TEXCOORD1;
    float4 turn : TEXCOORD2;
};

struct Output {
    float4 position : POSITION;
    float2 uv : TEXCOORD0;
    float fogged : TEXCOORD1;
};

Output main(Input input) {
    float3 m = input.position * input.place.w;
    float c = input.turn.x;
    float s = input.turn.y;
    float3 world = float3(input.place.x + m.x * c - m.y * s, input.place.y + m.z, input.place.z + m.x * s + m.y * c);

    Output o;
    o.position = world.x * viewProjection[0] + world.y * viewProjection[1] + world.z * viewProjection[2]
        + viewProjection[3];
    o.uv = input.uv;
    o.fogged = fog.z > 0.5 ? saturate((fog.y - o.position.w) / max(fog.y - fog.x, 1)) : 1;
    return o;
}
