Shader "Raid/Fish Proximity Outline"
{
    Properties
    {
        _Ink ("Stroke", Color) = (.93,1,.99,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Width ("Stroke width in pixels", Float) = 3
        [HideInInspector] _Cull ("Cull", Float) = 1
        [HideInInspector] _Channels ("Color channels", Float) = 15
        [HideInInspector] _StencilCompare ("Stencil comparison", Float) = 6
        [HideInInspector] _StencilPass ("Stencil operation", Float) = 0
        [HideInInspector] _StencilWrite ("Stencil write mask", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            ColorMask [_Channels]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 8
                ReadMask 8
                WriteMask [_StencilWrite]
                Comp [_StencilCompare]
                Pass [_StencilPass]
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Ink;
            float _Opacity, _Width;
            CBUFFER_END

            float4 Vert(float3 position : POSITION,float3 normal : NORMAL) : SV_POSITION
            {
                float3 world=TransformObjectToWorld(position);
                float4 clip=TransformWorldToHClip(world);
                float4 tip=TransformWorldToHClip(world+TransformObjectToWorldNormal(normal)*.06);
                float2 screen=max(_ScaledScreenParams.xy,1);
                float2 projected=(tip.xy/max(tip.w,.001)-clip.xy/max(clip.w,.001))*screen;
                float2 outward=projected/max(length(projected),.0001);
                clip.xy+=outward*(2*_Width/screen)*clip.w;
                return clip;
            }

            half4 Frag() : SV_Target
            {
                return half4(_Ink.rgb,_Ink.a*_Opacity);
            }
            ENDHLSL
        }
    }
}
