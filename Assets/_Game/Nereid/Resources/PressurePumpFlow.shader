Shader "RAID/Pressure pump flow"
{
    Properties
    {
        [HideInInspector] _Transfer("Level and pump motion",Vector)=(0,0,0,0)
        [HideInInspector] _Outlet("Outlet in chamber",Vector)=(0,0,0,0)
        [HideInInspector] _Intake("Intake in chamber",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Transfer, _Outlet, _Intake;
            CBUFFER_END
            struct A { float3 shape:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float fog:TEXCOORD0; };
            float hash(float x) {return frac(sin(x*93.784)*3727.192);}
            V vert(A v)
            {
                float h=_Transfer.x, fill=_Transfer.y, time=_Time.y;
                float s=v.uv.x, seed=floor(s*34.0), spray=v.uv.y;
                float phase=frac(s+time*(1.4+.27*hash(seed+2.)));
                float angle=6.28318*hash(seed+11.);
                float2 axis=float2(cos(angle),sin(angle));
                float3 p;
                float shimmer=.5+.5*sin(s*29.-time*14.+v.shape.z*4.);
                half4 col;
                if(fill>0.)
                {
                    float f=_Transfer.z*s;
                    float zSpeed=_Transfer.w;
                    float beneath=saturate((h-_Outlet.y+.03)*4.);
                    float radius=lerp(.104,.068,s)*(1.+.08*sin(time*17.-s*29.));
                    float3 center=_Outlet.xyz+float3(0,-.35*f-3.9*f*f,-zSpeed*f);
                    center=lerp(center,_Outlet.xyz+float3(.035*sin(6.*s+time)*s,.12*s*s,-.8*s),beneath);
                    float2 normal=normalize(float2(zSpeed,-.35-7.8*f));
                    if(spray<.5)
                    {
                        p=center+float3(v.shape.x*radius,v.shape.z*radius*normal.x,v.shape.z*radius*normal.y);
                        // A braided, aerated core: bright threads twist down the jet and it whitens where it tears.
                        float twist=.5+.5*sin(s*38.-time*21.+v.shape.x*3.);
                        float stripe=pow(saturate(.5+.48*v.shape.x+.26*twist),3.);
                        float tear=smoothstep(.55,1.,s)*(.4+.6*shimmer);
                        col=half4(lerp(lerp(half3(.07,.27,.34),half3(.66,.9,.91),stripe),half3(.9,.97,.98),tear*.55),lerp(.86,.42,beneath));
                    }
                    else
                    {
                        float3 impact=float3(_Outlet.x,h,_Outlet.z-zSpeed*_Transfer.z);
                        float reach=(.26+.62*hash(seed+13.))*phase;
                        float3 drop=impact+float3(axis.x*reach,.028+4.*phase*(1.-phase)*(.12+.24*hash(seed+8.)),axis.y*reach);
                        float3 bubble=_Outlet.xyz+float3(axis.x*(.11+phase*.24),phase*min(.85,max(.18,h-_Outlet.y+.04)),-.18-.64*phase+axis.y*phase*.19);
                        float size=(.014+.026*hash(seed))*sin(phase*3.14159);
                        p=lerp(drop,bubble,beneath)+v.shape*float3(size,size*1.35,size);
                        // Submerged bubbles catch a glint on their upper shoulder. Keep the
                        // jet legible through the bulk water without adding a second emitter.
                        float sheen=saturate(.48+.42*v.shape.y+.26*v.shape.x);
                        col=half4(lerp(half3(.40,.73,.77),half3(.84,.96,.99),sheen),
                            lerp(.68,.43,beneath)*sin(phase*3.14159));
                    }
                }
                else
                {
                    float3 top=float3(_Intake.x,h-.04,_Intake.z-.61);
                    float3 bottom=float3(_Intake.x,min(_Intake.y,max(.064,h*.6)),_Intake.z);
                    if(spray<.5)
                    {
                        float radius=lerp(.057,.32,s*s);
                        float turn=s*7.-time*3.;
                        float2 ring=float2(v.shape.x*cos(turn)-v.shape.z*sin(turn),v.shape.x*sin(turn)+v.shape.z*cos(turn));
                        p=lerp(bottom,top,s)+float3(ring.x*radius,0,ring.y*radius);
                        float filament=saturate(.4+.7*v.shape.x);
                        col=half4(.12,.44,.49,(.05+.18*filament)*saturate((h+.12)*2.));
                    }
                    else
                    {
                        float u=1.-phase;
                        float spin=angle+phase*9.+time*.7;
                        float r=(.08+.46*u*u)*(.7+.32*hash(seed+1.));
                        float size=(.011+.014*hash(seed+17.))*sin(phase*3.14159);
                        p=lerp(bottom,top,u)+float3(r*cos(spin),-.026*sin(phase*3.14159),r*sin(spin))+size*v.shape;
                        float sheen=saturate(.48+.42*v.shape.y-.26*v.shape.x);
                        col=half4(lerp(half3(.35,.65,.67),half3(.78,.91,.92),sheen),(.24+.53*u)*sin(phase*3.14159));
                    }
                }
                V o;o.positionCS=TransformObjectToHClip(p);o.color=col;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(V i):SV_Target { return half4(MixFog(i.color.rgb,i.fog),i.color.a); }
            ENDHLSL
        }
    }
}
