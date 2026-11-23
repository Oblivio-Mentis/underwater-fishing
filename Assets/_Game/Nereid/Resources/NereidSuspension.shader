Shader "RAID/Suspended water fines"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+3" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NereidDiveLamp.hlsl"
            float4 _NereidWorkLightPositions[3], _NereidWorkLightDirections[3], _NereidWorkLightRadiance[3];
            CBUFFER_START(UnityPerMaterial)
                float _Waterline, _Density, _DeepWater;
            CBUFFER_END
            struct Input { float3 seed:POSITION; float2 corner:TEXCOORD0; float2 size:TEXCOORD1; };
            struct Vary { float4 clip:SV_POSITION; float2 corner:TEXCOORD0; half4 light:COLOR; };
            Vary vert(Input v)
            {
                float t = _Time.y;
                float phase = dot(v.seed,float3(13.2,29.3,17.7));
                float3 drift = float3(.046+.053*sin(phase),-.031+.043*cos(phase),.033*cos(phase*2.9));
                drift+=_DeepWater*float3(.026,-.015,.017);
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float extent = max(1.,v.size.y);
                // Wrap a world-stationary field around the moving mask; specks do not follow camera turns.
                float3 q = frac(v.seed+(drift*t+.13*sin(t*.54+v.seed*31)-origin)/extent+.5)-.5;
                float3 p = origin+q*extent;
                float dist = distance(GetCameraPositionWS(),p);
                float weight = 1-smoothstep(.38,.5,max(max(abs(q.x),abs(q.y)),abs(q.z)));
                weight *= smoothstep(.28,.75,dist)*(1-smoothstep(14.,24.,dist))*step(p.y,_Waterline)*step(v.seed.z,_Density);
                // Open-water fines sit back (x.8); inside the helmet and hull cones they read x1.5 brighter.
                float3 light = lerp(float3(.126,.186,.179),float3(.077,.127,.143),_DeepWater);
                light+=NereidBeam(p)*lerp(float3(.345,.42,.285),float3(.36,.585,.72),_DeepWater);
                [unroll] for(int j=0;j<3;j++)
                {
                    float4 source = _NereidWorkLightPositions[j], axis = _NereidWorkLightDirections[j], lamp = _NereidWorkLightRadiance[j];
                    float3 ray=p-source.xyz; float sq=max(dot(ray,ray),.001);
                    float cone=saturate((dot(axis.xyz,ray*rsqrt(sq))-axis.w)*lamp.w);
                    float falloff=saturate(1.-sq*source.w);
                    light+=lamp.rgb*(.195*cone*cone*falloff*falloff/max(8.,sq));
                }
                float3 view = TransformWorldToView(p);
                // Keep distant lit debris subpixel with a faint, anti-aliased footprint.
                float radius = max(v.size.x,dist/_ScreenParams.y*.78);
                float angle=phase*.72+t*.21;
                float2 flake=v.corner*float2(1,lerp(.50,1.1,v.seed.y));
                float2 turn=float2(cos(angle),sin(angle));
                view.xy += float2(dot(flake,float2(turn.x,-turn.y)),dot(flake,turn.yx))*radius;
                Vary o; o.clip = mul(UNITY_MATRIX_P,float4(view,1));
                o.corner=v.corner; o.light=half4(min(light,2.0),weight*(.25+.23*v.seed.y)); return o;
            }
            half4 frag(Vary i):SV_Target
            {
                float rr=dot(i.corner,i.corner);
                return half4(i.light.rgb,i.light.a*(1-smoothstep(.12,1.,rr)));
            }
            ENDHLSL
        }
    }
}
