Shader "RAID/Defense/Suspended shoal sediment"
{
    Properties { [HideInInspector] _Current("Opacity, drift seed, speed, wing",Vector)=(0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-26" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidDiveLamp.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Current;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                float2 flow : TEXCOORD1;
            };

            Varyings Vert(float3 position : POSITION, float2 flow : TEXCOORD0)
            {
                Varyings o;
                o.world=TransformObjectToWorld(position);
                o.clip=TransformWorldToHClip(o.world);
                o.flow=flow;
                return o;
            }

            float DriftSpeck(float2 p)
            {
                float2 cell=floor(p);
                float salt=frac(sin(dot(cell,float2(72.17,218.65)))*29716.44);
                float2 dust=frac(p)-float2(.27+salt*.46,.26+frac(salt*17)*.48);
                return saturate(1-length(dust*float2(1.3,2.7))*6)*smoothstep(.48,.93,salt);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float time=_Time.y*(.21+_Current.z);
                float along=i.flow.x;
                float across=i.flow.y;
                float bend=sin(along*7+i.world.y*.18+_Current.y-time*.37)*.18;
                float edge=abs(across-bend);
                half spread=saturate(1-edge);
                float2 p=float2(along*24-time*2.1+_Current.y,across*8+i.world.y*.21);
                float currents=.49+.26*sin(p.x*.91+p.y*2.6+sin(p.x*.32))+.18*cos(p.x*1.9-p.y+sin(p.y*2.2));
                float filaments=.57+.43*sin(p.x*.8+4*sin(p.y*.67)-p.y*5.9);
                half density=smoothstep(.25,.83,currents)*lerp(.69,1.17,filaments);
                half fade=smoothstep(.015,.23,along)*(1-smoothstep(.49,1,along))*spread*spread;
                float surface=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.clip)),_ZBufferParams);
                float depth=-TransformWorldToView(i.world).z;
                half soft=saturate((surface-depth)*.25);
                half range=smoothstep(5,20,depth)*(1-smoothstep(143,195,depth));
                half beam=saturate(RaidSearchBeam(i.world)*2.5);
                half3 water=WaterColumnColor(SafeNormalize(i.world-GetCameraPositionWS()),_NereidReefClarity.x);
                half3 sediment=water*1.18+lerp(half3(.032,.082,.110),half3(.12,.28,.31),beam);
                half alpha=density*fade*soft*range*_Current.x*lerp(.72,1.6,beam)*(1+_Current.w*.19);
                float2 speck=float2(along*70-time*7+_Current.y,across*13+sin(p.x*.31)*.43);
                half dust=DriftSpeck(speck)*fade*soft*range*_Current.x*lerp(.52,1.35,beam);
                return half4(sediment*alpha+half3(.25,.54,.60)*dust,saturate(alpha+dust*.42));
            }
            ENDHLSL
        }
    }
}
