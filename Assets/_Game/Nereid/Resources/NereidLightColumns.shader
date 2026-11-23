Shader "RAID/Nereid Light Columns"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _NereidTurbidity;
            struct Input {float3 pos:POSITION;float3 norm:NORMAL;half4 col:COLOR;};
            struct Vary {float4 clip:SV_POSITION;float3 pos:TEXCOORD0;half3 norm:TEXCOORD1;};
            Vary vert(Input i){Vary o;o.pos=TransformObjectToWorld(i.pos);o.clip=TransformWorldToHClip(o.pos);o.norm=TransformObjectToWorldNormal(i.norm);return o;}
            half4 frag(Vary i):SV_Target
            {
                float thickness=abs(dot(normalize(i.norm),SafeNormalize(GetWorldSpaceViewDir(i.pos))));
                float distanceToEye=distance(GetCameraPositionWS(),i.pos);
                float current=sin(dot(i.pos.xz,float2(.91,.37))+i.pos.y*.25+_Time.y*.36);
                float fade=smoothstep(-16.5,-8,i.pos.y)*(1-smoothstep(14,23,i.pos.y))*smoothstep(1.4,7,distanceToEye)*(1-smoothstep(30,85,distanceToEye));
                float murk=lerp(1,.46*(1-smoothstep(9,25,distanceToEye)),_NereidTurbidity);
                // Shelf-water shafts: a blue-grey column of scattered light, not a saturated teal blade.
                return half4(.30,.60,.72,thickness*thickness*fade*murk*(.070+.016*current));
            }
            ENDHLSL
        }
    }
}