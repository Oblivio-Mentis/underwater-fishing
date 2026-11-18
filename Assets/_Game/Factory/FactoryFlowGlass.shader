Shader "RAID011/Factory Flow Glass"
{
    Properties
    {
        _BaseColor("Glass tint",Color)=(.15,.55,.63,.16)
        _FlowColor("Flow signal",Color)=(.15,.8,.9,1)
        _FlowActive("Batch active",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _FlowColor;
            float _FlowActive;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
            Varyings vert(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS);half3 view=normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim=pow(1-abs(dot(normal,view)),3);
                float moving=frac(input.uv.y*.75-_Time.y*(.25+_FlowActive*.6));
                half band=(1-smoothstep(.04,.10,abs(moving-.5)))*(.12+_FlowActive*.5);
                half3 color=_BaseColor.rgb*(.65+rim*.5)+_FlowColor.rgb*(rim*.35+band);
                return half4(MixFog(color,input.fog),saturate(_BaseColor.a+rim*.3+band*.15));
            }
            ENDHLSL
        }
    }
}
