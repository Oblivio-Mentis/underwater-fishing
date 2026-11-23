Shader "RAID/Nereid landing sand"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float3 pos:POSITION; float3 normal:NORMAL; half4 color:COLOR; };
            struct Vary { float4 pos:SV_POSITION; half4 color:COLOR; };
            Vary Vert(Input input)
            {
                Vary o;
                o.pos=TransformObjectToHClip(input.pos);
                float3 normal=TransformObjectToWorldNormal(input.normal);
                half light=.76h+.22h*saturate(dot(normal,normalize(float3(-.34,.86,.37))));
                o.color=half4(input.color.rgb*light,input.color.a);
                return o;
            }
            half4 Frag(Vary input):SV_Target {return input.color;}
            ENDHLSL
        }
    }
}
