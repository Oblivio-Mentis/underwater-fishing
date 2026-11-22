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
            #include "NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_RimColor,_EmissionColor;
            half _Smoothness,_PatternStrength,_CargoLighting;
            CBUFFER_END
            struct A {float4 pos:POSITION; float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;float3 model:TEXCOORD3;};
            V vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);o.fog=ComputeFogFactor(o.pos.z);o.model=i.pos.xyz;return o;
            }
            half4 frag(V i):SV_Target
            {
                half3 n=normalize(i.normal);
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                Light key=GetMainLight();
                half face=saturate(abs(dot(v,n))), edge=(1.-face)*(1.-face);
                // Broad mottling stays attached to the fish as it banks past the lamp.
                // It changes the scale coat only, leaving the actual body and caudal surfaces intact.
                half field=sin(i.model.z*22+6.5*abs(i.model.x)+1.1*sin(i.model.x*17));
                half scaleCoat=smoothstep(-.30,.82,field)*(.54+.46*saturate(n.y));
                half3 base=_BaseColor.rgb*(1+_PatternStrength*(.27*scaleCoat-.07));
                half3 sky=max(half3(.065,.095,.13),SampleSH(n));
                half3 body=base*(sky+key.color*saturate(dot(n,key.direction))*.68)+_EmissionColor.rgb;
                half3 beam=NereidBeam(i.world)*_NereidTurbidity*half3(.83,.95,1.);
                half keySheen=pow(saturate(dot(n,normalize(key.direction+v))),19);
                body+=_RimColor.rgb*keySheen*_Smoothness*(.055+.10*scaleCoat*_PatternStrength);
                body+=beam*(base*(.30+.70*face)+_RimColor.rgb*(edge*.58+pow(face,24.)*_Smoothness*.16));
                body=lerp(body,clamp(body,base*.63,base*1.40+.028+_EmissionColor.rgb*.16),_CargoLighting);
                return half4(MixFog(body,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
