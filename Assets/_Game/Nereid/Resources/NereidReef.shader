Shader "RAID/Nereid Reef"
{
    Properties { _Tint("Tint",Color)=(1,1,1,1) _Glow("Bioluminescence",Range(0,3))=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Glow;
            CBUFFER_END
            struct Input {float3 pos:POSITION;float3 norm:NORMAL;half4 color:COLOR;};
            struct Vary {float4 clip:SV_POSITION;float3 pos:TEXCOORD0;half3 norm:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            Vary vert(Input v)
            {
                Vary o;o.pos=TransformObjectToWorld(v.pos);o.clip=TransformWorldToHClip(o.pos);
                o.norm=TransformObjectToWorldNormal(v.norm);o.color=v.color;o.fog=ComputeFogFactor(o.clip.z);return o;
            }
            float2 jitter(float2 v)
            {
                float3 p=frac(v.xyx*float3(.1031,.1030,.0973));p+=dot(p,p.yzx+32.33);
                return frac((p.xx+p.yz)*p.zy);
            }
            float caustic(float2 p,float time)
            {
                float2 cell=floor(p),f=frac(p);float a=6,b=7;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 d=float2(x,y);
                    float2 offset=.5+.37*sin(6.283*jitter(cell+d)+time);
                    float2 v=d+offset-f;float dist=dot(v,v);
                    b=min(b,max(a,dist));a=min(a,dist);
                }
                return 1-smoothstep(.018,.145,b-a);
            }
            half4 frag(Vary i):SV_Target
            {
                float time=_Time.y*.36;
                float2 p=i.pos.xz*.48+float2(i.pos.y*.11+time*.11,0);
                p+=.27*sin(p.yx*2.4+float2(time,-time*.86));
                half3 n=normalize(i.norm);
                Light key=GetMainLight(TransformWorldToShadowCoord(i.pos));
                half shaft=max(0,n.y)*key.shadowAttenuation;
                half light=.62+.50*saturate(dot(n,normalize(half3(-.42,.91,.23))))+.2*saturate(dot(n,key.direction))*key.shadowAttenuation;
                half3 base=i.color.rgb*_Tint.rgb;
                half3 rgb=base*(light+_Glow)+half3(.13,.34,.29)*caustic(p,time)*shaft*(.42+.25*sin(p.x*.31+p.y*.48));
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}