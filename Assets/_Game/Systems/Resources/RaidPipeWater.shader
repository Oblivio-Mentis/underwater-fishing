Shader "Raid/Water inside pressure pipes"
{
    Properties
    {
        _Window ("Water head, tail, pressure", Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-16" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Window;
            CBUFFER_END

            struct Input { float3 position:POSITION; float2 uv:TEXCOORD0; float4 flow:TEXCOORD1; };
            struct Surface { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float2 flow:TEXCOORD1; };

            Surface Vert(Input input)
            {
                Surface output;
                float wave=sin(input.uv.x*12-_Time.y*11)+.45*sin(input.uv.x*19+input.uv.y*8-_Time.y*17);
                input.position.y+=input.flow.z*.45*wave;
                output.position=TransformObjectToHClip(input.position);
                output.uv=input.uv;
                output.flow=input.flow.xy;
                return output;
            }

            half4 Frag(Surface input) : SV_Target
            {
                float along=input.flow.x;
                float edge=smoothstep(-.008,.023,_Window.x-along);
                float wake=_Window.y<.001?1:smoothstep(-.005,.04,along-_Window.y);
                float band=input.uv.x*1.48-_Time.y*2.85;
                float foam=smoothstep(.65,.99,sin(band*5.1+sin(input.uv.y*7.1+band)*1.65));
                float ripples=.50+.50*sin(band*11.7+input.uv.y*13.1);
                float rip=foam*(.31+.69*ripples);
                float crest=1-smoothstep(.024,.20,abs(input.uv.y-.08));
                float upper=input.flow.y;
                half3 body=lerp(half3(.045,.27,.36),half3(.16,.62,.73),smoothstep(-.80,.14,input.uv.y));
                half3 color=lerp(body,half3(.27,.72,.80),upper);
                color=lerp(color,half3(.73,1.04,1.06),rip*(.50+.41*upper)+crest*(1-upper)*.22);
                return half4(color,saturate((.67+.22*upper+.09*rip)*edge*wake*_Window.z));
            }
            ENDHLSL
        }
    }
}
