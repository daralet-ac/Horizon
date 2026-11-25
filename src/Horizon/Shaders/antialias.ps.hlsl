// Anti-aliasing (Ui/DepthShading.Image.cs): FXAA, after Timothy Lottes's FXAA 3.11 (its quality preset 12 search),
// over a copy of the 3D world before the interface is drawn, so only the world's edges are smoothed. Each pixel's
// contrast with its neighbours says whether it's on an edge; along the edge the ends are searched for, and the pixel is
// sampled across the edge by how far it sits from them, plus a little for edges thinner than a pixel. Compiled by
// Shaders/build.cmd into Resources/shaders/antialias.ps.cso.
sampler frameColour : register(s1);

float4 screen : register(c13);     // width, height, 1/width, 1/height

float Luma(float3 rgb) {
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

float3 At(float2 uv) {
    return tex2Dlod(frameColour, float4(uv, 0, 0)).rgb;
}

float LumaAt(float2 uv) {
    return Luma(At(uv));
}

static const float Steps[8] = { 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0, 8.0 };

float4 main(float2 uv : TEXCOORD0) : COLOR {
    float2 pixel = screen.zw;
    float3 colour = At(uv);
    float lumaM = Luma(colour);
    float lumaN = LumaAt(uv + float2(0, -pixel.y));
    float lumaS = LumaAt(uv + float2(0, pixel.y));
    float lumaE = LumaAt(uv + float2(pixel.x, 0));
    float lumaW = LumaAt(uv + float2(-pixel.x, 0));
    float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaE, lumaW)));
    float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaE, lumaW)));
    float range = lumaMax - lumaMin;

    // flat areas, and faint edges (the game's textures' own detail), are left exactly as they were
    if (range < max(0.0312, lumaMax * 0.125)) {
        return float4(colour, 1);
    }

    float lumaNW = LumaAt(uv + float2(-pixel.x, -pixel.y));
    float lumaNE = LumaAt(uv + float2(pixel.x, -pixel.y));
    float lumaSW = LumaAt(uv + float2(-pixel.x, pixel.y));
    float lumaSE = LumaAt(uv + float2(pixel.x, pixel.y));

    float edgeHorizontal = abs(-2 * lumaW + lumaNW + lumaSW) + 2 * abs(-2 * lumaM + lumaN + lumaS)
        + abs(-2 * lumaE + lumaNE + lumaSE);
    float edgeVertical = abs(-2 * lumaN + lumaNW + lumaNE) + 2 * abs(-2 * lumaM + lumaW + lumaE)
        + abs(-2 * lumaS + lumaSW + lumaSE);
    bool horizontal = edgeHorizontal >= edgeVertical;

    float luma1 = horizontal ? lumaN : lumaW;
    float luma2 = horizontal ? lumaS : lumaE;
    float gradient1 = luma1 - lumaM;
    float gradient2 = luma2 - lumaM;
    bool steeper1 = abs(gradient1) >= abs(gradient2);
    float gradientScaled = 0.25 * max(abs(gradient1), abs(gradient2));
    float stepLength = horizontal ? pixel.y : pixel.x;
    float lumaLocalAverage;

    if (steeper1) {
        stepLength = -stepLength;
        lumaLocalAverage = 0.5 * (luma1 + lumaM);
    }
    else {
        lumaLocalAverage = 0.5 * (luma2 + lumaM);
    }

    float2 current = uv;

    if (horizontal) {
        current.y += stepLength * 0.5;
    }
    else {
        current.x += stepLength * 0.5;
    }

    // along the edge both ways until its ends
    float2 offset = horizontal ? float2(pixel.x, 0) : float2(0, pixel.y);
    float2 uv1 = current - offset;
    float2 uv2 = current + offset;
    float lumaEnd1 = LumaAt(uv1) - lumaLocalAverage;
    float lumaEnd2 = LumaAt(uv2) - lumaLocalAverage;
    bool reached1 = abs(lumaEnd1) >= gradientScaled;
    bool reached2 = abs(lumaEnd2) >= gradientScaled;

    for (int i = 1; i < 8; i++) {
        if (reached1 && reached2) {
            break;
        }

        if (!reached1) {
            uv1 -= offset * Steps[i];
            lumaEnd1 = LumaAt(uv1) - lumaLocalAverage;
            reached1 = abs(lumaEnd1) >= gradientScaled;
        }

        if (!reached2) {
            uv2 += offset * Steps[i];
            lumaEnd2 = LumaAt(uv2) - lumaLocalAverage;
            reached2 = abs(lumaEnd2) >= gradientScaled;
        }
    }

    float distance1 = horizontal ? (uv.x - uv1.x) : (uv.y - uv1.y);
    float distance2 = horizontal ? (uv2.x - uv.x) : (uv2.y - uv.y);
    bool nearer1 = distance1 < distance2;
    float nearest = min(distance1, distance2);
    float edgeLength = distance1 + distance2;
    float pixelOffset = -nearest / edgeLength + 0.5;
    bool centreSmaller = lumaM < lumaLocalAverage;
    bool correctVariation = ((nearer1 ? lumaEnd1 : lumaEnd2) < 0) != centreSmaller;
    float finalOffset = correctVariation ? pixelOffset : 0;

    // thin features, a pixel or less across
    float lumaAverage = (2 * (lumaN + lumaS + lumaE + lumaW) + lumaNW + lumaNE + lumaSW + lumaSE) / 12;
    float subPixel1 = saturate(abs(lumaAverage - lumaM) / range);
    float subPixel2 = (-2 * subPixel1 + 3) * subPixel1 * subPixel1;
    float subPixelOffset = subPixel2 * subPixel2 * 0.75;
    finalOffset = max(finalOffset, subPixelOffset);

    float2 finalUv = uv;

    if (horizontal) {
        finalUv.y += finalOffset * stepLength;
    }
    else {
        finalUv.x += finalOffset * stepLength;
    }

    return float4(At(finalUv), 1);
}
