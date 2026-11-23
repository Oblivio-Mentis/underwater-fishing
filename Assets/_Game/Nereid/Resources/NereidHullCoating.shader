Shader "RAID/Nereid Hull Coating"
{
    Properties
    {
        _BaseColor("Marine finish", Color)=(.31,.45,.48,1)
        _Metallic("Exposed metal", Range(0,1))=.16
        _Smoothness("Smoothness", Range(0,1))=.35
        [NoScaleOffset] _SurfaceAtlas("Panel texture atlas", 2D)="white" {}
        _SurfaceRegion("Atlas tile", Vector)=(0,0,1,1)
        _SurfaceTiling("Repeats per metre", Vector)=(1,1,0,0)
        _SurfaceDetail("Average, contrast, relief, enabled", Vector)=(1,0,0,0)
        _Marine("Exterior marine light", Range(0,1))=1
        _DeckTread("Raised deck flutes", Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NereidWaterColumn.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Metallic, _Smoothness, _Marine, _DeckTread;
            float4 _SurfaceRegion, _SurfaceTiling, _SurfaceDetail;
            CBUFFER_END
            TEXTURE2D(_SurfaceAtlas);
            SAMPLER(sampler_SurfaceAtlas);
            float _NereidTurbidity, _NereidWorkLightSeabed;
            float3 _NereidReefClarity;
            float4 _NereidWorkLightPositions[3], _NereidWorkLightDirections[3], _NereidWorkLightRadiance[3];

            struct Input {float3 position:POSITION;float3 normal:NORMAL;};
            struct Varyings
            {
                float4 clip:SV_POSITION;
                float3 position:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half fog:TEXCOORD2;
                half3 vertexLight:TEXCOORD3;
                float3 panelPosition:TEXCOORD4;
                half3 panelNormal:TEXCOORD5;
            };
            Varyings vert(Input v)
            {
                Varyings o;
                o.position=TransformObjectToWorld(v.position);
                o.clip=TransformWorldToHClip(o.position);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.fog=ComputeFogFactor(o.clip.z);
                o.vertexLight=VertexLighting(o.position,o.normal);
                float3x3 localToWorld=(float3x3)GetObjectToWorldMatrix();
                float3 metres=float3(length(mul(localToWorld,float3(1,0,0))),
                    length(mul(localToWorld,float3(0,1,0))),length(mul(localToWorld,float3(0,0,1))));
                o.panelPosition=v.position*metres;
                o.panelNormal=normalize(v.normal/max(metres,.0001));
                return o;
            }

            half3 WaterIrradiance(float3 positionWS,half3 normal)
            {
                // A broad patch of downwelling light gives rounded ballast skins a readable terminator.
                half sky=saturate(.5+.5*dot(normal,half3(-.47,.84,-.27)));
                half3 light=half3(.15,.23,.27)+half3(.23,.45,.52)*sky;
                // Approximate the diffuse return from the actual floodlit patches on the sandy bed.
                [unroll] for(int i=0;i<3;i++)
                {
                    float4 origin=_NereidWorkLightPositions[i];
                    float3 axis=_NereidWorkLightDirections[i].xyz;
                    float travel=max(0.,origin.y-_NereidWorkLightSeabed)/max(.02,-axis.y);
                    float reach=saturate(1.-travel*travel*origin.w)*saturate(-axis.y*4.);
                    float3 toPatch=origin.xyz+axis*travel-positionWS;
                    float distance2=max(dot(toPatch,toPatch),.01);
                    half facing=saturate(.20+.8*dot(normal,toPatch*rsqrt(distance2)));
                    light+=_NereidWorkLightRadiance[i].rgb*(reach*facing/(2800.+distance2*16.));
                }
                return light;
            }

            float HullHash(float2 p)
            {
                float3 q=frac(float3(p.xyx)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }

            float HullNoise(float2 p)
            {
                float2 id=floor(p), t=frac(p);
                t=t*t*(3.-2.*t);
                float a=lerp(HullHash(id),HullHash(id+float2(1,0)),t.x);
                float b=lerp(HullHash(id+float2(0,1)),HullHash(id+1.),t.x);
                return lerp(a,b,t.y);
            }

            half SamplePanel(float2 axes)
            {
                float2 repeated=axes*_SurfaceTiling.xy;
                float2 cell=frac(repeated);
                float2 uv=_SurfaceRegion.xy+cell*_SurfaceRegion.zw;
                float2 gradients=repeated*_SurfaceRegion.zw;
                half3 texel=SAMPLE_TEXTURE2D_GRAD(_SurfaceAtlas,sampler_SurfaceAtlas,uv,ddx(gradients),ddy(gradients)).rgb;
                float2 edge=min(cell,1.-cell);
                float2 aa=clamp(fwidth(repeated),.001,.024);
                float2 borders=1.-smoothstep(float2(.013,.014)-aa,float2(.021,.028)+aa,edge);
                float joint=max(borders.x,borders.y);
                float lip=(1.-smoothstep(.057-aa.x,.072+aa.x,min(edge.x,edge.y)))*(1.-joint);
                float2 studDist=(edge-float2(.075,.087))*float2(1.05,1);
                float fastener=1.-smoothstep(.012,.025+max(aa.x,aa.y),length(studDist));
                float panel=dot(texel,half3(.2126,.7152,.0722))/max(_SurfaceDetail.x,.015);
                return panel*(1.-joint*.44)+lip*.26+fastener*.40;
            }

            half PanelHeight(Varyings i)
            {
                half3 alignment=abs(i.panelNormal);
                alignment*=alignment;
                alignment*=alignment;
                alignment/=max(dot(alignment,half3(1,1,1)),.001);
                return (SamplePanel(i.panelPosition.zy)*alignment.x+
                    SamplePanel(i.panelPosition.xz)*alignment.y+
                    SamplePanel(i.panelPosition.xy)*alignment.z);
            }

            half DeckFlutes(float2 metres)
            {
                float2 spacing=metres*float2(9.,9.);
                float2 cell=frac(spacing+.5)-.5;
                float angle=fmod(abs(floor(spacing.x+.5)+floor(spacing.y+.5)),2.)*2.-1.;
                float rib=length(float2((cell.x+angle*cell.y)*.52,(cell.x-angle*cell.y)*2.6));
                float aa=clamp(max(fwidth(spacing.x),fwidth(spacing.y)),.01,.15);
                return 1.-smoothstep(.23-aa,.26+aa,rib);
            }

            float3 ReliefNormal(float3 position,float3 normal,float height)
            {
                float3 dx=ddx(position),dy=ddy(position);
                float3 right=cross(dy,normal),up=cross(normal,dx);
                float area=dot(dx,right);
                float3 gradient=clamp(ddx(height),-.007,.007)*right+clamp(ddy(height),-.007,.007)*up;
                return SafeNormalize(normal*max(abs(area),.000001)-sign(area)*gradient);
            }

            half4 frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normal);
                half plate=1;
                if(_SurfaceDetail.w>.5)
                {
                    plate=clamp(PanelHeight(i),.45,2.32);
                    if(_DeckTread>.5 && i.panelNormal.y>.85)
                        plate=lerp(plate,plate*1.30,DeckFlutes(i.panelPosition.xz));
                    n=ReliefNormal(i.position,n,plate*_SurfaceDetail.z);
                }
                InputData lighting=(InputData)0;
                lighting.positionWS=i.position;
                lighting.positionCS=i.clip;
                lighting.normalWS=n;
                lighting.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.position));
                lighting.shadowCoord=TransformWorldToShadowCoord(i.position);
                lighting.vertexLighting=i.vertexLight;
                lighting.bakedGI=SampleSH(n)+(_Marine>.5 ? WaterIrradiance(i.position,n) : half3(.089,.10,.11));
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.clip);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                // Pressure enamel collects oxidation along vertical water flow.
                // Broad marks remain visible at expedition distances, fine scratches emerge up close.
                float2 skin=float2(dot(i.position,float3(.46,0.,.89)),i.position.y);
                float broad=HullNoise(skin*.62);
                float deposits=HullNoise(skin*float2(3.8,.31)+float2(12.8,47.));
                float streak=smoothstep(.52,.87,deposits)*smoothstep(.29,.68,broad);
                // Grime runs down in world space below seams and fasteners, inside and out.
                float runoff=HullNoise(float2(skin.x*5.1,skin.y*.42)+float2(71.,3.))*HullNoise(skin*1.3+float2(9.,21.));
                float grime=smoothstep(.30,.62,runoff)*(.55+.45*smoothstep(.25,.70,broad));
                half coat=lerp(.87,1.10,broad)-streak*(.075+.125*_Marine)-grime*.09;
                half projection=lerp(1.,plate,_SurfaceDetail.y);
                // Metre-scale patches of polished and chalked finish keep the plating from reading as one sheet.
                float polish=HullNoise(skin*.21+float2(5.3,2.1));
                half gloss=lerp(.35,.65,smoothstep(.22,.78,polish))+(_Smoothness-.40)*.6;
                // Worn rims: handling scuffs at grazing angles, and the raised lips and studs of the pressed panels.
                half grazing=1.-saturate(dot(n,lighting.viewDirectionWS));
                half scuff=smoothstep(.42,.84,HullNoise(skin*4.7+float2(31.,7.)));
                half relief=saturate((plate-1.)*2.4);
                half wear=saturate(grazing*grazing*grazing*scuff*1.5+relief*(.35+.65*scuff)*.9);
                half3 paint=_BaseColor.rgb*coat*projection + half3(.019,.022,.013)*streak*_Marine;
                // Safety ochre keeps its chroma: worn rims only brighten it, the grey chalking is for steel.
                half peakTint=max(_BaseColor.r,max(_BaseColor.g,_BaseColor.b));
                half chroma=saturate((peakTint-min(_BaseColor.r,min(_BaseColor.g,_BaseColor.b)))/max(peakTint,.01)*1.6);
                surface.albedo=lerp(paint,paint*1.45+half3(.028,.036,.040)*(1.-chroma),wear*.42);
                surface.metallic=saturate(_Metallic+wear*.35);
                surface.smoothness=saturate(gloss-streak*.10-grime*.07+(plate-1.)*.075+wear*.14);
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=1;
                half3 steel=UniversalFragmentPBR(lighting,surface).rgb;
                // Roll off reflections from close work lights while preserving their shape.
                half peak=max(steel.r,max(steel.g,steel.b));
                half specular=max(0.,peak-.69);
                if(specular>0.)steel*=(.69+specular/(1.+specular*.84))/peak;
                float3 delta=i.position-GetCameraPositionWS();
                float transmission=exp2(-dot(delta,delta)*_NereidReefClarity.x);
                half3 submerged=lerp(WaterColumnColor(-lighting.viewDirectionWS),steel,transmission);
                return half4(lerp(MixFog(steel,i.fog),submerged,_NereidTurbidity),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
