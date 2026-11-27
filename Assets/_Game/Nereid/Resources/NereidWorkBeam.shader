Shader "RAID/Submerged Work Beam"
{
    Properties { [HideInInspector] _FloodIndex("Work light index",Float)=0 [HideInInspector] _DefenseBeam("Defense searchlight",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" "DisableBatching"="True"}
        Pass
        {
            // Rasterise the exit of the volume. A diver can look out from inside a light shaft.
            Cull Front ZWrite Off ZTest Always
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _FloodIndex;
            float _DefenseBeam;
            CBUFFER_END
            float _NereidTurbidity;
            float _RaidDryHaze;
            float4 _NereidWorkLightRadiance[3],_NereidWorkLightDirections[3];
            float4 _RaidLampDirections[4];
            struct Input {float3 position:POSITION;};
            struct Varyings {float4 clip:SV_POSITION;float3 world:TEXCOORD0;};
            Varyings vert(Input v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.position);
                o.clip=TransformWorldToHClip(o.world);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                // An unbound/fallback texture has no scene depth. A clear pixel in a valid
                // depth buffer represents open water and clips at the camera's far plane.
                if(min(_CameraDepthTexture_TexelSize.z,_CameraDepthTexture_TexelSize.w)<=1.)return 0;
                float rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.clip));
                float3 toExit=i.world-GetCameraPositionWS();
                float endDistance=length(toExit);
                float3 ray=toExit/max(endDistance,.0001);
                float opaqueDistance=LinearEyeDepth(rawDepth,_ZBufferParams)/max(.001,-TransformWorldToViewDir(ray,false).z);
                endDistance=min(endDistance,opaqueDistance);

                // Find a conservative near entry in the unit cone's box; all lengths are metres on the camera ray.
                float3 startOS=TransformWorldToObject(GetCameraPositionWS());
                float3 stepOS=TransformWorldToObjectDir(ray,false);
                float3 invStep=(step(0.,stepOS)*2.-1.)/max(abs(stepOS),1.e-6);
                float3 face0=(float3(-1,-1,0)-startOS)*invStep;
                float3 face1=(float3(1,1,1)-startOS)*invStep;
                float3 nearFaces=min(face0,face1);
                float startDistance=max(0.,Max3(nearFaces.x,nearFaces.y,nearFaces.z));
                if(endDistance<=startDistance)return 0;
                const int samples=12;
                float ds=(endDistance-startDistance)/samples;
                float3 p=startOS+stepOS*(startDistance+.5*ds);
                half accumulated=0;
                [unroll] for(int k=0;k<samples;k++,p+=stepOS*ds)
                {
                    float z=p.z;
                    float r2=dot(p.xy,p.xy)/max(z*z,.00001);
                    float core=saturate(1.-r2);
                    // A diffuser on the emitter and a feathered range eliminate cone/cap outlines.
                    float falloff=smoothstep(0.,.025,z)*(1.-smoothstep(.62,1.,z))/(1.+z*z*16.);
                    accumulated+=core*core*falloff;
                }
                int lamp=(int)_FloodIndex;
                float3 forward=lerp(_NereidWorkLightDirections[min(2,lamp)].xyz,_RaidLampDirections[lamp].xyz,_DefenseBeam);
                half3 radiance=lerp(_NereidWorkLightRadiance[min(2,lamp)].rgb,
                    half3(.54,.78,1)*_RaidLampDirections[lamp].w*2.5,_DefenseBeam);
                float backscatter=saturate(dot(-ray,forward));
                float transmission=exp2(-.032*(startDistance+endDistance)*.5);
                float water=lerp(_NereidTurbidity,max(_NereidTurbidity,_RaidDryHaze),_DefenseBeam);
                half3 light=radiance*(.000006*ds*accumulated*transmission*(.70+.55*backscatter*backscatter)*water);
                return half4(light,0);
            }
            ENDHLSL
        }
    }
}
