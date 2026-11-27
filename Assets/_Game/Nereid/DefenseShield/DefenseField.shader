Shader "RAID/Defense/Containment membrane"
{
    Properties
    {
        _FieldTint("Stable field", Color) = (.18,.72,.81,1)
        _WarningTint("Critical field", Color) = (.98,.32,.10,1)
        _GridScale("Cells per metre", Range(.5,3)) = 1.4
        _BodyOpacity("Surface opacity", Range(0,.24)) = .055
        _RimOpacity("Rim opacity", Range(0,.65)) = .43
        _SillCharge("Sill illumination", Range(0,.45)) = .24
        _CellOpacity("Lattice opacity", Range(0,.4)) = .125
        [HDR] _PipeGlowTint("Pipe seal glow", Color) = (.15,1.05,1.28,1)
        _PipeBandWidth("Pipe seal width", Range(.018,.22)) = .065
        [HideInInspector] _FieldState("Integrity, formation, recovery", Vector) = (1,1,0,0)
        [HideInInspector] _FieldRadii("Semi-axes", Vector) = (7.13,4.585,4.70,.885)
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
            half4 _FieldTint, _WarningTint, _PipeGlowTint;
            float4 _FieldState, _FieldRadii, _BreakAxis, _Impact0, _Impact1, _Impact2, _ImpactPower;
            float4 _PipePoints[12], _PipeAxes[12];
            float _GridScale, _BodyOpacity, _RimOpacity, _CellOpacity, _SillCharge, _PipeBandWidth, _PipeContactCount;
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Interpolators
            {
                float4 positionCS : SV_POSITION;
                float3 unit : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
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
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
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
            float2 Hit(float3 unit, float4 origin, float power)
            {
                if (origin.w > 1.75 || power <= 0) return 0;
                float d = length((unit - origin.xyz) * _FieldRadii.xyz);
                float t = max(0, origin.w);
                float peak = exp2(-d * d * lerp(10,2.4,saturate(power))) * (1 - smoothstep(.045,.36,t));
                float advance = d - t * lerp(3.7,8.4,saturate(power));
                float first = exp2(-advance * advance * 23);
                float second = exp2(-(advance + .48) * (advance + .48) * 11) * .42;
                float wake = exp2(-(advance + .70) * (advance + .70) * 2) * .22;
                float fade = smoothstep(0,.055,t) * (1 - smoothstep(.55,1.65,t));
                return float2(peak, (first + second + wake) * fade) * power;
            }

            float PipeSeal(float3 local)
            {
                float seal = 0;
                [loop] for (int p = 0; p < (int)_PipeContactCount; p++)
                {
                    float3 offset = local - _PipePoints[p].xyz;
                    float along = dot(offset, _PipeAxes[p].xyz);
                    float radius = _PipePoints[p].w;
                    float radial = length(offset - along * _PipeAxes[p].xyz) - radius;
                    float band = max(0,radial-.014) / max(_PipeBandWidth,.018);
                    float glow = exp2(-band*band) * smoothstep(-.025,.035,radial);
                    glow *= 1-smoothstep(radius*1.62+.12,radius*3.2+.3,abs(along));
                    seal = max(seal,glow);
                }
                return seal;
            }

            half4 Frag(Interpolators i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 unit = i.unit;
                float3 local = unit * _FieldRadii.xyz + float3(0,_FieldRadii.w,0);
                float3 fromCamera = i.positionWS - GetCameraPositionWS();
                float d2 = max(dot(fromCamera, fromCamera), .0001);
                float3 direction = fromCamera * rsqrt(d2);
                float3 n = normalize(i.normalWS);
                float rim = pow(1 - abs(dot(n, direction)), 2.4);
                float front = IS_FRONT_VFACE(face, 1.0, .64);
                float3 facets = pow(abs(unit), 5);
                facets /= max(.001, facets.x + facets.y + facets.z);
                float travel=_Time.y;
                float3 shift=float3(travel*.072,-travel*.18,travel*.103);
                float3 distortion=.15*sin(local.zxy*1.16-travel*.81)+.07*cos(local.yzx*2.4+travel*.57);
                float3 panels=local*_GridScale+shift+distortion;
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
                float2 hit = saturate(Hit(unit,_Impact0,_ImpactPower.x) + Hit(unit,_Impact1,_ImpactPower.y)
                    + Hit(unit,_Impact2,_ImpactPower.z));

                // Assembly sweeps out from the hull. On breach the lattice fragments from the impact.
                float relativeHeight = -unit.y;
                float fray = lerp(relativeHeight, .5 + dot(normalize(unit), normalize(_BreakAxis.xyz)) * .49,
                    1 - _FieldState.z);
                float threshold = lerp(fray, patch, .23);
                float mask = smoothstep(threshold - .10, threshold + .05, formation * 1.16 - .10);
                float assemblyEdge = (threshold - formation * 1.16 + .10) * 13;
                float assembly = exp2(-assemblyEdge * assemblyEdge)
                    * (1 - smoothstep(.83,.99,formation)) * smoothstep(.015,.16,formation);
                float band=pow(saturate(.52+.48*sin(local.y*1.6-local.z*.49+local.x*.29+travel*1.13)),8);
                float baseOpacity = _BodyOpacity + _RimOpacity * rim + grid * _CellOpacity * (.41 + .40 * flow + .49*band) + band*.012;
                baseOpacity *= unstable * lerp(1,.72,critical);
                float sillDepth = -unit.y * _FieldRadii.y;
                float sill = exp2(-sillDepth * sillDepth * 27);
                float feed = .63 + .37 * pow(saturate(.55 + .45 * sin(local.z * 2.8 + local.x * .38 - _Time.y * 1.85)),3);
                float sillPower = sill * feed * _SillCharge * lerp(1,.66,critical);
                float seal = PipeSeal(local) * lerp(1,.60,critical);
                float joint = 1-smoothstep(1.0005,1.0105,abs(unit.z));
                float accent = hit.x * .55 + hit.y * (.17 + grid * .43) + arc * .34
                    + assembly * (.13 + grid * .22) + sillPower + seal*.39;
                float extinction = _NereidReefClarity.x;
                float contrast = exp2(-d2 * max(extinction * .42, .0008));
                float alpha = saturate((baseOpacity + accent) * mask * joint * front * contrast);

                half3 pigment = lerp(_FieldTint.rgb, _WarningTint.rgb, worn * .48 + critical * .42);
                half3 radiance = pigment * (.52 + rim * .45 + grid * .36 + flow * .15)
                    + lerp(pigment,half3(.82,1,1),.75) * (hit.x * 2.3 + hit.y * 1.18 + assembly * .68 + arc + sillPower * 2.4)
                    + _PipeGlowTint.rgb*seal*1.3;
                half3 color = NereidWaterFade(radiance, direction, d2 * .53, extinction);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
