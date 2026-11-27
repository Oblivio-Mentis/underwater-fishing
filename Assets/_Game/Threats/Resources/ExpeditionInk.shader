Shader "RAID/Expedition/Remora ink"
{
    Properties
    {
        _Density("Density", Range(0,1)) = 1
        _FlowSeed("Flow seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Front ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Density, _FlowSeed;
            CBUFFER_END

            struct Varyings { float4 clip:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings Vert(float3 at:POSITION)
            {
                Varyings o;
                o.world=TransformObjectToWorld(at);
                o.clip=TransformWorldToHClip(o.world);
                return o;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float3 eye=GetCameraPositionWS();
                float3 ray=SafeNormalize(i.world-eye);
                float3 origin=TransformWorldToObject(eye);
                float3 direction=TransformWorldToObjectDir(ray,false);
                float a=dot(direction,direction);
                float b=dot(direction,origin);
                float c=dot(origin,origin)-.25;
                float delta=b*b-a*c;
                if(delta<=0 || _Density<.004)return 0;
                float root=sqrt(delta);
                float first=max(0,(-b-root)/a);
                float last=(-b+root)/a;
                if(min(_CameraDepthTexture_TexelSize.z,_CameraDepthTexture_TexelSize.w)>1)
                {
                    float depth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.clip));
                    float scene=LinearEyeDepth(depth,_ZBufferParams)/max(.001,-TransformWorldToViewDir(ray,false).z);
                    last=min(last,scene);
                }
                if(first>=last)return 0;
                float span=(last-first)/9;
                float drift=_Time.y*.76+_FlowSeed;
                float mass=0, light=0;
                [unroll] for(int n=0;n<9;n++)
                {
                    float3 p=origin+direction*(first+(n+.5)*span);
                    float soft=saturate(1-dot(p,p)*4);
                    float sheet=sin(p.z*14+sin(p.x*13-drift)*1.3+sin(p.y*12+drift*.83));
                    float folds=sin(p.x*22+p.y*15+p.z*8+drift)*sin(p.y*27-p.z*17-drift);
                    float grain=sin(p.z*42+p.y*24+folds*2-drift*2);
                    float eddy=saturate(.47+sheet*.32+folds*.24+grain*.16);
                    float core=soft*soft*smoothstep(.20,.69,eddy);
                    mass+=core;
                    light+=core*smoothstep(.48,.84,eddy)*smoothstep(-.42,.38,p.y);
                }
                half alpha=(1-exp2(-mass*span*4.5*_Density))*saturate((last-first)*4);
                half tint=saturate(light/max(.0001,mass));
                return half4(lerp(half3(.018,.009,.028),half3(.20,.081,.185),tint*tint),alpha*.94);
            }
            ENDHLSL
        }
    }
}
