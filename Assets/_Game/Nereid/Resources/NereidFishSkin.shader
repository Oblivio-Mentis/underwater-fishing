Shader "RAID/Nereid Wet Fauna"
{
    Properties
    {
        _BaseColor("Skin",Color)=(.35,.45,.48,1)
        _RimColor("Wet scales",Color)=(.32,.56,.60,1)
        _Smoothness("Wetness",Range(0,1))=.52
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
            half _Smoothness;
            CBUFFER_END
            struct A {float4 pos:POSITION; float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;};
            V vert(A i)
            {
                V o;o.world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);o.fog=ComputeFogFactor(o.pos.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                half3 n=normalize(i.normal);
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                Light key=GetMainLight();
                half face=saturate(abs(dot(v,n))), edge=(1.-face)*(1.-face);
                half3 base=_BaseColor.rgb;
                half3 sky=max(half3(.065,.095,.13),SampleSH(n));
                half3 body=base*(sky+key.color*saturate(dot(n,key.direction))*.68)+_EmissionColor.rgb;
                half3 beam=NereidBeam(i.world)*_NereidTurbidity*half3(.83,.95,1.);
                body+=beam*(base*(.30+.70*face)+_RimColor.rgb*(edge*.58+pow(face,24.)*_Smoothness*.16));
                return half4(MixFog(body,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
