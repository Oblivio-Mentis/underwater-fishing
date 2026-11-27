#ifndef RAID_REMORA_STRETCH
#define RAID_REMORA_STRETCH

float3 StretchRemora(float3 world, float4 jaw, float3 direction, float3 pull)
{
    if(jaw.w<=0)return world;
    float3 fromJaw=world-jaw.xyz;
    float along=dot(fromJaw,direction);
    float weight=smoothstep(.05,.97,-along*jaw.w);
    float travel=length(pull);
    float load=saturate(travel*jaw.w);
    float taper=load*weight*(.80-.31*weight);
    float3 draw=pull-direction*(travel*.24*weight);
    return world+draw*weight-(fromJaw-direction*along)*taper;
}

#endif
