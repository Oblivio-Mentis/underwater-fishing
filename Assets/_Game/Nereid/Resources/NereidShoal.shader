Shader "RAID/Nereid Decorative Shoal"
{
    Properties
    {
        _Back("Dorsal colour",Color)=(.125,.255,.269,1)
        _Belly("Silver flank",Color)=(.49,.63,.57,1)
        _Fin("Fin colour",Color)=(.19,.36,.36,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Back,_Belly,_Fin;
            CBUFFER_END

            struct Input
            {
                float3 position:POSITION;
                float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half3 pigment:TEXCOORD2;
                half fog:TEXCOORD3;
            };

            Output Vertex(Input i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Output o;
                float3 worldRoot=TransformObjectToWorld(float3(0,0,0));
                float phase=dot(worldRoot,float3(.61,.91,-.30));
                float tail=saturate((.24-i.position.z)*1.26);
                i.position.x+=sin(_Time.y*8.8+i.position.z*9+phase)*tail*tail*.13;
                o.world=TransformObjectToWorld(i.position);
                o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);
                o.pigment=i.uv.x<.33?_Back.rgb:i.uv.x<.66?_Belly.rgb:_Fin.rgb;
                o.fog=ComputeFogFactor(o.clip.z);
                return o;
            }

            half4 Fragment(Output i):SV_Target
            {
                half3 n=normalize(i.normal);
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                half3 overhead=half3(-.31,.91,-.26);
                half sky=saturate(dot(n,overhead));
                half sheen=pow(saturate(dot(n,normalize(overhead+v))),22);
                half edge=1-saturate(abs(dot(n,v)));
                half3 body=i.pigment*(.50+sky*.53);
                body+=half3(.25,.36,.36)*(sheen*.46+edge*.18);
                half crossing=saturate(dot(n,normalize(overhead-v)));
                body+=half3(.12,.18,.17)*crossing*crossing*.35;
                body+=i.pigment*NereidBeam(i.world)*.48;
                return half4(MixFog(body,i.fog),1);
            }
            ENDHLSL
        }
    }
}
