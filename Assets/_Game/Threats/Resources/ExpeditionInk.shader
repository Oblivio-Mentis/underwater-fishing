Shader "RAID/Expedition/Remora ink"
{
    Properties { _Density("Density", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Density;
            struct Varyings { float4 clip:SV_POSITION; float3 local:TEXCOORD0; float3 ws:TEXCOORD1; float3 normal:TEXCOORD2; };
            Varyings Vert(float3 at:POSITION,float3 normal:NORMAL)
            {
                Varyings o;
                float displacement=sin(at.x*21+at.y*14+_Time.y*2)*sin(at.z*37-_Time.y*1.5)*.022;
                at+=normal*displacement;
                o.local=at;o.ws=TransformObjectToWorld(at);o.clip=TransformWorldToHClip(o.ws);
                o.normal=TransformObjectToWorldNormal(normal);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float core=pow(saturate(dot(normalize(GetWorldSpaceViewDir(i.ws)),normalize(i.normal))),.72);
                float flow=sin(i.local.x*56+i.local.y*32+_Time.y*2+sin(i.local.z*27)*3)*.5+.5;
                float wisps=sin(i.local.y*96-i.local.z*42+_Time.y*3)*.5+.5;
                return half4(lerp(half3(.008,.010,.019),half3(.024,.052,.057),flow*.74),
                    _Density*core*(.66+flow*.18+wisps*.1));
            }
            ENDHLSL
        }
    }
}
