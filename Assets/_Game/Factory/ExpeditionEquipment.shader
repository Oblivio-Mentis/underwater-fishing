Shader "RAID/Faceted Expedition Equipment"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Rubber("Seal rubber",Color)=(.14,.17,.18,1)
        _Fabric("Suit fabric",Color)=(.24,.29,.31,1)
        _Steel("Pressure steel",Color)=(.40,.45,.47,1)
        _LightSteel("Light alloy",Color)=(.62,.65,.65,1)
        _Safety("Safety ochre",Color)=(.84,.55,.20,1)
        _Bronze("Oxide bronze",Color)=(.47,.31,.19,1)
        _Visor("Visor blue",Color)=(.20,.34,.39,1)
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

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Rubber, _Fabric, _Steel, _LightSteel, _Safety, _Bronze, _Visor;
            half _FacetWeight, _Metallic, _Smoothness;
            CBUFFER_END

            struct Vertex { float3 position:POSITION; float3 normal:NORMAL; half4 paint:COLOR; };
            struct Interpolants
            {
                float4 clip:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half3 pigment:TEXCOORD2;
                half fog:TEXCOORD3;
                half3 vertexLight:TEXCOORD4;
            };
            half3 PartColour(half encoded)
            {
                int paint=(int)round(encoded*(255./32.));
                switch(paint)
                {
                    case 0:return _Rubber.rgb;
                    case 1:return _Fabric.rgb;
                    case 2:return _Steel.rgb;
                    case 3:return _LightSteel.rgb;
                    case 4:return _Safety.rgb;
                    case 5:return _Bronze.rgb;
                    default:return _Visor.rgb;
                }
            }
            Interpolants Vert(Vertex v)
            {
                Interpolants o;
                o.world=TransformObjectToWorld(v.position);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.clip=TransformWorldToHClip(o.world);
                o.pigment=PartColour(v.paint.r);
                o.fog=ComputeFogFactor(o.clip.z);
                o.vertexLight=VertexLighting(o.world,o.normal);
                return o;
            }
            half4 Frag(Interpolants i):SV_Target
            {
                half3 face=FacetNormalWS(i.world,i.normal);
                half3 n=SafeNormalize(lerp(i.normal,face,_FacetWeight));
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=i.pigment*_BaseColor.rgb;
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
