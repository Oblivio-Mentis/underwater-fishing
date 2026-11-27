Shader "RAID/Nereid Defense School"
{
    Properties
    {
        _Tint("Pigment", Color) = (1,1,1,1)
        _Glow("Bioluminescence", Range(0,3)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Glow;
            CBUFFER_END

            float3 _NereidReefClarity;
            half _NereidTurbidity;

            struct Attributes
            {
                float3 position:POSITION;
                float3 normal:NORMAL;
                half4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half3 pigment:TEXCOORD2;
                half fog:TEXCOORD3;
            };

            Varyings Vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 origin=TransformObjectToWorld(float3(0,0,0));
                float phase=dot(origin,float3(.35,.29,.51));
                float tail=saturate((.12-i.position.z)*.89);
                i.position.x+=sin(_Time.y*7.2+i.position.z*7+phase)*tail*tail*.15;
                Varyings o;
                o.world=TransformObjectToWorld(i.position);
                o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);
                o.pigment=i.color.rgb*_Tint.rgb;
                o.fog=ComputeFogFactor(o.clip.z);
                return o;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float3 delta=i.world-GetCameraPositionWS();
                float distanceSquared=dot(delta,delta);
                float3 viewingRay=delta*rsqrt(max(.0001,distanceSquared));
                half light=.36+.64*saturate(dot(normalize(i.normal),half3(-.45,.82,-.35)));
                half3 close=i.pigment*(light+_Glow);
                half3 water=NereidWaterFade(close,viewingRay,distanceSquared,_NereidReefClarity.x);
                // The lenses show through suspended silt a short distance beyond the muted body silhouette.
                half eyeVisibility=exp2(-distanceSquared*_NereidReefClarity.x*.56);
                water+=i.pigment*_Glow*eyeVisibility*.40;
                return half4(lerp(MixFog(close,i.fog),water,_NereidTurbidity),1);
            }
            ENDHLSL
        }
    }
}
