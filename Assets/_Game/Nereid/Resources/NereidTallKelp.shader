Shader "RAID/Tall laminaria"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NereidDiveLamp.hlsl"
            #include "NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _BendRadius, _BendMetres;
                float4 _KelpSwimmers[5];
            CBUFFER_END
            float3 _NereidReefClarity;
            struct Input {float3 pos:POSITION; float3 normal:NORMAL; half4 color:COLOR;};
            struct Vary {float4 clip:SV_POSITION; float3 pos:TEXCOORD0; half3 color:COLOR;};
            Vary vert(Input v)
            {
                float3 p=TransformObjectToWorld(v.pos);
                float h=v.color.a;
                float phase=dot(p.xz,float2(.35,.24))+p.y*.54;
                float t=_Time.y*.79;
                p.xz+=float2(.22*sin(t+phase),.11*sin(t*.74-phase))*h;
                float2 push=0;
                [unroll] for(int i=0;i<5;i++)
                {
                    float3 offset=p-_KelpSwimmers[i].xyz;
                    // A diver-length capsule opens the near side of the bank, tip by tip.
                    offset.y-=clamp(offset.y,-.45,.6);
                    float bend=1-smoothstep(.15,_BendRadius,length(offset));
                    float2 normal=offset.xz+float2(.0007,.0003);
                    push+=normal*rsqrt(max(dot(normal,normal),.000001))*bend*_KelpSwimmers[i].w;
                }
                p.xz+=push*_BendMetres*smoothstep(0,.43,h)/max(1.,length(push));
                Vary o; o.clip=TransformWorldToHClip(p); o.pos=p;
                o.color=v.color.rgb*(.72+.34*abs(dot(v.normal,normalize(float3(.45,.78,-.4)))));
                return o;
            }
            half4 frag(Vary i):SV_Target
            {
                float3 ray=i.pos-GetCameraPositionWS();
                float d=max(.001,length(ray));
                float3 close=i.color*(.81+.47*NereidBeam(i.pos));
                float transmission=exp2(-d*d*lerp(.00136,_NereidReefClarity.x,_NereidTurbidity));
                return half4(lerp(WaterColumnColor(ray/d),close,transmission),1);
            }
            ENDHLSL
        }
    }
}
