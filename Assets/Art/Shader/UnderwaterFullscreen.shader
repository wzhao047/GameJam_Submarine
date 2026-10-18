Shader "Submarine/UnderwaterFullscreen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Camera image", 2D) = "white" {}
        _Intensity ("Effect Intensity", Range(0,1)) = 1
        _NoiseStrength ("Noise Strength", Range(0,0.3)) = 0.035
        _NoiseSize ("Noise Size (pixels)", Range(1,8)) = 1.5
        _NoiseFPS ("Noise Frames Per Second", Range(0,60)) = 18
        _HueShift ("Hue Shift (degrees)", Range(-180,180)) = 0
        _Saturation ("Saturation", Range(0,2)) = 0.85
        _Tint ("Water Tint", Color) = (0.8,0.96,1,1)
        _TintStrength ("Tint Strength", Range(0,1)) = 0.2
        _DistortionStrength ("Distortion Strength (screen fraction)", Range(0,0.05)) = 0.004
        _DistortionScale ("Distortion Scale", Range(0.1,15)) = 3
        _DistortionSpeed ("Distortion Speed", Range(0,3)) = 0.35
        [Enum(Final,0,Original,1,Noise,2,Distortion,3)] _DebugMode ("Debug View", Float) = 0
        [HideInInspector] _EffectTime ("Effect Time", Float) = 0
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
            float _HueShift, _Saturation, _TintStrength;
            float4 _Tint;
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
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
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
                float2 inset = abs(_MainTex_TexelSize.xy)*0.5;
                float2 distortedUV = clamp(uv+offset,inset,1-inset);
                float frame = floor(_EffectTime * _NoiseFPS);
                float grain = Hash(floor(uv*abs(_MainTex_TexelSize.zw)/max(_NoiseSize,1))
                                   + float2(frame*13.37,frame*7.91));
                if (_DebugMode > 2.5) return float4(flow*0.5+0.5,0.5,1);
                if (_DebugMode > 1.5) return float4(grain.xxx,1);
                if (_DebugMode > 0.5) return float4(original,1);
                float3 color = tex2D(_MainTex,distortedUV).rgb;
                float3 hsv = RGBToHSV(max(color,0));
                hsv.x = frac(hsv.x + _HueShift/360.0);
                hsv.y = saturate(hsv.y*_Saturation);
                color = HSVToRGB(hsv);
                color *= lerp(float3(1,1,1),_Tint.rgb,_TintStrength);
                color = max(0,color + (grain-0.5)*_NoiseStrength);
                return float4(lerp(original,color,_Intensity),1);
            }
            ENDHLSL
        }
    }
}
