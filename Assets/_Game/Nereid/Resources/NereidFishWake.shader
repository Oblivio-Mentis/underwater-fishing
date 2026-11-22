Shader "Nereid/Fish Wake"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float distanceToEye : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.distanceToEye = distance(world,_WorldSpaceCameraPos);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0,.55h,1 - abs(input.uv.y * 2 - 1));
                half filaments = pow(saturate(.5h + .5h * sin(input.uv.x * 61 - input.uv.y * 9 + _Time.y * 9)),3);
                half distanceFade = 1 - smoothstep(7,23,input.distanceToEye);
                return half4(input.color.rgb,input.color.a * edge * (.38h + filaments * .62h) * distanceFade);
            }
            ENDHLSL
        }
    }
}
