Shader "RAID/Nereid Hull Coating"
{
    Properties
    {
        _BaseColor("Marine finish", Color)=(.31,.45,.48,1)
        _Metallic("Exposed metal", Range(0,1))=.16
        _Smoothness("Smoothness", Range(0,1))=.35
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NereidWaterColumn.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Metallic, _Smoothness;
            CBUFFER_END
            float _NereidTurbidity, _NereidWorkLightSeabed;
            float3 _NereidReefClarity;
            float4 _NereidWorkLightPositions[3], _NereidWorkLightDirections[3], _NereidWorkLightRadiance[3];

            struct Input {float3 position:POSITION;float3 normal:NORMAL;};
            struct Varyings {float4 clip:SV_POSITION;float3 position:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;half3 vertexLight:TEXCOORD3;};
            Varyings vert(Input v)
            {
                Varyings o;
                o.position=TransformObjectToWorld(v.position);
                o.clip=TransformWorldToHClip(o.position);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.fog=ComputeFogFactor(o.clip.z);
                o.vertexLight=VertexLighting(o.position,o.normal);
                return o;
            }

            half3 WaterIrradiance(float3 point,half3 normal)
            {
                // A broad patch of downwelling light gives rounded ballast skins a readable terminator.
                half sky=saturate(.5+.5*dot(normal,half3(-.47,.84,-.27)));
                half3 light=half3(.20,.29,.33)+half3(.30,.56,.64)*sky;
                // Approximate the diffuse return from the actual floodlit patches on the sandy bed.
                [unroll] for(int i=0;i<3;i++)
                {
                    float4 origin=_NereidWorkLightPositions[i];
                    float3 axis=_NereidWorkLightDirections[i].xyz;
                    float travel=max(0.,origin.y-_NereidWorkLightSeabed)/max(.02,-axis.y);
                    float reach=saturate(1.-travel*travel*origin.w)*saturate(-axis.y*4.);
                    float3 toPatch=origin.xyz+axis*travel-point;
                    float distance2=max(dot(toPatch,toPatch),.01);
                    half facing=saturate(.20+.8*dot(normal,toPatch*rsqrt(distance2)));
                    light+=_NereidWorkLightRadiance[i].rgb*(reach*facing/(2400.+distance2*13.));
                }
                return light;
            }

            half4 frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normal);
                InputData lighting=(InputData)0;
                lighting.positionWS=i.position;
                lighting.positionCS=i.clip;
                lighting.normalWS=n;
                lighting.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.position));
                lighting.shadowCoord=TransformWorldToShadowCoord(i.position);
                lighting.vertexLighting=i.vertexLight;
                lighting.bakedGI=SampleSH(n)+WaterIrradiance(i.position,n);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.clip);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb;
                surface.metallic=_Metallic;
                surface.smoothness=_Smoothness;
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                half3 steel=UniversalFragmentPBR(lighting,surface).rgb;
                float3 delta=i.position-GetCameraPositionWS();
                float transmission=exp2(-dot(delta,delta)*_NereidReefClarity.x);
                half3 submerged=lerp(WaterColumnColor(-lighting.viewDirectionWS),steel,transmission);
                return half4(lerp(MixFog(steel,i.fog),submerged,_NereidTurbidity),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
