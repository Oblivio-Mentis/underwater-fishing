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
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
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
            CBUFFER_END
            float3 _NereidReefClarity;

            struct Attributes
            {
                float3 position : POSITION;
                float3 normal : NORMAL;
                half4 pigment : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half3 pigment : TEXCOORD2;
                half beat : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 root = TransformObjectToWorld(float3(0,0,0));
                float phase = dot(root, float3(.71,1.12,-.86));
                float beat = _Time.y * _Billow.x + phase;
                float tail = saturate(.48 - v.position.z);
                v.position.x += sin(beat + v.position.z * 4.7) * tail * tail * _Billow.y;
                v.position.y += cos(beat * .68 + abs(v.position.x) * 2.9) * saturate(abs(v.position.x)-.18) * _Billow.y * .53;
                o.world = TransformObjectToWorld(v.position);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.pigment = v.pigment.rgb;
                o.beat = sin(beat * .41);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 delta = i.world - GetCameraPositionWS();
                float d2 = dot(delta,delta);
                float distanceToEye = sqrt(d2);
                half3 view = -SafeNormalize(delta);
                half luminous = _EyePass;
                half limit = 1-smoothstep(_Range*.74,_Range,distanceToEye);
                // Depth-tested opaque fragments keep the finest brackets behind the hull and window frames.
                half coverage = max(limit, luminous*limit*1.35) * smoothstep(2.5,5.6,distanceToEye) * _Fade;
                float dither = frac(52.9829189 * frac(dot(floor(i.clip.xy), float2(.06711056,.00583715))));
                clip(coverage - dither - .002);

                half3 n = FacetNormalWS(i.world,i.normal);
                half edge = 1 - abs(dot(n,view));
                half overhead = saturate(dot(n, half3(-.32,.90,-.28))*.55+.45);
                half3 pigment = i.pigment * _Tint.rgb;
                half3 color = pigment * (.12 + .28*overhead);
                color += pigment * pow(edge,2.3) * .25;
                half search=saturate(RaidSearchBeam(i.world));
                color += pigment * saturate(NereidBeam(i.world)) * .82;
                half3 column = WaterColumnColor(-view, _NereidReefClarity.x);
                half extinction = exp2(-d2 * max(.00042, _NereidReefClarity.x * .53));
                half3 shadow = lerp(column * .51, color, max(extinction,search*.76));
                half eyeFalloff = exp2(-d2 * .00049);
                half3 lamp = i.pigment * _EyeGlow * eyeFalloff * (.91 + .09*i.beat);
                return half4(lerp(shadow,lamp,luminous),1);
            }
            ENDHLSL
        }
    }
}
