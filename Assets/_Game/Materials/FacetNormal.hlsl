#ifndef RAID_FACET_NORMAL_INCLUDED
#define RAID_FACET_NORMAL_INCLUDED

// Match the authored winding on two-sided meshes, including inside surfaces.
half3 FacetNormalWS(float3 positionWS, half3 reference)
{
    float3 face = SafeNormalize(cross(ddy(positionWS), ddx(positionWS)));
    return dot(face, reference) < 0 ? -face : face;
}
#endif
