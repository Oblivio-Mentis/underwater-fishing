Shader "RAID/Defense/Impact shards"
{
    Properties
    {
        _ShardTint("Displaced field fragments", Color) = (.46,.90,.96,1)
        [HideInInspector] _FieldState("Field state", Vector) = (1,1,0,0)
        [HideInInspector] _FieldRadii("Semi-axes", Vector) = (2.52,1.28,4.88,0)
        [HideInInspector] _Impact0("Impact 0", Vector) = (0,0,0,3)
        [HideInInspector] _Impact1("Impact 1", Vector) = (0,0,0,3)
        [HideInInspector] _Impact2("Impact 2", Vector) = (0,0,0,3)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent"}
        Pass
        {
            Name "Impact shards"
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _FieldState, _FieldRadii, _Impact0, _Impact1, _Impact2;
            half4 _ShardTint;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes { float3 shape : POSITION; float2 seed : TEXCOORD0; };
            struct Interpolators { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : COLOR; };
            float Hash(float seed) { return frac(sin(seed * 81.219) * 43758.549); }

            Interpolators Vert(Attributes a)
            {
                float4 impact = a.seed.y < .5 ? _Impact0 : a.seed.y < 1.5 ? _Impact1 : _Impact2;
                float seed = a.seed.x;
                float t = saturate((impact.w - Hash(seed + 6.1) * .072) / (.30 + Hash(seed + 3.8) * .36));
                float3 axis = normalize(impact.xyz + float3(.0001,.0003,.0001));
                float3 normal = normalize(axis / _FieldRadii.xyz);
                float3 tangent = normalize(cross(abs(normal.y) > .92 ? float3(1,0,0) : float3(0,1,0), normal));
                float3 bitangent = cross(normal, tangent);
                float angle = Hash(seed + 1.3) * 6.283185;
                float3 radial = tangent * cos(angle) + bitangent * sin(angle);
                float breach = 1 - smoothstep(0,.18,_FieldState.x);
                float reach = (1 - exp2(-t * 3.3)) * (.28 + .94 * Hash(seed + 7.4)) * (1 + breach * .7);
                float3 center = axis * _FieldRadii.xyz + radial * reach + normal * (.03 + .35 * t);
                center.y += .45 * t * t;
                float size = (.019 + Hash(seed + 0.7) * .043) * (1 - .68 * t);
                float3 spin = radial * a.shape.x + bitangent * a.shape.y + normal * a.shape.z;
                Interpolators o;
                o.positionWS = TransformObjectToWorld(center + spin * size);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                half alpha = (1 - smoothstep(.15,.87,t)) * step(.006,t);
                half3 tint = lerp(_ShardTint.rgb,half3(1,.48,.21), breach * .62);
                o.color = half4(tint * (1.03 + a.shape.x * .27), alpha);
                return o;
            }

            half4 Frag(Interpolators i) : SV_Target
            {
                float3 ray = i.positionWS - GetCameraPositionWS();
                float d2 = max(dot(ray,ray), .0001);
                float extinction = _NereidReefClarity.x;
                half alpha = i.color.a * exp2(-d2 * max(.001, extinction * .58));
                half3 color = NereidWaterFade(i.color.rgb, ray * rsqrt(d2), d2 * .62, extinction);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
