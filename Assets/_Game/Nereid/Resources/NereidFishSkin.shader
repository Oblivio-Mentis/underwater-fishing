Shader "RAID/Nereid Wet Fauna"
{
    Properties
    {
        _BaseColor("Skin",Color)=(.35,.45,.48,1)
        _RimColor("Wet scales",Color)=(.32,.56,.60,1)
        _Smoothness("Wetness",Range(0,1))=.52
        _PatternStrength("Scale coat",Range(0,1))=0
        _CargoLighting("Controlled inspection light",Range(0,1))=0
        [HDR] _EmissionColor("Photophores",Color)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            #include "NereidDiveLamp.hlsl"
            #include "NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_RimColor,_EmissionColor;
            half _Smoothness,_PatternStrength,_CargoLighting;
            CBUFFER_END
            float3 _NereidReefClarity;
            struct A {float4 pos:POSITION; float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;float3 model:TEXCOORD3;half3 modelNormal:TEXCOORD4;};
            V vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);o.fog=ComputeFogFactor(o.pos.z);o.model=i.pos.xyz;
                o.modelNormal=i.normal;return o;
            }
            half4 frag(V i):SV_Target
            {
                half3 n=FacetNormalWS(i.world,i.normal);
                half3 nOS=normalize(i.modelNormal);
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                Light key=GetMainLight();
                half face=saturate(abs(dot(v,n))), edge=(1.-face)*(1.-face);
                // Broad flank markings follow the same species through every stage of the catch.
                half flank=smoothstep(-.18,.18,cos(i.model.z*6.5+abs(i.model.x)*1.8));
                half3 base=_BaseColor.rgb*(1.+_PatternStrength*(.18*flank-.06));
                // Countershading from the object normal: a cool dorsal surface, a warmer, paler underside.
                half belly=smoothstep(-.15,.75,-nOS.y);
                base*=lerp(half3(.88,.96,1.07),half3(1.14,1.05,.94)*(1.+.16*belly),belly);
                // Wrapped diffuse keeps the far side of the body readable in the dim water column.
                half3 sky=max(half3(.065,.095,.13),SampleSH(n));
                half wrapped=saturate((dot(n,key.direction)+.55)/1.55);
                half3 body=base*(sky+key.color*wrapped*wrapped*.74)+_EmissionColor.rgb;
                half3 beam=NereidBeam(i.world)*_NereidTurbidity*half3(.83,.95,1.);
                // Soft wet specular: a broad sheen plus a tighter glint over the scale coat.
                half nh=saturate(dot(n,normalize(key.direction+v)));
                half keySheen=pow(nh,9)*.045+pow(nh,34)*(.045+.035*flank*_PatternStrength);
                body+=_RimColor.rgb*key.color*keySheen*_Smoothness*1.6;
                // Thin membranes and fin edges pick up a light rim where the surface turns away.
                half finRim=pow(1.-face,3.)*saturate(.6+.4*dot(n,half3(0,1,0)));
                body+=(_RimColor.rgb*.55+sky*.8)*finRim*.34;
                body+=beam*(base*(.30+.70*face)+_RimColor.rgb*(edge*.58+finRim*.3+pow(face,24.)*_Smoothness*.16));
                body=lerp(body,clamp(body,base*.63,base*1.40+.028+_EmissionColor.rgb*.16),_CargoLighting);
                // Fall into the water column the same way the reef does, so a far catch sits in the same haze.
                float3 delta=i.world-GetCameraPositionWS();
                float transmission=exp2(-dot(delta,delta)*_NereidReefClarity.x);
                half3 submerged=lerp(WaterColumnColor(-v),body,transmission);
                return half4(lerp(MixFog(body,i.fog),submerged,_NereidTurbidity),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
