Shader "RAID/Water Pressure"
{
    Properties
    {
        _Tint("Tint", Color) = (0.26, 0.92, 1, 1)
        _Phase("Phase", Float) = 0
        _Mode("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+18" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Phase;
                half _Mode;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(input.positionOS);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half rim=1.0h-abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.positionWS)));
                half alpha;
                if(_Mode>2.5h)
                {
                    half petals=.74h+.26h*sin(i.uv.x*59+i.uv.y*31-_Phase*12);
                    alpha=(.54h+rim*rim*.26h)*petals*pow(saturate(1-_Phase),1.5h);
                }
                else if(_Mode>1.5h)
                {
                    float u=i.uv.x, h=i.uv.y;
                    half fade=smoothstep(0,.04,h)*(1-smoothstep(.54,1,h));
                    float spin=-u*5+h*(2.45+1.85*h)+_Time.y*.92+sin(h*7+_Time.y*2)*.08;
                    half ribbon=smoothstep(.44,.91,cos(spin*6.283185));
                    half filament=smoothstep(.82,.993,cos(spin*6.283185));
                    half seam=smoothstep(.64,.96,cos((-u*9+h*(4.2+2.1*h)+_Time.y*1.39)*6.283185));
                    alpha=fade*(.023h+.18h*ribbon+.22h*filament+.08h*seam)*(.35h+.65h*rim)*_Phase;
                }
                else if(_Mode>.5h)
                {
                    half ring=abs(length(i.uv*2-1)-.82h);
                    half front=1-smoothstep(.005h,.074h,ring);
                    half tail=(1-smoothstep(.015h,.07h,abs(length(i.uv*2-1)-.68h)))*.3h;
                    alpha=(front+tail)*saturate(1-_Phase)*.65h;
                }
                else
                {
                    half turbulence=.72h+.28h*sin(i.uv.x*91+i.uv.y*47-_Phase*18);
                    alpha=(pow(rim,5)*.91h+pow(rim,1.65h)*.13h)*turbulence*pow(saturate(1-_Phase),1.4h);
                }
                return half4(_Tint.rgb,alpha*_Tint.a);
            }
            ENDHLSL
        }
    }
}
