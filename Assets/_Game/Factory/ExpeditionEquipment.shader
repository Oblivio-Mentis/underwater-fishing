Shader "RAID/Faceted Expedition Equipment"
{
    Properties
    {
        [MainTexture] _BaseMap("Source colour atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _PaletteStrength("Simplify source colours", Range(0,1)) = .9
        _FacetWeight("Flat face lighting", Range(0,1)) = .88
        _Metallic("Metallic", Range(0,1)) = .17
        _Smoothness("Smoothness", Range(0,1)) = .26
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Game/Materials/FacetNormal.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _PaletteStrength, _FacetWeight, _Metallic, _Smoothness;
            CBUFFER_END

            struct Vertex { float3 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct Interpolants
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                float2 uv:TEXCOORD2;
                half fog:TEXCOORD3;
                half3 vertexLight:TEXCOORD4;
            };
            Interpolants Vert(Vertex v)
            {
                Interpolants o;
                o.world=TransformObjectToWorld(v.position);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.clip=TransformWorldToHClip(o.world);
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.fog=ComputeFogFactor(o.clip.z);
                o.vertexLight=VertexLighting(o.world,o.normal);
                return o;
            }
            half3 SuitPalette(half3 source)
            {
                half luma=dot(source,half3(.2126,.7152,.0722));
                half3 graphite=lerp(half3(.019,.026,.032),half3(.082,.103,.115),smoothstep(.029,.071,luma));
                half3 steel=lerp(graphite,half3(.255,.280,.280),smoothstep(.14,.22,luma));
                steel=lerp(steel,half3(.55,.56,.51),smoothstep(.37,.49,luma));
                half3 ochre=lerp(half3(.185,.063,.015),half3(.54,.215,.035),smoothstep(.08,.19,luma));
                half paint=smoothstep(.22,.46,(source.r-source.b)/max(source.r,.03));
                return lerp(steel,ochre,paint);
            }
            half4 Frag(Interpolants i):SV_Target
            {
                half3 face=FacetNormalWS(i.world,i.normal);
                half3 n=SafeNormalize(lerp(i.normal,face,_FacetWeight));
                half3 source=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(source,SuitPalette(source),_PaletteStrength)*_BaseColor.rgb;
                surface.metallic=_Metallic;
                surface.smoothness=_Smoothness;
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                InputData light=(InputData)0;
                light.positionWS=i.world;
                light.positionCS=i.clip;
                light.normalWS=n;
                light.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.world));
                light.shadowCoord=TransformWorldToShadowCoord(i.world);
                light.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.clip);
                light.bakedGI=SampleSH(n);
                light.vertexLighting=i.vertexLight;
                light.shadowMask=half4(1,1,1,1);
                return half4(MixFog(UniversalFragmentPBR(light,surface).rgb,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
