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
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Grip, _CameraClearance;
            CBUFFER_END
            float3 _NereidReefClarity;

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
                normal*=dot(normal,view)>0?-1:1;
                float3 light=normalize(float3(-.44,.78,-.37));
                half diffuse=.40+.60*saturate(dot(normal,light)*.64+.36);
                half underside=smoothstep(.28,.83,i.uv.y);
                half muscle=smoothstep(.16,.86,i.uv.x);
                half3 hide=lerp(half3(.137,.026,.056),half3(.377,.095,.133),underside*.78+muscle*.18);
                half ridge=exp2(-19*abs(i.uv.y-.89))*.17;
                half frill=.91+.09*sin(i.uv.x*16+i.uv.y*8);
                half sheen=pow(saturate(dot(reflect(-light,normal),-view)),18);
                half lamp=saturate(NereidBeam(i.world));
                half3 wet=hide*diffuse*frill*(1+lamp*.61+_Grip*.11);
                wet+=half3(.24,.094,.106)*(ridge+sheen*(.16+lamp*.30));
                return half4(lerp(wet,NereidWaterFade(wet,view,range,_NereidReefClarity.x),_NereidTurbidity),1);
            }
            ENDHLSL
        }
    }
}
