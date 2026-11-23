Shader "RAID/Pressure Lock Water"
{
    Properties
    {
        _Shallow("Shallow water", Color)=(.12,.46,.50,1)
        _Deep("Deep water", Color)=(.016,.09,.11,1)
        _Foam("Foam", Color)=(.78,.93,.92,1)
        _Refraction("Refraction", Range(0,.2))=.07
        _WaveScale("Wave scale", Range(.2,4))=1
        [HideInInspector] _PumpDisturbance("Surface contact and signed pump drive", Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Lock water surface"
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Shallow, _Deep, _Foam, _PumpDisturbance;
            float _Refraction, _WaveScale;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 local:TEXCOORD1; float4 screen:TEXCOORD2;};

            // Three crossing wave trains plus a short chop; the pump adds radial rings around its contact point.
            float Height(float2 p,float t)
            {
                p*=_WaveScale;
                float h=.022*sin(p.x*3.1+t*1.9+.8*sin(p.y*2.2-t*.7));
                h+=.016*sin(p.y*4.7-t*2.6+.5*sin(p.x*3.9+t));
                h+=.009*sin((p.x+p.y)*7.3+t*3.4);
                h+=.004*sin(p.x*15.-t*5.1)*sin(p.y*13.+t*4.3);
                float drive=_PumpDisturbance.z;
                float2 d=p/_WaveScale-_PumpDisturbance.xy;
                float r=length(d);
                float ring=(1.-smoothstep(.1,1.4,r))*abs(drive);
                h+=ring*.018*sin(r*22.-t*11.);
                return h;
            }
            float3 Normal(float2 p,float t)
            {
                const float e=.03;
                float hx=Height(p+float2(e,0),t)-Height(p-float2(e,0),t);
                float hz=Height(p+float2(0,e),t)-Height(p-float2(0,e),t);
                return normalize(float3(-hx/(2*e),1,-hz/(2*e)));
            }
            V vert(A v)
            {
                V o;
                o.local=v.positionOS.xz;
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.world.y+=Height(o.world.xz,_Time.y);
                o.positionCS=TransformWorldToHClip(o.world);
                o.screen=ComputeScreenPos(o.positionCS);
                return o;
            }
            half4 frag(V i,bool front:SV_IsFrontFace):SV_Target
            {
                float t=_Time.y;
                float2 uv=i.screen.xy/i.screen.w;
                float3 eye=GetCameraPositionWS();
                float3 view=normalize(eye-i.world);
                bool under=eye.y<i.world.y;
                float3 n=Normal(i.world.xz,t);
                if(under)n=-n;

                // Refract the opaque scene through the ripples. Where the offset would pull a
                // nearer object across the surface, fall back to the straight sample.
                float surfaceDepth=LinearEyeDepth(i.positionCS.z,_ZBufferParams);
                float2 bend=n.xz*_Refraction*saturate(2.5/surfaceDepth);
                float2 uvR=uv+bend;
                float behindR=LinearEyeDepth(SampleSceneDepth(uvR),_ZBufferParams);
                if(behindR<surfaceDepth-.02){uvR=uv;behindR=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);}
                float3 scene=SampleSceneColor(uvR);
                float thickness=max(0,behindR-surfaceDepth);

                // Water column absorbs with distance; the chamber is small so the ramp is short.
                float absorb=1.-exp(-thickness*1.9);
                float3 tint=lerp(_Shallow.rgb,_Deep.rgb,absorb);
                float3 color=lerp(scene*lerp(1.,tint*1.35,.55),tint,absorb*.78);

                float fres=pow(1.-saturate(dot(n,view)),4.);
                float3 lamp=normalize(float3(.18,1.,-.22));
                float spec=pow(saturate(dot(reflect(-lamp,n),view)),110.)*1.6;
                float sheen=pow(saturate(dot(reflect(-lamp,n),view)),9.)*.22;

                // Foam: on the chamber walls and the pump contact, plus faint crest lines.
                float edge=1.-saturate(thickness/.16);
                float crest=smoothstep(.035,.06,Height(i.world.xz+float2(.21,-.13),t*1.03)+.013*sin(i.world.x*17.+t*2.));
                float drive=_PumpDisturbance.z;
                float2 fromPump=i.world.xz-_PumpDisturbance.xy;
                float r=length(fromPump);
                float localPump=(1.-smoothstep(.12,1.05,r))*abs(drive);
                float foam=edge*.75+crest*.18*(1.-absorb);
                if(localPump>.001)
                {
                    float phase=r*31.-t*12.;
                    if(drive<0.)phase=r*21.+atan2(fromPump.y,fromPump.x+.00001)*3.+t*7.;
                    float rings=smoothstep(.32,.95,sin(phase+.42*sin(9.*fromPump.x+5.*t)));
                    float center=1.-smoothstep(.05,.24,r);
                    foam+=localPump*(rings*.55+center*.35*step(0.,drive));
                    color=lerp(color,_Deep.rgb*.8,center*localPump*step(drive,0.));
                }
                foam=saturate(foam);

                if(under)
                {
                    // Looking up from inside the flood: a mirror-silver ceiling at grazing angles,
                    // the dry chamber visible straight above, lamp caustics wobbling across it.
                    float tir=pow(1.-saturate(dot(n,view)),2.2);
                    float3 ceiling=lerp(scene,float3(.55,.70,.71),tir*.85);
                    float caustic=pow(saturate(.5+.5*sin(i.world.x*9.+t*2.3)*sin(i.world.z*7.-t*1.7)),6.)*.35;
                    color=ceiling+caustic+spec*.5;
                    color=lerp(color,_Foam.rgb,foam*.5);
                    return half4(color,1.);
                }

                color=lerp(color,lerp(_Shallow.rgb*1.4,float3(.46,.62,.64),fres),fres*.55);
                color+=spec+sheen;
                color=lerp(color,_Foam.rgb,foam);
                return half4(color,1.);
            }
            ENDHLSL
        }
    }
}
