#ifndef RAID_REMORA_STRETCH
#define RAID_REMORA_STRETCH

float3 StretchRemora(float3 world, float4 jaw, float3 direction, float3 pull)
{
    if(jaw.w<=0)return world;
    float aft=dot(jaw.xyz-world,direction)*jaw.w;
    return world+pull*smoothstep(.06,.98,aft);
}

#endif
