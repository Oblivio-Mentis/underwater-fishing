Shader "RAID/Nereid Reef"
{
    Properties { _Tint("Tint",Color)=(1,1,1,1) _Glow("Bioluminescence",Range(0,3))=0 _Hide("Wet hide",Range(0,1))=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Glow, _Hide;
            CBUFFER_END
            #include "NereidDiveLamp.hlsl"
            #include "NereidWaterColumn.hlsl"
            // Squared-distance extinction, diffuse skylight and work-light attenuation at this depth.
            float3 _NereidReefClarity;
            // This large generated terrain is shared by both Forward renderers. Read the three
            // actual spot lights so distant per-object light selection cannot move their footprints.
            float4 _NereidWorkLightPositions[3], _NereidWorkLightDirections[3], _NereidWorkLightRadiance[3];
            half3 HullIrradiance(float3 position,half3 n)
            {
                float3 total=0;
                [unroll] for(int j=0;j<3;j++)
                {
                    float4 source=_NereidWorkLightPositions[j], axis=_NereidWorkLightDirections[j], lamp=_NereidWorkLightRadiance[j];
                    float3 ray=position-source.xyz;
                    float squared=max(dot(ray,ray),.01);
                    float3 dir=ray*rsqrt(squared);
                    float spot=saturate((dot(axis.xyz,dir)-axis.w)*lamp.w);
                    float fraction=squared*source.w;
                    float falloff=saturate(1-fraction*fraction);
                    float incidence=saturate(dot(n,-dir));
                    total+=lamp.rgb*(spot*spot*falloff*falloff*incidence/max(4.,squared));
                }
                return total;
            }
            struct Input {float3 pos:POSITION;float3 norm:NORMAL;half4 color:COLOR;};
            struct Vary {float4 clip:SV_POSITION;float3 pos:TEXCOORD0;half3 norm:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;};
            Vary vert(Input v)
            {
                Vary o;o.pos=TransformObjectToWorld(v.pos);o.clip=TransformWorldToHClip(o.pos);
                o.norm=TransformObjectToWorldNormal(v.norm);o.color=v.color;o.fog=ComputeFogFactor(o.clip.z);return o;
            }
            float2 jitter(float2 v)
            {
                float3 p=frac(v.xyx*float3(.1031,.1030,.0973));p+=dot(p,p.yzx+32.33);
                return frac((p.xx+p.yz)*p.zy);
            }
            float ReefNoise(float2 p)
            {
                float2 i=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(jitter(i).x,jitter(i+float2(1,0)).x,f.x),lerp(jitter(i+float2(0,1)).x,jitter(i+1).x,f.x),f.y);
            }
            float caustic(float2 p,float time)
            {
                float2 cell=floor(p),f=frac(p);float a=6,b=7;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 d=float2(x,y);
                    float2 offset=.5+.37*sin(6.283*jitter(cell+d)+time);
                    float2 v=d+offset-f;float dist=dot(v,v);
                    b=min(b,max(a,dist));a=min(a,dist);
                }
                return 1-smoothstep(.0,.22,b-a);
            }
            half4 frag(Vary i):SV_Target
            {
                float time=_Time.y*.36;
                float2 projected=i.pos.xz+float2(-.28,.24)*i.pos.y;
                float2 p=projected*float2(.36,.62)+float2(time*.09,time*.027);
                p+=.38*sin(p.yx*1.42+float2(time,-time*.61));
                half field=ReefNoise(projected*.103+float2(time*.045,0));
                half litPool=smoothstep(.31,.83,field)*smoothstep(.22,.74,ReefNoise(projected*.23+field*1.86));
                half grain=ReefNoise(i.pos.xz*3.1+i.pos.y*.72);
                half3 n=normalize(i.norm);
                Light key=GetMainLight(TransformWorldToShadowCoord(i.pos));
                half shaft=max(0,n.y)*key.shadowAttenuation;
                half sun=saturate(dot(n,normalize(half3(-.49,.84,-.21))));
                half light=.43+.54*sun+.14*saturate(dot(n,key.direction))*key.shadowAttenuation;
                half3 base=i.color.rgb*_Tint.rgb;
                half wet=saturate(dot(reflect(-normalize(half3(-.49,.84,-.21)),n),SafeNormalize(GetWorldSpaceViewDir(i.pos))));
                half3 rgb=base*((light+litPool*.29)*(.90+.15*grain)+_Glow);
                rgb+=half3(.078,.175,.14)*caustic(p,time)*shaft*litPool+half3(.02,.044,.05)*pow(wet,12)*key.shadowAttenuation;
                // Wavelength-dependent extinction gives both cliff layers an underwater silhouette.
                float range=max(0,distance(GetCameraPositionWS(),i.pos)-4);
                half3 transmit=exp(-range*float3(.031,.019,.014));
                half3 water=half3(.010,.071,.098);
                half3 outside=rgb*transmit+water*(1-transmit);
                float3 toSurface=i.pos-GetCameraPositionWS();
                float d=max(.01,length(toSurface));
                float pool=NereidBeam(i.pos);
                float face=abs(dot(n,-toSurface/d));
                float rim=(1.-face)*(1.-face);
                float wrapped=.32+.68*face;
                float clarity=exp2(-d*d*_NereidReefClarity.x);
                half3 closeColor=rgb*_NereidReefClarity.y+pool*(base*(wrapped*1.45)+half3(.016,.027,.028)*(.4+grain)*wrapped);
                closeColor+=_Hide*pool*rim*half3(.025,.092,.103);
                half3 murk=lerp(WaterColumnColor(toSurface/d),closeColor,clarity);
                half3 workLight=base*HullIrradiance(i.pos,n)*(.86+.18*grain);
                half3 workTransmission=lerp(transmit,exp2(-d*_NereidReefClarity.z*half3(.14,.114,.10)),_NereidTurbidity);
                return half4(lerp(outside,murk,_NereidTurbidity)+workLight*workTransmission,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
