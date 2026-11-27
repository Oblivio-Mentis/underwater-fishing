Shader "RAID/Defense/Swarm Silhouette"
{
    Properties
    {
        _Tint ("Body colour multiplier", Color) = (1,1,1,1)
        [HideInInspector] _EyePass ("Eye surface", Float) = 0
        _EyeGlow ("Eye brightness", Range(0,3)) = 1.35
        _Fade ("Swarm visibility", Range(0,1)) = 1
        _Range ("Dissolve distance", Float) = 47
        _Billow ("Swim speed and flexibility", Vector) = (5,.22,0,0)
        [HideInInspector] _CloudBatch ("Batched bank", Float) = 0
        [HideInInspector] _CloudDistanceScale ("Cloud distance scale", Float) = 1
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolightprobe nolightmap nolodfade
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidWaterColumn.hlsl"
            #include "Assets/_Game/Nereid/Resources/NereidDiveLamp.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _EyeGlow, _Fade, _EyePass;
            float _Range;
            float4 _Billow;
            float _CloudBatch, _CloudDistanceScale;
            float4 _CloudPoints[110], _CloudHeadings[110];
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes
            {
                float3 position : POSITION;
                float3 normal : NORMAL;
                half4 pigment : COLOR;
                float4 bank : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half3 pigment : TEXCOORD2;
                half beat : TEXCOORD3;
                float2 halo : TEXCOORD4;
                half visibility : TEXCOORD5;
            };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 root = TransformObjectToWorld(float3(0,0,0));
                int index = (int)v.bank.x;
                if(_CloudBatch>.5)root=_CloudPoints[index].xyz;
                float phase = dot(root, float3(.71,1.12,-.86));
                float beat = _Time.y * _Billow.x + phase;
                float tail = saturate(.48 - v.position.z);
                v.position.x += sin(beat + v.position.z * 4.7) * tail * tail * _Billow.y;
                v.position.y += cos(beat * .68 + abs(v.position.x) * 2.9) * saturate(abs(v.position.x)-.18) * _Billow.y * .53;
                o.world = TransformObjectToWorld(v.position);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.halo=v.bank.yz;
                o.visibility=1;
                if(_CloudBatch>.5)
                {
                    float3 forward=_CloudHeadings[index].xyz;
                    float3 side=normalize(cross(float3(0,1,0),forward));
                    float3 up=cross(forward,side);
                    o.world=root+(v.position.x*side+v.position.y*up+v.position.z*forward)*_CloudPoints[index].w;
                    o.normal=v.normal.x*side+v.normal.y*up+v.normal.z*forward;
                    if(v.bank.w>.5)
                    {
                        float3 view=normalize(GetCameraPositionWS()-o.world);
                        o.visibility=smoothstep(-.37,.27,dot(view,forward))*smoothstep(-.28,.67,dot(view,o.normal));
                        float travel=distance(o.world,GetCameraPositionWS());
                        float size=lerp(.20,.82,smoothstep(16,73,travel/_CloudDistanceScale))*_CloudDistanceScale;
                        float3 right=UNITY_MATRIX_I_V._m00_m10_m20;
                        float3 vertical=UNITY_MATRIX_I_V._m01_m11_m21;
                        o.world+=(right*v.bank.y+vertical*v.bank.z)*size+view*.065;
                    }
                }
                o.clip = TransformWorldToHClip(o.world);
                o.pigment = v.pigment.rgb;
                o.beat = sin(beat * .41);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 delta = i.world - GetCameraPositionWS();
                float d2 = dot(delta,delta);
                float distanceToEye = sqrt(d2);
                float glowDistance = lerp(1,_CloudDistanceScale,_CloudBatch);
                float glowD2 = d2/(glowDistance*glowDistance);
                half3 view = -SafeNormalize(delta);
                half luminous = _EyePass;
                half limit = 1-smoothstep(_Range*.74,_Range,distanceToEye);
                if(luminous>1.5)
                {
                    float haloRadius=dot(i.halo,i.halo);
                    half falloff=exp2(-8*haloRadius)*.39 + exp2(-46*haloRadius)*1.6;
                    half halo=saturate(1-haloRadius)*falloff*limit*_Fade*i.visibility;
                    halo*=exp2(-glowD2*.00017);
                    return half4(i.pigment*halo*_EyeGlow*(.88+.12*i.beat),0);
                }
                // Depth-tested opaque fragments keep the finest brackets behind the hull and window frames.
                half coverage = max(limit, luminous*limit*1.35) * smoothstep(2.5,5.6,distanceToEye) * _Fade;
                float dither = frac(52.9829189 * frac(dot(floor(i.clip.xy), float2(.06711056,.00583715))));
                clip(coverage - dither - .002);

                half3 n = FacetNormalWS(i.world,i.normal);
                half edge = saturate(1 - abs(dot(n,view)));
                half overhead = saturate(dot(n, half3(-.32,.90,-.28))*.55+.45);
                half3 pigment = i.pigment * _Tint.rgb;
                half3 color = pigment * (.12 + .28*overhead);
                color += pigment * pow(edge,2.3) * .25;
                half search=saturate(RaidSearchBeam(i.world));
                color += pigment * saturate(NereidBeam(i.world)) * .82;
                half3 column = WaterColumnColor(-view, _NereidReefClarity.x);
                half extinction = exp2(-d2 * max(.00042, _NereidReefClarity.x * lerp(.53,.20,_CloudBatch)));
                half3 shadow = lerp(column * .51, color, max(extinction,search*.76));
                half eyeFalloff = exp2(-glowD2 * lerp(.00049,.00020,_CloudBatch));
                half3 lamp = i.pigment * _EyeGlow * eyeFalloff * (.91 + .09*i.beat);
                return half4(lerp(shadow,lamp,luminous),1);
            }
            ENDHLSL
        }
    }
}
