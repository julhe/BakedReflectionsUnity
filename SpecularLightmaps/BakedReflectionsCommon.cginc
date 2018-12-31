#ifndef BAKED_REFLECTIONS_COMMON
#define BAKED_REFLECTIONS_COMMON

// There are two storage modes for baked reflections. Sphere and Hemisphere
//Hemisphere                   |  Sphere        
//--------------------------------------------------------------
//+fewer Storage requierements | -More Storage   
//+no interpolation issues     | -interpolation Issues              
//                             |                                  
//-Require Tangents            | +no Requieremetn for Tangents        
//-artefacts on uv seams       | +no artefacts on uv seams

#define BAKED_REFLECTIONS_HEMISPHERE_MODE

// === Octahedral Packing ===
// TODO: I'm not sure if this implementation is 100% optimized.

// Returns �1
float2 signNotZero(float2 v) {
	return float2((v.x >= 0.0) ? +1.0 : -1.0, (v.y >= 0.0) ? +1.0 : -1.0);
}

// Assume normalized input. Output is on [-1, 1] for each component.
float2 float32x3_to_oct(in float3 v) {
	// Project the sphere onto the octahedron, and then onto the xy plane
	float2 p = v.xy * (1.0 / (abs(v.x) + abs(v.y) + abs(v.z)));
	// Reflect the folds of the lower hemisphere over the diagonals
	return (v.z <= 0.0) ? ((1.0 - abs(p.yx)) * signNotZero(p)) : p;
}

float3 oct_to_float32x3(float2 e) {
	float3 v = float3(e.xy, 1.0 - abs(e.x) - abs(e.y));
	if (v.z < 0) v.xy = (1.0 - abs(v.yx)) * signNotZero(v.xy);
	return normalize(v);
}

float2 float32x3_to_hemioct(in float3 v) {
	// Project the hemisphere onto the hemi-octahedron,
	// and then into the xy plane
	float2 p = v.xy * (1.0 / (abs(v.x) + abs(v.y) + v.z));
	// Rotate and scale the center diamond to the unit square
	return float2(p.x + p.y, p.x - p.y);
}
float3 hemioct_to_float32x3(float2 e) {
	// Rotate and scale the unit square back to the center diamond
	float2 temp = float2(e.x + e.y, e.x - e.y) * 0.5;
	float3 v = float3(temp, 1.0 - abs(temp.x) - abs(temp.y));
	return normalize(v);
}

// ==========================

//### actuall functions for shader use###

float2 _BakedReflectionParams; 
//x: slices per axis,
//z: inverse of "slices per axis" for speedups


float3 SampleBakedReflectionEx(sampler2D reflectionAtlas, float3 reflectionVector, float2 uv, float perceptualRoughness) {
	float slicesPerAxis = _BakedReflectionParams.x;
    float slicesPerAxis_rcp = _BakedReflectionParams.y;
	// convert reflection vector to uv of the hemisphere slice
#ifdef BAKED_REFLECTIONS_HEMISPHERE_MODE
	float2 sliceUV = (float32x3_to_hemioct(reflectionVector) * 0.5 + 0.5);
#else
	float2 sliceUV = (float32x3_to_oct(reflectionVector) * 0.5 + 0.5);
#endif
	sliceUV = saturate(sliceUV);
    sliceUV *= slicesPerAxis_rcp;

	float2 uvPixelSpace = uv * slicesPerAxis;
    float2 uvFloor = floor(uvPixelSpace) * slicesPerAxis_rcp;
    float2 uvCeil = ceil(uvPixelSpace) * slicesPerAxis_rcp;

	// perform bilinear interpolation
	// TODO: change to tex2Dlod
	float lodLevel = perceptualRoughness * 6.0;
	float3 smp0 = tex2Dlod(reflectionAtlas, float4(uvFloor + sliceUV, 0, lodLevel));
	float3 smp1 = tex2Dlod(reflectionAtlas, float4(float2(uvCeil.x, uvFloor.y) + sliceUV, 0, lodLevel));

	float2 uvFrac = frac(uvPixelSpace);
	float3 x = lerp(smp0, smp1, uvFrac.x);

	float3 smp2 = tex2Dlod(reflectionAtlas, float4(float2(uvFloor.x, uvCeil.y) + sliceUV, 0, lodLevel));
	float3 smp3 = tex2Dlod(reflectionAtlas, float4(uvCeil + sliceUV, 0, lodLevel));
	float3 y = lerp(smp2, smp3, uvFrac.x);

	return lerp(x, y, uvFrac.y);
}

float3 SampleBakedReflection(sampler2D reflectionAtlas, float3 reflectionVector, float2 uv, float smoothness) {
	return SampleBakedReflectionEx(reflectionAtlas, reflectionVector, uv, (1.0 - smoothness));
}
#endif