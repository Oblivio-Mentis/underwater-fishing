Shader "Raid/Kraken Gullet"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Input { float3 position:POSITION; float2 uv:TEXCOORD0; };
            struct Surface { float4 position:SV_POSITION; float3 local:TEXCOORD0; float2 uv:TEXCOORD1; };

            Surface Vert(Input input)
            {
                Surface output;
                output.position=TransformObjectToHClip(input.position);
                output.local=input.position;
                output.uv=input.uv;
                return output;
            }

            half4 Frag(Surface input) : SV_Target
            {
                float depth=max(0,input.local.z);
                float3 facet=normalize(cross(ddx(input.local),ddy(input.local)));
                float angle=input.uv.x*6.2831853;
                float light=abs(dot(facet,normalize(float3(-.36,.42,-.92))));
                float ridges=.64+.36*sin(depth*24+sin(angle*6.0)*1.6);
                float tissue=.56+.44*sin(angle*8.0+depth*2.7);
                float gloss=pow(saturate(light),6)*pow(ridges,5);
                float spill=exp2(-depth*3.45);
                half3 rim=lerp(half3(.115,.017,.037),half3(.32,.069,.089),tissue*.68+light*.26);
                half3 color=(rim*(.73+.31*ridges)+half3(.25,.083,.066)*gloss)*spill;
                color+=half3(.0021,.0016,.0039)*(1-spill);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
