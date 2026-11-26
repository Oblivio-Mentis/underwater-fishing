Shader "Raid/Fish Proximity Outline"
{
    Properties { _Opacity ("Opacity", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half _Opacity;
            CBUFFER_END

            float4 Vert(float3 position : POSITION,float3 normal : NORMAL) : SV_POSITION
            {
                return TransformObjectToHClip(position+normal*.046);
            }

            half4 Frag() : SV_Target
            {
                return half4(.96,.99,1,_Opacity);
            }
            ENDHLSL
        }
    }
}
