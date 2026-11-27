Shader "RAID/Expedition/Squid tongue"
{
    Properties
    {
        _Grip("Contraction", Range(0,1)) = 0
        [HideInInspector] _CameraClearance("Camera clearance", Float) = .075
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Grip, _CameraClearance;
            CBUFFER_END
            float3 _NereidReefClarity;
            half _NereidTurbidity;

            struct Varyings { float4 clip:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; };
            Varyings Vert(float3 vertex:POSITION,float2 uv:TEXCOORD0)
            {
                Varyings o;
                o.world=TransformObjectToWorld(vertex);
                o.clip=TransformWorldToHClip(o.world);
                o.uv=uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 delta=i.world-GetCameraPositionWS();
                float range=dot(delta,delta);
                clip(range-_CameraClearance*_CameraClearance);
                float3 view=SafeNormalize(delta);
                float3 normal=SafeNormalize(cross(ddy(i.world),ddx(i.world)));
                half facet=.37+.63*abs(dot(normal,half3(-.36,.83,-.40)));
                half ridge=1-smoothstep(.09,.32,abs(sin(i.uv.x*53-_Time.y*8)));
                half throat=exp2(-18*abs(i.uv.x-.85));
                half3 wet=lerp(half3(.24,.035,.074),half3(.83,.17,.18),ridge*.46+throat*.38)*facet;
                wet+=half3(.35,.052,.057)*_Grip*(ridge*.70+throat);
                return half4(lerp(wet,NereidWaterFade(wet,view,range,_NereidReefClarity.x),_NereidTurbidity),1);
            }
            ENDHLSL
        }
    }
}
