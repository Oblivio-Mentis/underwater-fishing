Shader "RAID/Pressure Lock Water"
{
    Properties { _Shallow("Reflected inspection light", Color)=(.15,.52,.54,.57) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Shallow;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION; float3 world:TEXCOORD0;float2 local:TEXCOORD1;};
            V vert(A v)
            {
                V o;o.local=v.positionOS.xz;
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.world.y+=.014*sin(o.world.x*5+_Time.y*2.6)+.009*sin(o.world.z*8-_Time.y*3);
                o.positionCS=TransformWorldToHClip(o.world);return o;
            }
            half4 frag(V i):SV_Target
            {
                float t=_Time.y;
                float wave=sin(i.world.x*4.9+t*2.7+.7*sin(i.world.z*4-t))+sin(i.world.z*7.1-t*3.1+.35*sin(i.world.x*9));
                float shine=smoothstep(1.43,1.99,wave);
                float edge=smoothstep(.43,.494,max(abs(i.local.x),abs(i.local.y)));
                float fres=1-abs(normalize(GetCameraPositionWS()-i.world).y);
                half3 color=lerp(half3(.016,.102,.114),_Shallow.rgb,.32+fres*.68)+shine*half3(.09,.16,.13)+edge*.20;
                return half4(color,saturate(_Shallow.a+.21*fres+.16*edge));
            }
            ENDHLSL
        }
    }
}
