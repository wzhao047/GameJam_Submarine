Shader "Submarine/UnderwaterFullscreen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Camera image", 2D) = "white" {}
        [Header(Overall)]
        _Intensity ("Effect Intensity", Range(0,1)) = 1
        [Header(Color Grading)]
        _Exposure ("Exposure (EV)", Range(-5,5)) = 0
        _Brightness ("Brightness", Range(-0.5,0.5)) = 0
        _Contrast ("Contrast", Range(0,2)) = 1
        _ContrastPivot ("Contrast Pivot", Range(0,1)) = 0.5
        _HueShift ("Hue Shift (degrees)", Range(-180,180)) = 0
        _Saturation ("Saturation", Range(0,2)) = 0.85
        _Temperature ("Temperature (cool - / warm +)", Range(-100,100)) = 0
        _WhiteBalanceTint ("Tint (green - / magenta +)", Range(-100,100)) = 0
        _Tint ("Water Tint", Color) = (0.8,0.96,1,1)
        _TintStrength ("Tint Strength", Range(0,1)) = 0.2
        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0,1)) = 0
        _VignetteColor ("Vignette Color", Color) = (0,0,0,1)
        _VignetteRadius ("Vignette Radius", Range(0,1.5)) = 0.65
        _VignetteSoftness ("Vignette Softness", Range(0.01,1.5)) = 0.65
        _VignetteRoundness ("Vignette Roundness (ellipse 0 / circle 1)", Range(0,1)) = 0
        _VignetteCenterX ("Vignette Center X", Range(0,1)) = 0.5
        _VignetteCenterY ("Vignette Center Y", Range(0,1)) = 0.5
        [Header(Noise)]
        _NoiseStrength ("Noise Strength", Range(0,0.3)) = 0.035
        _NoiseSize ("Noise Size (pixels)", Range(1,8)) = 1.5
        _NoiseFPS ("Noise Frames Per Second", Range(0,60)) = 18
        _NoiseContrast ("Noise Contrast", Range(1,8)) = 1
        _ColorNoise ("Color Noise", Range(0,1)) = 0
        [Header(Screen Structure)]
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0
        _ScanlineSpacing ("Scanline Spacing (pixels)", Range(2,16)) = 3
        _ScanlineSpeed ("Scanline Speed (pixels per second)", Range(-100,100)) = 0
        _PixelGridStrength ("Pixel Grid Strength", Range(0,1)) = 0
        _PixelCellSize ("Pixel Cell Size (pixels)", Range(3,18)) = 3
        _SubpixelStrength ("RGB Subpixel Strength", Range(0,1)) = 0
        [Header(Signal Interference)]
        _SignalJitterStrength ("Signal Jitter Strength (screen fraction)", Range(0,0.02)) = 0
        _SignalBandHeight ("Signal Band Height (pixels)", Range(1,128)) = 12
        _SignalJitterFPS ("Signal Jitter Frames Per Second", Range(0,60)) = 12
        _RGBSplit ("RGB Separation (pixels)", Range(0,8)) = 0
        [Header(Random Glitch)]
        _GlitchStrength ("Random Glitch Strength", Range(0,1)) = 0
        _GlitchInterval ("Glitch Trigger Interval (seconds)", Range(0.2,30)) = 6
        _GlitchChance ("Glitch Chance per Interval", Range(0,1)) = 0.6
        _GlitchDuration ("Random Glitch Duration (seconds)", Range(0.05,2)) = 0.25
        [Header(Collision Glitch)]
        _CollisionGlitchStrength ("Collision Glitch Strength", Range(0,1)) = 0.85
        _CollisionGlitchDuration ("Collision Glitch Duration (seconds)", Range(0,2)) = 0.35
        [Header(Glitch Appearance)]
        _GlitchTearStrength ("Glitch Horizontal Tear (screen fraction)", Range(0,0.1)) = 0.025
        _GlitchBlockStrength ("Glitch Block Interference", Range(0,1)) = 0.5
        _GlitchBandHeight ("Glitch Band Height (pixels)", Range(2,128)) = 24
        _GlitchRGBSplit ("Glitch RGB Separation (pixels)", Range(0,32)) = 8
        _GlitchFPS ("Glitch Frames Per Second", Range(1,60)) = 20
        _GlitchSeed ("Glitch Random Seed", Float) = 1
        [Toggle] _GlitchPreview ("Preview Continuous Random Glitch", Float) = 0
        [Header(Water Distortion)]
        _DistortionStrength ("Distortion Strength (screen fraction)", Range(0,0.05)) = 0.004
        _DistortionScale ("Distortion Scale", Range(0.1,15)) = 3
        _DistortionSpeed ("Distortion Speed", Range(0,3)) = 0.35
        [Header(Debug)]
        [Enum(Final,0,Original,1,Noise,2,Distortion,3,ColorGrading,4,VignetteMask,5,ScreenPattern,6)] _DebugMode ("Debug View", Float) = 0
        [HideInInspector] _EffectTime ("Effect Time", Float) = 0
        [HideInInspector] _CollisionGlitchElapsed ("Collision Glitch Elapsed", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "UnderwaterFullscreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Intensity, _NoiseStrength, _NoiseSize, _NoiseFPS;
            float _NoiseContrast, _ColorNoise;
            float _ScanlineStrength, _ScanlineSpacing, _ScanlineSpeed;
            float _PixelGridStrength, _PixelCellSize, _SubpixelStrength;
            float _SignalJitterStrength, _SignalBandHeight, _SignalJitterFPS, _RGBSplit;
            float _GlitchStrength, _GlitchInterval, _GlitchChance, _GlitchDuration;
            float _GlitchTearStrength, _GlitchBlockStrength, _GlitchBandHeight;
            float _GlitchRGBSplit, _GlitchFPS, _GlitchSeed, _GlitchPreview;
            float _CollisionGlitchStrength, _CollisionGlitchDuration, _CollisionGlitchElapsed;
            float _HueShift, _Saturation, _TintStrength;
            float _Exposure, _Brightness, _Contrast, _ContrastPivot;
            float _Temperature, _WhiteBalanceTint;
            float4 _Tint;
            float4 _VignetteColor;
            float _VignetteStrength, _VignetteRadius, _VignetteSoftness;
            float _VignetteRoundness, _VignetteCenterX, _VignetteCenterY;
            float _DistortionStrength, _DistortionScale, _DistortionSpeed;
            float _DebugMode, _EffectTime;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float SmoothNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell+float2(1,0)), f.x),
                            lerp(Hash(cell+float2(0,1)), Hash(cell+1), f.x), f.y);
            }
            float3 RGBToHSV(float3 c)
            {
                float4 k = float4(0,-1.0/3.0,2.0/3.0,-1);
                float4 p = lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
                float4 q = lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
                float d = q.x - min(q.w,q.y);
                return float3(abs(q.z+(q.w-q.y)/(6*d+1e-10)),d/(q.x+1e-10),q.x);
            }
            float3 HSVToRGB(float3 c)
            {
                float3 p = abs(frac(c.xxx+float3(0,2.0/3.0,1.0/3.0))*6-3);
                return c.z * lerp(float3(1,1,1),saturate(p-1),c.y);
            }
            float3 GradeColor(float3 color)
            {
                color = max(color,0) * exp2(_Exposure);
                float warm = _Temperature * 0.005;
                float magenta = _WhiteBalanceTint * 0.005;
                color *= exp2(float3(warm+magenta,-magenta,-warm+magenta));
                float3 hsv = RGBToHSV(color);
                hsv.x = frac(hsv.x + _HueShift/360.0);
                hsv.y = saturate(hsv.y*_Saturation);
                color = HSVToRGB(hsv);
                color *= lerp(float3(1,1,1),_Tint.rgb,_TintStrength);
                color = (color-_ContrastPivot)*_Contrast+_ContrastPivot+_Brightness;
                return max(color,0);
            }
            float VignetteMask(float2 uv,float aspect)
            {
                float2 position = (uv-float2(_VignetteCenterX,_VignetteCenterY))*2;
                position.x *= lerp(1.0,max(aspect,0.01),saturate(_VignetteRoundness));
                return smoothstep(_VignetteRadius,_VignetteRadius+max(_VignetteSoftness,0.001),length(position));
            }
            float3 ScreenMask(float2 pixel)
            {
                // Pixel units keep the screen texture independent of the scene camera's motion.
                float scanline = 0.5+0.5*cos(6.2831853*(pixel.y+_EffectTime*_ScanlineSpeed)/max(_ScanlineSpacing,2));
                float size = max(_PixelCellSize,3);
                float2 cell = frac(pixel/size);
                float2 borderDistance = min(cell,1-cell)*size;
                float grid = 1-smoothstep(0.25,0.75,min(borderDistance.x,borderDistance.y));
                float channel = floor(cell.x*3);
                float3 subpixel = float3(1-step(0.5,channel),
                                        step(0.5,channel)*(1-step(1.5,channel)),
                                        step(1.5,channel));
                return (1-_ScanlineStrength*scanline)*(1-_PixelGridStrength*grid)
                     * lerp(float3(1,1,1),subpixel,saturate(_SubpixelStrength));
            }
            float GlitchEnvelope()
            {
                if (_GlitchStrength <= 0) return 0;
                if (_GlitchPreview > 0.5) return 1;
                // Each time window gets a stable chance and a random burst start.
                // Keep the whole burst inside its window, leaving a quiet gap.
                float interval = max(_GlitchInterval,0.2);
                float window = floor(_EffectTime/interval);
                float localTime = _EffectTime-window*interval;
                float duration = min(max(_GlitchDuration,0.01),interval*0.8);
                float seed = _GlitchSeed*19.17;
                float trigger = 1-step(saturate(_GlitchChance),Hash(float2(window+seed,7.23)));
                float start = Hash(float2(window+seed,83.91))*(interval-duration);
                return trigger*step(start,localTime)*(1-step(start+duration,localTime));
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 pixel = uv*abs(_MainTex_TexelSize.zw);
                float3 original = tex2D(_MainTex, uv).rgb;
                float aspect = abs(_MainTex_TexelSize.z / max(_MainTex_TexelSize.w,1));
                float2 p = uv * float2(aspect,1) * _DistortionScale;
                float t = _EffectTime * _DistortionSpeed;
                float2 flow = float2(SmoothNoise(p+float2(t*0.41,t*0.27)),
                                    SmoothNoise(p+float2(-t*0.29,t*0.37)+29.3))*2-1;
                // Fade at the border so distortion cannot expose black edges.
                float2 edge = smoothstep(0,0.06,uv) * smoothstep(0,0.06,1-uv);
                float2 offset = flow * _DistortionStrength * edge.x * edge.y;
                offset.x /= max(aspect,0.01);
                float jitterFrame = floor(_EffectTime*_SignalJitterFPS);
                float band = floor(pixel.y/max(_SignalBandHeight,1));
                offset.x += (Hash(float2(band+47.3,jitterFrame+91.7))*2-1)
                          * _SignalJitterStrength*edge.x*edge.y;
                float collisionActive = step(0,_CollisionGlitchElapsed)
                                      * (1-step(max(_CollisionGlitchDuration,0),_CollisionGlitchElapsed));
                // The two timers are independent; overlapping bursts use the stronger effect.
                float glitch = max(GlitchEnvelope()*saturate(_GlitchStrength),
                                   collisionActive*saturate(_CollisionGlitchStrength));
                float glitchFrame = floor(_EffectTime*max(_GlitchFPS,1));
                float glitchBand = floor(pixel.y/max(_GlitchBandHeight,2));
                float glitchBandMask = 0;
                if (glitch > 0)
                {
                    glitchBandMask = step(0.6,Hash(float2(glitchBand+_GlitchSeed*7.13,glitchFrame+13.17)));
                    float tear = Hash(float2(glitchBand+_GlitchSeed*3.71,glitchFrame+71.9))*2-1;
                    offset.x += tear*_GlitchTearStrength*glitch*glitchBandMask*edge.x*edge.y;
                }
                float2 inset = abs(_MainTex_TexelSize.xy)*0.5;
                float2 distortedUV = clamp(uv+offset,inset,1-inset);
                float frame = floor(_EffectTime * _NoiseFPS);
                float2 noiseCell = floor(pixel/max(_NoiseSize,1))+float2(frame*13.37,frame*7.91);
                float grain = Hash(noiseCell);
                float3 rgbGrain = float3(grain,Hash(noiseCell+float2(17.13,81.7)),Hash(noiseCell+float2(63.9,24.41)));
                rgbGrain = lerp(grain.xxx,rgbGrain,saturate(_ColorNoise));
                rgbGrain = saturate((rgbGrain-0.5)*_NoiseContrast+0.5);
                float vignette = VignetteMask(uv,aspect);
                float3 screenMask = ScreenMask(pixel);
                if (_DebugMode > 5.5) return float4(screenMask,1);
                if (_DebugMode > 4.5) return float4(vignette.xxx,1);
                if (_DebugMode > 3.5) return float4(GradeColor(original),1);
                if (_DebugMode > 2.5) return float4(flow*0.5+0.5,0.5,1);
                if (_DebugMode > 1.5) return float4(rgbGrain,1);
                if (_DebugMode > 0.5) return float4(original,1);
                float3 sampleColor = tex2D(_MainTex,distortedUV).rgb;
                float rgbSplit = _RGBSplit+glitch*_GlitchRGBSplit*(0.25+0.75*glitchBandMask);
                if (rgbSplit > 0)
                {
                    float2 separation = float2(rgbSplit*abs(_MainTex_TexelSize.x),0);
                    sampleColor.r = tex2D(_MainTex,clamp(distortedUV+separation,inset,1-inset)).r;
                    sampleColor.b = tex2D(_MainTex,clamp(distortedUV-separation,inset,1-inset)).b;
                }
                float3 color = GradeColor(sampleColor);
                color = max(0,color + (rgbGrain-0.5)*_NoiseStrength);
                if (glitch > 0 && _GlitchBlockStrength > 0)
                {
                    float height = max(_GlitchBandHeight,2);
                    float2 block = floor(pixel/float2(height*4,height));
                    float2 blockSeed = block+float2(glitchFrame*11.3,_GlitchSeed*37.1);
                    float blockMask = step(0.8,Hash(blockSeed+14.7));
                    float3 blockColor = float3(Hash(blockSeed+3.1),Hash(blockSeed+28.7),Hash(blockSeed+63.9));
                    float staticNoise = Hash(floor(pixel)+float2(glitchFrame*7.3,_GlitchSeed*11.9));
                    float3 interference = lerp(blockColor,staticNoise.xxx,0.35);
                    color = lerp(color,interference,glitch*_GlitchBlockStrength*blockMask);
                }
                color *= screenMask;
                color = lerp(color,_VignetteColor.rgb,vignette*saturate(_VignetteStrength));
                return float4(lerp(original,color,saturate(_Intensity)),1);
            }
            ENDHLSL
        }
    }
}
