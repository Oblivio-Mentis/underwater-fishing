Shader "Raid/Blast Refraction"
{
    Properties { _Wave ("Wave origin / age / strength", Vector) = (.5,.5,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-2" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Wave;
            CBUFFER_END

            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(vertex.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect=_ScaledScreenParams.x / max(1,_ScaledScreenParams.y);
                float2 toward=uv-_Wave.xy;
                toward.x*=aspect;
                float distanceToSource=length(toward);
                float crest=distanceToSource-(.065+1.52*_Wave.z);
                float packet=exp(-12*abs(crest));
                float fade=smoothstep(0,.045,_Wave.z)*(1-smoothstep(.11,1,_Wave.z));
                float2 normal=toward/max(distanceToSource,.001);
                normal.x/=aspect;
                float ripple=sin(112*crest)*packet + .18*sin(39*distanceToSource-25*_Wave.z);
                float2 refracted=clamp(uv + normal*ripple*fade*_Wave.w*.031,.001,.999);
                float border=smoothstep(0,.06,min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
                half alpha=saturate((packet*.76+.15)*border*sqrt(fade)*sqrt(_Wave.w));
                return half4(SampleSceneColor(refracted),alpha);
            }
            ENDHLSL
        }
    }
}
