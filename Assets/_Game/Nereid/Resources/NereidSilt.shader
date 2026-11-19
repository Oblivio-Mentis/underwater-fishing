Shader "RAID/Nereid Silt"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input{float3 pos:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Vary{float4 pos:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            Vary vert(Input v){Vary o;o.pos=TransformObjectToHClip(v.pos);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(Vary i):SV_Target
            {
                float2 p=i.uv*2-1;float edge=length(p)+.085*sin(9*p.x+4*p.y)*sin(7*p.y-2*p.x);
                return half4(i.color.rgb,i.color.a*(1-smoothstep(.18,1,edge))*.6);
            }
            ENDHLSL
        }
    }
}