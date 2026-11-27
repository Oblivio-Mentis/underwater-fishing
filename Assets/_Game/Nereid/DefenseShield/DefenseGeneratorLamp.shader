Shader "RAID/Defense/Generator lamp"
{
    Properties
    {
        _Ready("Generator ready", Color) = (.30,.88,.97,1)
        _Alarm("Generator alarm", Color) = (1,.39,.15,1)
        _Offline("Generator offline", Color) = (.035,.067,.080,1)
        [HideInInspector] _GeneratorState("Integrity, formation, recovery, impact age", Vector) = (1,1,0,3)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
        Pass
        {
            Name "Generator lamp"
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Ready, _Alarm, _Offline;
            float4 _GeneratorState;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes { float3 position : POSITION; half4 pigment : COLOR; };
            struct Interpolators { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 pigment : COLOR; };
            Interpolators Vert(Attributes a)
            {
                Interpolators o;
                o.positionWS = TransformObjectToWorld(a.position);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.pigment = a.pigment;
                return o;
            }
            half4 Frag(Interpolators i) : SV_Target
            {
                float integrity = _GeneratorState.x;
                float formation = _GeneratorState.y;
                float warning = 1 - smoothstep(.18,.61,integrity);
                float phase = dot(i.positionWS.xz,float2(.38,.57));
                float hazard = .57 + .43 * sin(phase + _Time.y * 6.1);
                float power = smoothstep(.02,.62,formation);
                power = lerp(power, max(.17,power), _GeneratorState.z * step(.001, formation));
                power *= lerp(.88,.32 + .64 * hazard, warning);
                float hit = (1 - smoothstep(.02,.23,_GeneratorState.w)) * power;
                half3 radiance = lerp(_Offline.rgb, lerp(_Ready.rgb,_Alarm.rgb,warning),power)
                    * (.66 + i.pigment.r * .40 + hit * .5);
                float3 ray = i.positionWS - GetCameraPositionWS();
                float d2 = max(dot(ray,ray),.0001);
                return half4(NereidWaterFade(radiance,ray * rsqrt(d2),d2 * .75,_NereidReefClarity.x),1);
            }
            ENDHLSL
        }
    }
}
