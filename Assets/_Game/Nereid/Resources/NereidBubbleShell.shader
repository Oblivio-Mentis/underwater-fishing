Shader "RAID/Nereid Bubble Shell"
{
    Properties
    {
        _BaseColor("Shell tint and opacity",Color)=(.62,.85,.89,.57)
        [Toggle] _ParticleTint("Use particle tint",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NereidDiveLamp.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _ParticleTint;
            CBUFFER_END
            struct Input { float3 position:POSITION; float3 normal:NORMAL; half4 color:COLOR; };
            struct Output { float4 clip:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:TEXCOORD2; half fog:TEXCOORD3; };

            Output Vertex(Input i)
            {
                Output o;
                o.world=TransformObjectToWorld(i.position);
                o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);
                o.color=lerp(half4(1,1,1,1),i.color,_ParticleTint);
                o.fog=ComputeFogFactor(o.clip.z);
                return o;
            }

            half4 Fragment(Output i):SV_Target
            {
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                half3 n=normalize(i.normal);
                half edge=pow(1-saturate(dot(n,v)),1.65);
                half3 ceiling=half3(-.27,.91,-.31);
                half reflection=pow(saturate(dot(reflect(-ceiling,n),v)),19);
                half beam=saturate(NereidBeam(i.world)*.5)*_NereidTurbidity;
                half dryGlow=.56+.52*reflection+.46*edge;
                half3 tint=_BaseColor.rgb*i.color.rgb*dryGlow;
                half fog=ComputeFogIntensity(i.fog);
                half alpha=_BaseColor.a*i.color.a*saturate(.04+edge*.66+reflection*.85+beam*(reflection+edge)*.39);
                return half4(MixFog(tint,i.fog),alpha*fog);
            }
            ENDHLSL
        }
    }
}
