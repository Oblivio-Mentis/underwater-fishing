Shader "RAID011/Living Tissue"
{
    Properties
    {
        _BaseColor("Tissue tone",Color)=(.18,.12,.2,1)
        _VeinColor("Subtle vascular tone",Color)=(.1,.045,.085,1)
        _Smoothness("Wet surface",Range(0,1))=.4
        _DetailStrength("Surface detail",Range(0,.3))=.1
        _CavityDepth("Cavity darkening",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _VeinColor;
        half _Smoothness;
        half _DetailStrength;
        half _CavityDepth;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
        ENDHLSL
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
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 positionOS:TEXCOORD1;half3 normalWS:TEXCOORD2;half fog:TEXCOORD3;};
            float TissueHash(float3 p) {p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float TissueNoise(float3 p)
            {
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                float lo=lerp(lerp(TissueHash(i),TissueHash(i+float3(1,0,0)),f.x),lerp(TissueHash(i+float3(0,1,0)),TissueHash(i+float3(1,1,0)),f.x),f.y);
                float hi=lerp(lerp(TissueHash(i+float3(0,0,1)),TissueHash(i+float3(1,0,1)),f.x),lerp(TissueHash(i+float3(0,1,1)),TissueHash(i+float3(1,1,1)),f.x),f.y);
                return lerp(lo,hi,f.z);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.positionOS=input.positionOS.xyz;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float3 p=input.positionOS;
                half broad=TissueNoise(p*3.3),fine=TissueNoise(p*13+float3(broad,broad*2,broad));
                half veins=smoothstep(.64,.86,TissueNoise(p*7+float3(0,broad*1.7,0)));
                half3 smallNormal=half3(TissueNoise(p*10+11),TissueNoise(p*10+29),TissueNoise(p*10+47))-.5;
                half3 normal=normalize(input.normalWS+smallNormal*_DetailStrength*.5);
                half cavity=saturate((p.z+.05)*.6)*_CavityDepth;
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;lighting.normalWS=normal;
                lighting.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                lighting.fogCoord=input.fog;lighting.vertexLighting=VertexLighting(input.positionWS,normal);lighting.bakedGI=SampleSH(normal);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(_BaseColor.rgb,_VeinColor.rgb,veins*.22)*(1+(broad-.5)*.14)*(1-cavity*.67);
                surface.metallic=0;surface.specular=half3(.08,.08,.08);surface.smoothness=_Smoothness+(fine-.5)*.08;
                surface.normalTS=half3(0,0,1);surface.occlusion=.9-cavity*.45;surface.alpha=1;
                half4 color=UniversalFragmentPBR(lighting,surface);color.rgb=MixFog(color.rgb,input.fog);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment depthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;float3 _LightPosition;
            float4 shadowVert(Attributes input):SV_POSITION
            {
                float3 world=TransformObjectToWorld(input.positionOS.xyz),normal=TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-world);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,normal,direction));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 depthFrag():SV_Target {return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            float4 depthVert(Attributes input):SV_POSITION {return TransformObjectToHClip(input.positionOS.xyz);}
            half4 depthFrag():SV_Target {return 0;}
            ENDHLSL
        }
    }
}
