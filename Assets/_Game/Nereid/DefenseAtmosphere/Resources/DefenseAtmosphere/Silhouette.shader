Shader "RAID/Defense/Swarm Silhouette"
{
    Properties
    {
        _Tint ("Body colour multiplier", Color) = (1,1,1,1)
        [HideInInspector] _EyePass ("Eye surface", Float) = 0
        _EyeGlow ("Eye brightness", Range(0,3)) = 1.35
        _Fade ("Swarm visibility", Range(0,1)) = 1
        _Range ("Dissolve distance", Float) = 47
        _Billow ("Swim speed and flexibility", Vector) = (5,.22,0,0)
        [HideInInspector] _CloudBatch ("Batched bank", Float) = 0
        [HideInInspector] _Handoff ("Leave for live swimmer", Range(0,1)) = 0
        [HideInInspector] _Motion ("Stroke, effort, species, pigment", Vector) = (0,1,0,1)
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolightprobe nolightmap nolodfade
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _EyeGlow, _Fade, _EyePass;
            float _Range;
            float4 _Billow, _Motion;
            float _CloudBatch, _Handoff;
            float4 _CloudPoints[110], _CloudHeadings[110], _CloudMotion[110];
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes
            {
                float3 position : POSITION;
                float3 normal : NORMAL;
                half4 pigment : COLOR;
                float4 bank : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half3 pigment : TEXCOORD2;
                half beat : TEXCOORD3;
                float2 halo : TEXCOORD4;
                half visibility : TEXCOORD5;
            };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 root = TransformObjectToWorld(float3(0,0,0));
                int index = (int)v.bank.x;
                float4 motion=_Motion;
                if(_CloudBatch>.5)
                {
                    root=_CloudPoints[index].xyz;
                    motion=_CloudMotion[index];
                }
                float beat=motion.x, effort=motion.y;
                float shell=saturate(.54-v.position.z);
                float fin=saturate(abs(v.position.x)-.18);
                float tail=sin(beat+v.position.z*4.7)*shell*shell;
                v.position.x+=tail*_Billow.y*effort;
                v.position.y+=cos(beat*.82+abs(v.position.x)*2.9)*fin*_Billow.y*.43*effort;
                if(abs(motion.z-3)<.25)
                {
                    // A squid takes water into its mantle and pulses. Keep this
                    // silhouette distinct from the fish driving with their tails.
                    float contract=.057*sin(beat)*saturate(1-shell)*effort;
                    v.position.xy*=1+contract;
                    v.position.z-=contract*.24;
                }
                o.world = TransformObjectToWorld(v.position);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.halo=v.bank.yz;
                o.visibility=1;
                if(_CloudBatch>.5)
                {
                    float4 heading=_CloudHeadings[index];
                    float3 forward=heading.xyz;
                    float3 side=SafeNormalize(cross(float3(0,1,0),forward));
                    float3 up=cross(forward,side);
                    float twist=sin(heading.w), upright=cos(heading.w);
                    float3 bank=side*upright+up*twist;
                    up=up*upright-side*twist;
                    o.world=root+(v.position.x*bank+v.position.y*up+v.position.z*forward)*_CloudPoints[index].w;
                    o.normal=v.normal.x*bank+v.normal.y*up+v.normal.z*forward;
                    if(v.bank.w>.5)
                    {
                        float3 view=SafeNormalize(GetCameraPositionWS()-o.world);
                        o.visibility=smoothstep(-.24,.45,dot(view,forward))*smoothstep(-.30,.55,dot(view,o.normal));
                        float travel=distance(o.world,GetCameraPositionWS());
                        float size=lerp(.18,.74,smoothstep(35,145,travel));
                        float3 right=UNITY_MATRIX_I_V._m00_m10_m20;
                        float3 vertical=UNITY_MATRIX_I_V._m01_m11_m21;
                        o.world+=(right*v.bank.y+vertical*v.bank.z)*size+view*.045;
                    }
                }
                o.clip = TransformWorldToHClip(o.world);
                o.pigment = v.pigment.rgb;
                o.beat = motion.w*(.92+.08*sin(beat*.41));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 delta = i.world - GetCameraPositionWS();
                float d2 = dot(delta,delta);
                float distanceToEye = sqrt(d2);
                half3 view = -SafeNormalize(delta);
                half luminous = _EyePass;
                half limit = 1-smoothstep(_Range*.76,_Range,distanceToEye);
                if(luminous>1.5)
                {
                    float haloRadius=dot(i.halo,i.halo);
                    half falloff=exp2(-7*haloRadius)*.22 + exp2(-30*haloRadius)*.64;
                    half halo=saturate(1-haloRadius)*falloff*limit*_Fade*i.visibility;
                    halo*=exp2(-d2*.00009);
                    return half4(i.pigment*halo*_EyeGlow*i.beat,0);
                }
                // Depth-tested opaque fragments keep the finest brackets behind the hull and window frames.
                half coverage = limit * smoothstep(2.5,5.6,distanceToEye) * _Fade;
                float dither = frac(52.9829189 * frac(dot(floor(i.clip.xy), float2(.06711056,.00583715))));
                clip(coverage - dither - .002);
                if(_Handoff>0)clip(dither-_Handoff);

                half3 n = FacetNormalWS(i.world,i.normal);
                half edge = saturate(1 - abs(dot(n,view)));
                half overhead = saturate(dot(n, half3(-.32,.90,-.28))*.55+.45);
                half3 pigment = i.pigment * _Tint.rgb;
                half search=saturate(RaidSearchBeam(i.world)*3);
                half beam=saturate(NereidBeam(i.world)*2);
                half3 color=pigment*(.10+.21*overhead)+pigment*pow(edge,2.3)*.15;
                color+=pigment*beam*(.29+.51*overhead);
                half3 column=WaterColumnColor(-view,_NereidReefClarity.x);
                half extinction=exp2(-d2*max(.00029,_NereidReefClarity.x*lerp(.50,.22,_CloudBatch)));
                half3 shadow=lerp(column*.53,color,max(extinction,search*.89));
                half eyeFalloff=exp2(-d2*lerp(.00013,.00009,_CloudBatch));
                half3 lamp=i.pigment*_EyeGlow*eyeFalloff*i.beat;
                return half4(lerp(shadow,lamp,luminous),1);
            }
            ENDHLSL
        }
    }
}
