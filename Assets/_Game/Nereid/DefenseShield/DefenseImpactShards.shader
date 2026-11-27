Shader "RAID/Defense/Impact shards"
{
    Properties
    {
        _ShardTint("Displaced field fragments", Color) = (.46,.90,.96,1)
        [HideInInspector] _FieldState("Field state", Vector) = (1,1,0,0)
        [HideInInspector] _FieldRadii("Semi-axes", Vector) = (7.13,4.585,4.70,.885)
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
            float4 _FieldState, _FieldRadii, _Impact0, _Impact1, _Impact2, _ImpactPower;
            float4 _ImpactNormals[3];
            half4 _ShardTint;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes { float3 shape : POSITION; float2 seed : TEXCOORD0; };
            struct Interpolators { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : COLOR; };
            float Hash(float seed) { return frac(sin(seed * 81.219) * 43758.549); }

            Interpolators Vert(Attributes a)
            {
                int slot = (int)(a.seed.y+.5);
                float4 impact = slot == 0 ? _Impact0 : slot == 1 ? _Impact1 : _Impact2;
                float power = _ImpactPower[slot];
                float seed = a.seed.x;
                float t = saturate((impact.w - Hash(seed + 6.1) * .054) / (.36 + Hash(seed + 3.8) * .43));
                float3 axis = dot(impact.xyz,impact.xyz) > .0001 ? impact.xyz : float3(0,-1,0);
                float3 normal = power > 0 ? _ImpactNormals[slot].xyz : float3(0,-1,0);
                float3 tangent = normalize(cross(abs(normal.y) > .92 ? float3(1,0,0) : float3(0,1,0), normal));
                float3 bitangent = cross(normal, tangent);
                float angle = Hash(seed + 1.3) * 6.283185;
                float3 radial = tangent * cos(angle) + bitangent * sin(angle);
                float breach = 1 - smoothstep(0,.18,_FieldState.x);
                float reach = (1 - exp2(-t * 4.4)) * (.40 + 1.34 * Hash(seed + 7.4))
                    * (.44 + power*.74) * (1 + breach * .7);
                float3 center = axis * _FieldRadii.xyz + float3(0,_FieldRadii.w,0) + radial * reach + normal * (.03 + .35 * t);
                center.y += .45 * t * t;
                float size = (.023 + Hash(seed + 0.7) * .063) * (.45+power*.7) * (1 - .68 * t);
                float3 spin = radial * a.shape.x + bitangent * a.shape.y + normal * a.shape.z;
                Interpolators o;
                o.positionWS = TransformObjectToWorld(center + spin * size);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                half alpha = (1 - smoothstep(.15,.87,t)) * step(.006,t) * saturate(power*1.4);
                half3 tint = lerp(_ShardTint.rgb,half3(1,.48,.21), breach * .62);
                o.color = half4(tint * (1.25 + a.shape.x * .38 + power*.5), alpha);
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
