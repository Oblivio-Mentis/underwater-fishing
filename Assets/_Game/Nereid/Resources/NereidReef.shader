Shader "RAID/Nereid Reef"
{
    Properties
    {
        _Tint("Tint",Color)=(1,1,1,1)
        _Glow("Bioluminescence",Range(0,3))=0
        _Hide("Wet hide",Range(0,1))=0
        _BasaltLayers("Basalt layers",Range(0,1))=0
        _Sand("Current-washed sand",Float)=0
        _Rift("Abyssal strata",Float)=0
        _StoneArch("Exposed limestone fractures",Float)=0
        _Wreck("Oxidized hull finish",Float)=0
        _WreckDirection("Wreck length",Vector)=(0,0,1,0)
        _WreckLampOrigin("First worklight",Vector)=(0,0,0,0)
        _PigmentLightCap("Matte pigment light cap",Float)=0
    }
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
            #include "Assets/_Game/Materials/FacetNormal.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Glow, _Hide, _BasaltLayers, _Sand, _Rift, _PigmentLightCap, _StoneArch, _Wreck;
            float4 _WreckDirection, _WreckLampOrigin;
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
                half3 n = FacetNormalWS(i.pos, i.norm);
                float3 eye=GetCameraPositionWS();
                float3 toSurface=i.pos-eye;
                float d=max(.01,length(toSurface));
                float3 view=toSurface/d;
                // Rift basalt and anything standing in Depth II water loses the surface key
                // (sun direction, shadowed shafts, wet gloss). A weak wrap from above keeps forms round.
                half rift=max(step(.5,_Rift),NereidDeepWater(_NereidReefClarity.x));
                Light key=GetMainLight(TransformWorldToShadowCoord(i.pos));
                half shaft=max(0,n.y)*key.shadowAttenuation*(1-rift);
                half sun=saturate(dot(n,normalize(half3(-.49,.84,-.21))));
                half wrap=saturate(n.y*.5+.5);
                half light=lerp(.43+.54*sun+.14*saturate(dot(n,key.direction))*key.shadowAttenuation,
                    .40+.36*wrap*wrap,rift);
                half3 base=i.color.rgb*_Tint.rgb;
                if (_Rift > .5)
                {
                    half3 cool=NereidLuminance(base)*half3(.72,1.05,1.38);
                    base=lerp(base,cool,.32);
                }
                else if (_BasaltLayers > .5)
                {
                    half crown=saturate(n.y*.65+.25);
                    base=lerp(half3(.075,.106,.117),half3(.145,.185,.170),crown);
                }
                if(_StoneArch>.5) base*=lerp(half3(.86,.90,.92),half3(1.02,1.04,.94),saturate(n.y));
                if(_Wreck>.5) base=lerp(base*.76,half3(.16,.083,.048),.26);
                half wet=saturate(dot(reflect(-normalize(half3(-.49,.84,-.21)),n),-view))*(1-rift);
                half3 rgb=base*(light+litPool*.25*(1-rift)+_Glow);
                // A cool top light so needle crowns and terrace edges keep a silhouette in the rift.
                rgb+=half3(.030,.062,.098)*saturate(n.y)*saturate(n.y)*.85*rift;
                if(_Rift<.5)
                {
                    // Caustics are a soft accent that fades out past working distance.
                    half reach=1-smoothstep(9.,14.,d);
                    rgb+=half3(.078,.175,.14)*.5*caustic(p,time)*shaft*litPool*reach;
                }
                else
                {
                    // Local colour near the deep fissures. Their surroundings still recede into fog.
                    float2 first=i.pos.xz-float2(41,-46.2);
                    float2 second=i.pos.xz-float2(45.5,-58);
                    float2 third=i.pos.xz-float2(52,-52.2);
                    float distanceToVent=min(dot(first,first),min(dot(second,second),dot(third,third)));
                    half vent=exp2(-distanceToVent*.23)*saturate((-i.pos.y-12.5)*.25);
                    rgb+=half3(.013,.086,.079)*vent*(.45+.55*abs(n.y));
                    float2 lower=i.pos.xz-float2(43,-86),fault=i.pos.xz-float2(48.4,-88.5);
                    float2 crown=i.pos.xz-float2(51,-80),terminal=i.pos.xz-float2(47,-95);
                    float2 gate=i.pos.xz-float2(41.5,-77);
                    half blueGlow=exp2(-min(dot(lower,lower),min(dot(crown,crown),dot(gate,gate)))*.135);
                    half violetGlow=exp2(-min(dot(fault,fault),dot(terminal,terminal))*.21);
                    float2 portVoid=i.pos.xz-float2(-29,-35),foreVent=i.pos.xz-float2(17,30);
                    half portGlow=exp2(-dot(portVoid,portVoid)*.091)*saturate((-i.pos.y-17)*.17);
                    half foreGlow=exp2(-dot(foreVent,foreVent)*.112)*saturate((-i.pos.y-11)*.19);
                    rgb+=half3(.038,.026,.096)*portGlow+half3(.012,.074,.092)*foreGlow;
                    rgb+=(blueGlow*half3(.011,.049,.110)+violetGlow*half3(.072,.020,.093))*
                        saturate((-i.pos.y-21.)*.12)*(.50+.50*abs(n.y));
                }
                if(_Wreck>.5)
                {
                    float3 fromLamp=i.pos-_WreckLampOrigin.xyz;
                    float nearest=4.2*clamp(round(dot(fromLamp,_WreckDirection.xyz)/4.2),0,3);
                    float3 delta=fromLamp-_WreckDirection.xyz*nearest;
                    float warmth=exp2(-dot(delta,delta)*.21);
                    rgb+=warmth*half3(.27,.13,.038)*(.44+.56*abs(n.y));
                }
                rgb+=half3(.02,.044,.05)*pow(max(0.,wet),12.)*key.shadowAttenuation;
                // Wavelength-dependent extinction gives both cliff layers an underwater silhouette.
                float range=max(0,d-4);
                half3 transmit=exp(-range*float3(.031,.019,.014));
                half3 water=half3(.010,.071,.098);
                half3 outside=rgb*transmit+water*(1-transmit);
                float pool=NereidBeam(i.pos);
                float face=abs(dot(n,-view));
                float rim=(1.-face)*(1.-face);
                float wrapped=.32+.68*face;
                half3 lit=rgb*_NereidReefClarity.y;
                if(_Sand>.5)
                {
                    // Sand under the key compresses towards .75 luminance; it never goes to white.
                    half lum=NereidLuminance(lit);
                    half knee=.55;
                    half shoulder=knee+(.75-knee)*(1-exp2(-(lum-knee)*5.));
                    lit*=lerp(1.,shoulder/max(lum,.001),step(knee,lum));
                }
                half3 closeColor=lit+pool*(base*(wrapped*1.45)+half3(.016,.027,.028)*.9*wrapped);
                closeColor+=_Hide*pool*rim*half3(.025,.092,.103);
                half3 murk=NereidWaterFade(closeColor,view,d*d,_NereidReefClarity.x);
                half3 workLight=base*HullIrradiance(i.pos,n)*.95;
                half3 workTransmission=lerp(transmit,exp2(-d*_NereidReefClarity.z*half3(.14,.114,.10)),_NereidTurbidity);
                half glassHaze=_RaidDryHaze*smoothstep(4,23,d);
                half3 result=lerp(outside,murk,max(_NereidTurbidity,glassHaze))+workLight*workTransmission;
                // A close mask lamp should still reveal the companion's shell markings.
                if (_PigmentLightCap>0) result=min(result,base*_PigmentLightCap+.022+rgb*_Wreck*.38);
                return half4(result,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
