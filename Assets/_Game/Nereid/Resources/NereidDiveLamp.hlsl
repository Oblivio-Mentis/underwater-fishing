// Local inspection beam shared with the real URP spot illuminating the rest of the vessel.
#ifndef NEREID_DIVE_LAMP_INCLUDED
#define NEREID_DIVE_LAMP_INCLUDED
float _NereidTurbidity;
float3 _NereidLampForward;
float4 _NereidLampOriginRange;
float NereidBeam(float3 positionWS)
{
    float3 ray=positionWS-_NereidLampOriginRange.xyz;
    float distance2=max(dot(ray,ray),.001);
    float axis=dot(ray*rsqrt(distance2),_NereidLampForward);
    float spot=smoothstep(.8290,.9511,axis); // 68 degree penumbra, 36 degree core.
    float q=distance2/max(.001,_NereidLampOriginRange.w*_NereidLampOriginRange.w);
    float window=saturate(1.-q*q);
    return spot*window*window*min(6.,76./(distance2+4.));
}
#endif
