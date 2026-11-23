Shader "RAID/Nereid Water Distance"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background+1" "RenderType"="Background"}
        Pass
        {
            Cull Front ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "NereidWaterColumn.hlsl"
            float _NereidTurbidity;
            float3 _NereidReefClarity;
            struct Input {float3 pos:POSITION;};
            struct Vary {float4 clip:SV_POSITION;float3 dir:TEXCOORD0;};
            Vary vert(Input i)
            {
                Vary o;o.dir=normalize(i.pos);
                o.clip=TransformWorldToHClip(GetCameraPositionWS()+o.dir*45);
                #if UNITY_REVERSED_Z
                o.clip.z=.00001*o.clip.w;
                #else
                o.clip.z=.99999*o.clip.w;
                #endif
                return o;
            }
            half4 frag(Vary i):SV_Target
            {
                float3 dir=normalize(i.dir);
                float upper=smoothstep(-.06,.78,dir.y);
                float lower=smoothstep(-.88,-.06,dir.y);
                float toward=saturate(dot(dir,normalize(float3(-.4,.84,-.24))));
                float shimmer=.53+.24*sin(dir.x*17+dir.z*9+_Time.y*.10)+.2*sin(dir.z*32-dir.x*3-_Time.y*.13);
                half3 horizon=lerp(half3(.003,.021,.034),half3(.012,.083,.117),lower);
                half3 sky=lerp(horizon,half3(.026,.179,.228),upper);
                sky+=half3(.034,.135,.126)*pow(toward,8)*(.68+.32*shimmer);
                // The same column the object fog resolves to; the surface shimmer belongs to Depth I only.
                half shallow=1-NereidDeepWater(_NereidReefClarity.x);
                half3 surfaceGlow=half3(.007,.024,.032)*pow(toward,6)*(.85+.15*shimmer)*shallow;
                return half4(lerp(sky,WaterColumnColor(dir,_NereidReefClarity.x)+surfaceGlow,_NereidTurbidity),1);
            }
            ENDHLSL
        }
    }
}
