Shader "RAID/Equipment Placement"
{
    Properties { _Color("Preview tint", Color) = (0.15, 1, 0.66, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half facing = abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                half rim = (1.0h - facing) * (1.0h - facing);
                half bands = smoothstep(0.70h, 0.87h, frac(input.positionWS.y * 16.0 - _Time.y * 1.6));
                half pulse = 0.08h * sin(_Time.y * 3.5);
                return half4(_Color.rgb * (0.56h + 0.5h * rim + 0.42h * bands),
                    _Color.a * (0.27h + 0.58h * rim + 0.28h * bands + pulse));
            }
            ENDHLSL
        }
    }
}
