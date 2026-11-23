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
            #include "NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Back,_Belly,_Fin;
            CBUFFER_END
            float3 _NereidReefClarity;

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
                half2 shade:TEXCOORD4; // belly weight, fin membrane
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
                bool fin=i.uv.x>=.66;
                o.pigment=i.uv.x<.33?_Back.rgb:fin?_Fin.rgb:_Belly.rgb;
                // Countershading from the object normal, same logic as the catchable fish but per vertex.
                o.shade=half2(smoothstep(-.15,.75,-normalize(i.normal).y),fin?1:0);
                o.fog=ComputeFogFactor(o.clip.z);
                return o;
            }

            half4 Fragment(Output i):SV_Target
            {
                half3 n=normalize(i.normal);
                half3 v=SafeNormalize(GetWorldSpaceViewDir(i.world));
                half3 overhead=half3(-.31,.91,-.26);
                half belly=i.shade.x;
                half3 pigment=i.pigment*lerp(half3(.88,.96,1.07),half3(1.14,1.05,.94)*(1.+.16*belly),belly);
                half wrapped=saturate((dot(n,overhead)+.55)/1.55);
                half nh=saturate(dot(n,normalize(overhead+v)));
                half sheen=pow(nh,9)*.04+pow(nh,30)*.07;
                half face=saturate(abs(dot(n,v)));
                half edge=1-face;
                half3 body=pigment*(.42+wrapped*wrapped*.66);
                body+=half3(.25,.36,.36)*(sheen*2.2+edge*.16);
                // Fin membranes take a thin pale rim along their edge.
                body+=half3(.30,.42,.42)*pow(edge,3.)*(.25+.45*i.shade.y);
                half crossing=saturate(dot(n,normalize(overhead-v)));
                body+=half3(.12,.18,.17)*crossing*crossing*.35;
                body+=pigment*NereidBeam(i.world)*(.42+.30*face);
                float3 delta=i.world-GetCameraPositionWS();
                float transmission=exp2(-dot(delta,delta)*_NereidReefClarity.x);
                half3 submerged=lerp(WaterColumnColor(-v),body,transmission);
                return half4(lerp(MixFog(body,i.fog),submerged,_NereidTurbidity),1);
            }
            ENDHLSL
        }
    }
}
