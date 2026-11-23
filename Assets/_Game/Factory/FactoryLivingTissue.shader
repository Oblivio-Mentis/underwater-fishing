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
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 positionOS:TEXCOORD1;half3 normalWS:TEXCOORD2;half fog:TEXCOORD3;};
            Varyings vert(Attributes input)
            {
                Varyings output;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;output.positionOS=input.positionOS.xyz;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.fog=ComputeFogFactor(p.positionCS.z);return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float3 p=input.positionOS;
                half3 normal=FacetNormalWS(input.positionWS,input.normalWS);
                half cavity=saturate((p.z+.05)*.6)*_CavityDepth;
                // Slow breathing: a faint flush drifting through the hide, well under the mesh pulse already driven from C#.
                half breath=.5+.5*sin(_Time.y*.9+p.z*1.4);
                half3 viewDir=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;lighting.normalWS=normal;
                lighting.viewDirectionWS=viewDir;lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                lighting.fogCoord=input.fog;lighting.vertexLighting=VertexLighting(input.positionWS,normal);lighting.bakedGI=SampleSH(normal);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);lighting.shadowMask=half4(1,1,1,1);
                // Hide sits between the two burgundy bible tones; the authored tint only shifts hue within that band.
                half3 dark=half3(.0437,.0056,.0144),light=half3(.1022,.0144,.0382); // #3A1220 -> #5A2036 in linear
                half3 tintShift=_BaseColor.rgb/max(dot(_BaseColor.rgb,half3(.2126,.7152,.0722)),.01);
                half fold=saturate(.46+.29*normal.y+breath*.055);
                half3 hide=lerp(dark,light,fold)*lerp(half3(1,1,1),tintShift,.35);
                hide=lerp(hide,_VeinColor.rgb,_DetailStrength*(1.-fold));
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=hide*(1-cavity*.67);
                surface.metallic=0;
                surface.smoothness=min(_Smoothness,.40);
                surface.specular=half3(.055,.05,.05);
                surface.normalTS=half3(0,0,1);surface.occlusion=.9-cavity*.45;surface.alpha=1;
                half4 color=UniversalFragmentPBR(lighting,surface);
                // Shallow red transmission where light grazes thin tissue: a wrap term in red only, no emission.
                Light key=GetMainLight(lighting.shadowCoord);
                half wrap=saturate((dot(normal,key.direction)+.9)/1.9);
                half thin=pow(1-saturate(dot(normal,viewDir)),2.5);
                color.rgb+=half3(.085,.012,.018)*key.color*(wrap*wrap*.5+thin*.35)*(1-cavity*.8)*(.88+.12*fold);
                // Second, very wide lobe: the dull sheen of a wet hide that never resolves into a mirror highlight.
                half nh=saturate(dot(normal,normalize(key.direction+viewDir)));
                color.rgb+=key.color*key.shadowAttenuation*pow(nh,5)*.028*(1-cavity);
                color.rgb=MixFog(color.rgb,input.fog);return color;
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
