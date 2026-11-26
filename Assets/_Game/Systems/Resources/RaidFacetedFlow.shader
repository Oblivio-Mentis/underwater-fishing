Shader "Raid/Faceted Flow"
{
    Properties
    {
        _Bands ("Moving bands", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+11" "RenderType"="Transparent" }
        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half _Bands;
            CBUFFER_END

            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Surface { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Surface Vert(Input input)
            {
                Surface output;
                output.position=TransformObjectToHClip(input.position);
                output.uv=input.uv;
                output.color=input.color;
                return output;
            }

            half4 Frag(Surface input) : SV_Target
            {
                half rib=smoothstep(.22,.73,frac(input.uv.x*3.7-_Time.y*2.5+input.uv.y*.28));
                half edge=pow(abs(input.uv.y*2-1),5);
                return half4(input.color.rgb*lerp(1,.77+.29*rib+.19*edge,_Bands),input.color.a*lerp(1,.88+.12*rib,_Bands));
            }
            ENDHLSL
        }
    }
}
