#ifndef NEREID_WATER_COLUMN_INCLUDED
#define NEREID_WATER_COLUMN_INCLUDED
float4 _NereidWaterTint;
// Rec.601-ish luminance of a scene-linear colour; shared so every water fade agrees.
half NereidLuminance(half3 c){return dot(c,half3(.30,.55,.15));}
// Depth II is recognised by its squared-distance extinction (Depth I .00136-.0023, Depth II .00625-.0094).
half NereidDeepWater(float extinction){return smoothstep(.003,.005,extinction);}
// Far water is a blue-grey column: the tint loses part of its chroma, darkens softly
// downward and lifts faintly upward. The sun halo and the upward lift exist only on Depth I.
half3 WaterColumnColor(float3 viewDirection,float extinction)
{
    half shallow=1-NereidDeepWater(extinction);
    half down=smoothstep(-.82,.05,viewDirection.y);
    half up=smoothstep(.05,.80,viewDirection.y)*shallow;
    half halo=pow(saturate(dot(viewDirection,normalize(float3(-.47,.86,-.20)))),9)*shallow;
    half3 tint=_NereidWaterTint.rgb;
    half3 neutral=NereidLuminance(tint);
    half3 grey=lerp(tint,neutral,.28);
    return grey*(lerp(.56,1.,down)+up*.30+halo*.30);
}
// Legacy form for shaders that do not read the clarity vector: Depth I behaviour.
half3 WaterColumnColor(float3 viewDirection){return WaterColumnColor(viewDirection,0);}
// Distance fade of a lit surface: chroma drops before luminance, then the remaining
// luminance settles into the column. d2 is the squared eye distance.
half3 NereidWaterFade(half3 close,float3 viewDirection,float d2,float extinction)
{
    half3 column=WaterColumnColor(viewDirection,extinction);
    half clarity=exp2(-d2*extinction);
    half chroma=exp2(-d2*extinction*2.6);
    half3 hue=column/max(NereidLuminance(column),.002);
    half3 greyed=lerp(NereidLuminance(close)*hue,close,chroma);
    return lerp(column,greyed,clarity);
}
#endif
