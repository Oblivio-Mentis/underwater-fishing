#ifndef NEREID_WATER_COLUMN_INCLUDED
#define NEREID_WATER_COLUMN_INCLUDED
float4 _NereidWaterTint;
half3 WaterColumnColor(float3 viewDirection)
{
    // Scattered overhead light still gives a sense of up when distant objects disappear.
    float height=smoothstep(-.65,.72,viewDirection.y);
    float halo=pow(saturate(dot(viewDirection,normalize(float3(-.47,.86,-.20)))),9);
    return _NereidWaterTint.rgb*(lerp(.48,1.36,height)+halo*.38);
}
#endif
