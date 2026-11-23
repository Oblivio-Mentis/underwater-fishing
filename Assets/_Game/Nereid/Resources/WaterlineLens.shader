Shader "RAID/Waterline Lens"
{
    Properties
    {
        [HideInInspector] _Line("Waterline a,b,c,halfwidth in screen uv", Vector)=(0,1,-2,.02)
        [HideInInspector] _State("Submerged, rush, drying, aspect", Vector)=(0,0,0,1.78)
        _WaterTint("Water tint", Color)=(.14,.42,.47,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+120" "RenderType"="Transparent" }
        Pass
        {
            Name "Owned camera waterline"
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Line, _State, _WaterTint;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION; float4 screen:TEXCOORD0;};
            V vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.screen=ComputeScreenPos(o.positionCS);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float2 hash2(float2 p){return float2(hash(p),hash(p+19.19));}

            // Drops that form after the lens leaves the water: each grid cell may own one drop
            // that beads, slides down with a streak and dries away.
            float3 Drops(float2 uv,float drying,float aspect)
            {
                float3 result=0; // xy = refraction offset, z = highlight
                float2 grid=float2(9.*aspect,9.);
                for(int layer=0;layer<2;layer++)
                {
                    float2 g=grid*(1.+layer*.7);
                    float2 cell=floor(uv*g+layer*.37);
                    float2 rnd=hash2(cell+layer*41.);
                    if(rnd.x>.62)continue;
                    float born=rnd.y*.6;
                    float age=max(0,(1.-drying)*5.-born);
                    float slide=age*age*(.14+.3*rnd.x);
                    float2 center=(cell+float2(.2+.6*rnd.y,.75)+float2(0,-slide*g.y))/g;
                    center.x+=sin(age*7.+rnd.x*9.)*.004;
                    float2 d=(uv-center)*float2(aspect,1.);
                    float radius=(.009+.011*rnd.x)*saturate(1.-age*.16);
                    float rr=length(d);
                    float inside=1.-smoothstep(radius*.8,radius,rr);
                    float trail=(1.-smoothstep(0,radius*.45,abs(d.x)))*step(0,uv.y-center.y)*(1.-smoothstep(0,slide/g.y*.9+.001,uv.y-center.y))*.5;
                    result.xy+=d/max(rr,.0001)*(1.-rr/radius)*inside*.045+float2(0,trail*.004);
                    result.z+=inside*(.25+.6*smoothstep(radius*.55,radius*.95,rr))+trail*.08;
                }
                return result;
            }

            half4 frag(V i):SV_Target
            {
                float2 uv=i.screen.xy/i.screen.w;
                float t=_Time.y;
                float submerged=_State.x, rush=_State.y, drying=_State.z, aspect=_State.w;
                float s=_Line.x*uv.x+_Line.y*uv.y+_Line.z;   // >0: this pixel is under the water line
                float w=max(.004,_Line.w);
                float below=smoothstep(-w,w,s);
                float band=exp(-(s*s)/(w*w*2.2));
                float2 normal=normalize(_Line.xy+1e-5);

                // Meniscus: the water climbs the glass, bending the image along the line and
                // sitting slightly higher than the flat surface would.
                float2 offset=normal*band*.028*sin(uv.x*41.+t*9.)+normal*band*.02;
                // Under the line the image wobbles while the lens is still wet from the flood.
                float wobble=below*saturate(rush+.25)*.006;
                offset+=float2(sin(uv.y*23.+t*6.1),cos(uv.x*19.-t*5.3))*wobble;
                float3 drops=Drops(uv,drying,aspect)*step(.001,drying)*(1.-below);
                offset+=drops.xy;
                float3 scene=SampleSceneColor(saturate(uv+offset));

                // Flooding in: a dark band of water climbs, then air bubbles rush up past the eye.
                float3 wet=lerp(scene,scene*_WaterTint.rgb*2.4,.42*below);
                wet=lerp(wet,_WaterTint.rgb*.6,below*rush*.25);
                float3 color=wet;
                // The line itself: a bright lens edge with a faint dark shadow under it.
                color+=band*float3(.55,.68,.70)*.55;
                color-=smoothstep(0,w*3.,s)*(1.-smoothstep(w*3.,w*9.,s))*.08*(1.-submerged);
                color+=drops.z*.12*step(.001,drying);
                float alpha=saturate(max(below*(.55+.45*rush),band));
                // Fully submerged and settled: the sea look takes over, the lens goes clear.
                alpha*=1.-submerged*(1.-rush);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
