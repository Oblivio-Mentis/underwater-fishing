Shader "RAID/Nereid Hull Coating"
{
    Properties
    {
        _BaseColor("Marine finish", Color)=(.31,.45,.48,1)
        _Metallic("Exposed metal", Range(0,1))=.16
        _Smoothness("Smoothness", Range(0,1))=.35
        _Marine("Exterior marine light", Range(0,1))=1
        _DeckTread("Modular deck panels", Range(0,1))=0
        _DeckPlateSize("Deck panel width and length (metres)", Vector)=(2.4,3.6,0,0)
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
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            #include "NereidWaterColumn.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Metallic, _Smoothness, _Marine, _DeckTread;
            float4 _DeckPlateSize;
            CBUFFER_END
            float _NereidTurbidity, _NereidWorkLightSeabed;
            float3 _NereidReefClarity;
            float4 _NereidWorkLightPositions[3], _NereidWorkLightDirections[3], _NereidWorkLightRadiance[3];

            struct Input {float3 position:POSITION;float3 normal:NORMAL;};
            struct Varyings
            {
                float4 clip:SV_POSITION;
                float3 position:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half fog:TEXCOORD2;
                half3 vertexLight:TEXCOORD3;
                float3 panelPosition:TEXCOORD4;
                half3 panelNormal:TEXCOORD5;
            };
            Varyings vert(Input v)
            {
                Varyings o;
                o.position=TransformObjectToWorld(v.position);
                o.clip=TransformWorldToHClip(o.position);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.fog=ComputeFogFactor(o.clip.z);
                o.vertexLight=VertexLighting(o.position,o.normal);
                float3x3 localToWorld=(float3x3)GetObjectToWorldMatrix();
                float3 metres=float3(length(mul(localToWorld,float3(1,0,0))),
                    length(mul(localToWorld,float3(0,1,0))),length(mul(localToWorld,float3(0,0,1))));
                o.panelPosition=v.position*metres;
                o.panelNormal=normalize(v.normal/max(metres,.0001));
                return o;
            }

            half3 WaterIrradiance(float3 positionWS,half3 normal)
            {
                // A broad patch of downwelling light gives rounded ballast skins a readable terminator.
                half sky=saturate(.5+.5*dot(normal,half3(-.47,.84,-.27)));
                half3 light=half3(.15,.23,.27)+half3(.23,.45,.52)*sky;
                // Approximate the diffuse return from the actual floodlit patches on the sandy bed.
                [unroll] for(int i=0;i<3;i++)
                {
                    float4 origin=_NereidWorkLightPositions[i];
                    float3 axis=_NereidWorkLightDirections[i].xyz;
                    float travel=max(0.,origin.y-_NereidWorkLightSeabed)/max(.02,-axis.y);
                    float reach=saturate(1.-travel*travel*origin.w)*saturate(-axis.y*4.);
                    float3 toPatch=origin.xyz+axis*travel-positionWS;
                    float distance2=max(dot(toPatch,toPatch),.01);
                    half facing=saturate(.20+.8*dot(normal,toPatch*rsqrt(distance2)));
                    light+=_NereidWorkLightRadiance[i].rgb*(reach*facing/(2800.+distance2*16.));
                }
                return light;
            }

            half DeckJoint(float2 metres)
            {
                float2 size = max(_DeckPlateSize.xy, .25);
                float2 edge = (.5 - abs(frac(metres / size) - .5)) * size;
                float2 footprint = max(fwidth(metres), .001);
                float2 seam = 1. - smoothstep(.008, .008 + footprint, edge);
                // Narrow joints gradually disappear at grazing angles.
                seam *= saturate(.025 / footprint);
                return max(seam.x, seam.y);
            }

            half4 frag(Varyings i):SV_Target
            {
                half3 n = FacetNormalWS(i.position, i.normal);
                InputData lighting=(InputData)0;
                lighting.positionWS=i.position;
                lighting.positionCS=i.clip;
                lighting.normalWS=n;
                lighting.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.position));
                lighting.shadowCoord=TransformWorldToShadowCoord(i.position);
                lighting.vertexLighting=i.vertexLight;
                lighting.bakedGI=SampleSH(n)+(_Marine>.5 ? WaterIrradiance(i.position,n) : half3(.089,.10,.11));
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.clip);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                half joint = _DeckTread > .5 && i.panelNormal.y > .65 ? DeckJoint(i.panelPosition.xz) : 0;
                surface.albedo = _BaseColor.rgb * (1. - .14 * joint);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                half3 steel=UniversalFragmentPBR(lighting,surface).rgb;
                // Roll off reflections from close work lights while preserving their shape.
                half peak=max(steel.r,max(steel.g,steel.b));
                half specular=max(0.,peak-.69);
                if(specular>0.)steel*=(.69+specular/(1.+specular*.84))/peak;
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
