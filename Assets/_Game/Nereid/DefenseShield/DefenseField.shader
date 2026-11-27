Shader "RAID/Defense/Containment membrane"
{
    Properties
    {
        _FieldTint("Stable field", Color) = (.18,.72,.81,1)
        _WarningTint("Critical field", Color) = (.98,.32,.10,1)
        _GridScale("Cells per metre", Range(.5,3)) = 1.4
        _BodyOpacity("Surface opacity", Range(0,.24)) = .045
        _RimOpacity("Rim opacity", Range(0,.65)) = .23
        _CellOpacity("Lattice opacity", Range(0,.4)) = .081
        [HideInInspector] _FieldState("Integrity, formation, recovery", Vector) = (1,1,0,0)
        [HideInInspector] _FieldRadii("Semi-axes", Vector) = (2.52,1.28,4.88,0)
        [HideInInspector] _BreakAxis("Breach direction", Vector) = (0,-1,0,0)
        [HideInInspector] _Impact0("Impact 0", Vector) = (0,0,0,3)
        [HideInInspector] _Impact1("Impact 1", Vector) = (0,0,0,3)
        [HideInInspector] _Impact2("Impact 2", Vector) = (0,0,0,3)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent"}
        Pass
        {
            Name "Containment membrane"
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _FieldTint, _WarningTint;
            float4 _FieldState, _FieldRadii, _BreakAxis, _Impact0, _Impact1, _Impact2;
            float _GridScale, _BodyOpacity, _RimOpacity, _CellOpacity;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes { float3 positionOS : POSITION; };
            struct Interpolators
            {
                float4 positionCS : SV_POSITION;
                float3 unit : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Interpolators Vert(Attributes v)
            {
                Interpolators o;
                float3 p = v.positionOS;
                float interference = sin(p.x * 8.1 + p.z * 12.4 - _Time.y * 5.3)
                    * sin(p.y * 10.1 - p.z * 7.4 + _Time.y * 3.2);
                float worn = 1 - smoothstep(.04,.68,_FieldState.x);
                p *= 1 + interference * worn * .022;
                o.unit = v.positionOS;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float LinearTrace(float coordinate)
            {
                float gap = abs(frac(coordinate + .5) - .5);
                float pixel = max(.007, fwidth(coordinate));
                return (1 - smoothstep(.017, .017 + pixel, gap)) * (1 - smoothstep(.23, .55, pixel));
            }

            float Lattice(float2 panel)
            {
                const float2 diagonal = float2(.5, .8660254);
                return max(LinearTrace(panel.x),
                    max(LinearTrace(dot(panel, diagonal)), LinearTrace(dot(panel,float2(-diagonal.x,diagonal.y)))));
            }

            float HashCell(float3 cell)
            {
                cell = frac(cell * .1031);
                cell += dot(cell, cell.yzx + 33.33);
                return frac((cell.x + cell.y) * cell.z);
            }

            // x: impact flash, y: a sharp leading edge followed by a wider, weaker return wave.
            float2 Hit(float3 unit, float4 origin)
            {
                if (origin.w > 1.42) return 0;
                float d = length((unit - origin.xyz) * _FieldRadii.xyz);
                float t = max(0, origin.w);
                float peak = exp2(-d * d * 5.2) * (1 - smoothstep(.035,.30,t));
                float advance = d - t * 6.5;
                float first = exp2(-advance * advance * 37);
                float second = exp2(-(advance + .30) * (advance + .30) * 18) * .3;
                float fade = smoothstep(0,.075,t) * (1 - smoothstep(.44,1.42,t));
                return float2(peak, (first + second) * fade);
            }

            half4 Frag(Interpolators i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 unit = normalize(i.unit);
                float3 local = unit * _FieldRadii.xyz;
                float3 fromCamera = i.positionWS - GetCameraPositionWS();
                float d2 = max(dot(fromCamera, fromCamera), .0001);
                float3 direction = fromCamera * rsqrt(d2);
                float3 n = TransformObjectToWorldNormal(unit);
                float rim = pow(1 - abs(dot(n, direction)), 2.4);
                float front = IS_FRONT_VFACE(face, 1.0, .50);
                float3 facets = pow(abs(unit), 5);
                facets /= max(.001, facets.x + facets.y + facets.z);
                float3 panels = local * _GridScale;
                float grid = dot(facets, float3(Lattice(panels.yz), Lattice(panels.xz), Lattice(panels.xy)));

                float integrity = _FieldState.x;
                float formation = _FieldState.y;
                float worn = 1 - smoothstep(.26, .62, integrity);
                float critical = 1 - smoothstep(.06, .30, integrity);
                float current = .66 + .34 * sin(local.z * .71 + local.x * .92 - local.y * 3.7 - _Time.y * 2.25);
                float flow = pow(saturate(current), 6);
                float patch = HashCell(floor(local * float3(2.0,2.7,2.0)));
                float flutter = .66 + .34 * sin(patch * 30 + _Time.y * (2 + patch * 4.2));
                float unstable = lerp(1, flutter, worn * .85);
                float dischargeCoord = local.x * 2.6 - local.y * 4.2 + local.z * 1.3
                    + .19 * sin(local.z * 34 + local.y * 17 + _Time.y * 11);
                float arc = LinearTrace(dischargeCoord) * critical
                    * smoothstep(.78, .96, flutter) * step(.81, patch);
                float2 hit = saturate(Hit(unit,_Impact0) + Hit(unit,_Impact1) + Hit(unit,_Impact2));

                // Assembly sweeps out from the hull. On breach the lattice fragments from the impact.
                float relativeHeight = .50 - .50 * unit.y;
                float fray = lerp(relativeHeight, .5 + dot(unit, _BreakAxis.xyz) * .49,
                    1 - _FieldState.z);
                float threshold = lerp(fray, patch, .23);
                float mask = smoothstep(threshold - .10, threshold + .05, formation * 1.16 - .10);
                float assemblyEdge = (threshold - formation * 1.16 + .10) * 13;
                float assembly = exp2(-assemblyEdge * assemblyEdge)
                    * (1 - smoothstep(.83,.99,formation)) * smoothstep(.015,.16,formation);
                float baseOpacity = _BodyOpacity + _RimOpacity * rim + grid * _CellOpacity * (.48 + .52 * flow);
                baseOpacity *= unstable * lerp(1,.72,critical);
                float accent = hit.x * .36 + hit.y * (.073 + grid * .29) + arc * .34 + assembly * (.13 + grid * .22);
                float extinction = _NereidReefClarity.x;
                float contrast = exp2(-d2 * max(extinction * .42, .0008));
                float alpha = saturate((baseOpacity + accent) * mask * front * contrast);

                half3 pigment = lerp(_FieldTint.rgb, _WarningTint.rgb, worn * .48 + critical * .42);
                half3 radiance = pigment * (.43 + rim * .38 + grid * .22 + flow * .15)
                    + lerp(pigment,half3(.82,1,1),.75) * (hit.x * 1.48 + hit.y * .65 + assembly * .68 + arc);
                half3 color = NereidWaterFade(radiance, direction, d2 * .53, extinction);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
