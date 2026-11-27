Shader "RAID/Expedition/Pressure silt"
{
    Properties { _Opacity("Cloud opacity",Range(0,1)) = .72 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Opacity;
            CBUFFER_END

            struct Varyings
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                float2 uv:TEXCOORD1;
                half4 tint:COLOR;
            };

            Varyings Vert(float3 position:POSITION,float2 uv:TEXCOORD0, half4 tint:COLOR)
            {
                Varyings o;
                o.world=TransformObjectToWorld(position);
                o.clip=TransformWorldToHClip(o.world);
                o.uv=uv*2-1;
                o.tint=tint;
                return o;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.clip)),_ZBufferParams);
                float viewDepth=-TransformWorldToView(i.world).z;
                half soft=saturate((scene-viewDepth)*5);
                half grain=frac(sin(dot(floor(i.uv*9),float2(129.53,75.13)))*21603.82);
                half radius=length(i.uv*.94);
                half core=smoothstep(1,.22,radius+grain*.12);
                half mote=smoothstep(.45,.89,grain)*smoothstep(.79,.27,radius);
                half visibility=smoothstep(.16,.57,viewDepth)*(1-smoothstep(16,35,viewDepth));
                half beam=saturate(NereidBeam(i.world)+RaidSearchBeam(i.world));
                half3 color=i.tint.rgb*(.36h+.46h*beam+.16h*mote);
                return half4(color,i.tint.a*(core*.56h+mote*.40h)*soft*visibility*_Opacity);
            }
            ENDHLSL
        }
    }
}
