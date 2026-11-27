// Local inspection beam shared with the real URP spot illuminating the rest of the vessel.
#ifndef NEREID_DIVE_LAMP_INCLUDED
#define NEREID_DIVE_LAMP_INCLUDED
float _NereidTurbidity;
float4 _NereidLampForward; // World direction and emitted candela (zero when switched off).
float4 _NereidLampOriginRange;
float2 _NereidLampCone;
float _RaidLampsActive;
float4 _RaidLampOrigins[4],_RaidLampDirections[4];
float RaidSearchBeam(float3 positionWS)
{
    if(_RaidLampsActive<.5)return 0;
    float light=0;
    [unroll] for(int i=0;i<4;i++)
    {
        float3 ray=positionWS-_RaidLampOrigins[i].xyz;
        float d2=max(.001,dot(ray,ray));
        float window=saturate(1-d2/max(.001,_RaidLampOrigins[i].w*_RaidLampOrigins[i].w));
        float spot=smoothstep(.90,.973,dot(ray*rsqrt(d2),_RaidLampDirections[i].xyz));
        light+=spot*window*_RaidLampDirections[i].w/(d2+115)*.44;
    }
    return light;
}
float NereidBeam(float3 positionWS)
{
    float search=RaidSearchBeam(positionWS);
    if(_NereidLampForward.w<=0)return search;
    float3 ray=positionWS-_NereidLampOriginRange.xyz;
    float distance2=max(dot(ray,ray),.001);
    float axis=dot(ray*rsqrt(distance2),_NereidLampForward.xyz);
    float spot=smoothstep(_NereidLampCone.x,_NereidLampCone.y,axis);
    float q=distance2/max(.001,_NereidLampOriginRange.w*_NereidLampOriginRange.w);
    float window=saturate(1.-q*q);
    // Roll off near-field glare while keeping fish markings and rock contours visible at working distance.
    return search+spot*window*window*_NereidLampForward.w/(distance2+38.);
}
#endif
