Shader "Submarine/SonarLayers"
{
    Properties
    {
        _Layer ("0 Map / 1 Sweep / 2 Submarine / 3 Obstacles",Float)=0
        _Aspect ("Aspect",Float)=1.6
        _Tint ("Color",Color)=(0.04,0.8,0.55,1)
        _RingCount ("Range rings",Float)=4
        _Crosshair ("Crosshair",Float)=1
        _ScanAngle ("Scan angle",Float)=0.61
        _TrailAngle ("Trail",Float)=0.7
        _SweepDirection ("Direction",Float)=-1
        _DotRadius ("Dot size",Float)=0.018
        _Heading ("Heading",Float)=0
        _HeadingArrow ("Heading arrow",Float)=1
        _MapRotation ("Heading-up chart rotation",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Layer,_Aspect,_RingCount,_Crosshair,_ScanAngle,_TrailAngle,_SweepDirection,_DotRadius,_Heading,_HeadingArrow,_EchoCount,_MapRotation;
                float4 _Echoes[64];
                float4 _EchoColors[64];
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
            float Stroke(float d,float width,float aa) { return 1-smoothstep(width,width+aa,abs(d)); }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=(input.uv-0.5)*2*float2(_Aspect,1);
                float r=length(p),aa=max(fwidth(r),0.0012);
                float inside=1-smoothstep(0.78,0.79,r);
                float alpha=0;
                float3 outputColor=_Tint.rgb;
                float chartCos=cos(_MapRotation),chartSin=sin(_MapRotation);
                if(_Layer<0.5)
                {
                    [unroll] for(int ring=1;ring<=8;ring++)
                        if(ring<=(int)_RingCount) alpha+=Stroke(r-ring*0.78/max(_RingCount,1),0.0012,aa)*0.7;
                    alpha+=Stroke(r-0.12,0.0012,aa)*0.6;
                    float2 chartP=float2(chartCos*p.x+chartSin*p.y,-chartSin*p.x+chartCos*p.y);
                    alpha+=(Stroke(chartP.x,0.0008,aa)+Stroke(chartP.y,0.0008,aa))*inside*0.45*_Crosshair;
                }
                else if(_Layer<1.5)
                {
                    float angle=atan2(p.y,p.x);
                    float behind=frac((_ScanAngle-angle)*_SweepDirection/6.2831853+1)*6.2831853;
                    float trail=1-smoothstep(0,max(_TrailAngle,0.01),behind);
                    float beam=exp(-pow(behind/0.009,2));
                    alpha=(trail*trail*0.20+beam)*inside*smoothstep(0.02,0.12,r);
                }
                else if(_Layer<2.5)
                {
                    alpha=1-smoothstep(_DotRadius*0.7,_DotRadius,r);
                    float2 dir=float2(cos(_Heading),sin(_Heading));
                    float2 q=float2(dot(p,dir),dot(p,float2(-dir.y,dir.x)));
                    float arrow=step(0.025,q.x)*step(q.x,0.11)*(1-smoothstep((0.11-q.x)*0.4,(0.11-q.x)*0.4+aa,abs(q.y)));
                    alpha=max(alpha,arrow*_HeadingArrow);
                }
                else
                {
                    // Clip contacts at the radar circle, never at the contact's center.
                    clip(0.78-r);
                    float3 premultiplied=0;
                    [loop] for(int i=0;i<min((int)_EchoCount,64);i++)
                    {
                        if(_Echoes[i].z<=0) continue;
                        float2 worldPoint=_Echoes[i].xy;
                        float2 chartPoint=float2(chartCos*worldPoint.x-chartSin*worldPoint.y,chartSin*worldPoint.x+chartCos*worldPoint.y);
                        float d=length(p-chartPoint);
                        float radius=_Echoes[i].w>0 ? _Echoes[i].w : _DotRadius;
                        float dot=1-smoothstep(radius*0.65,radius,d);
                        float halo=exp(-d*d/max(radius*radius*4,0.00001))*0.4;
                        if(_EchoColors[i].w>1.5 && _EchoColors[i].w<2.5)
                        {
                            dot=(1-smoothstep(radius-aa,radius+aa,d))*0.22+Stroke(d-radius,0.002,aa);
                            halo=0;
                        }
                        float a=saturate((dot+halo)*inside)*saturate(_Echoes[i].z);
                        premultiplied=_EchoColors[i].rgb*a+premultiplied*(1-a);
                        alpha=a+alpha*(1-a);
                    }
                    outputColor=alpha>0.00001 ? premultiplied/alpha : 0;
                }
                // No tick bezel, outer arcs or decorative perimeter exists in any layer.
                alpha*=1-smoothstep(0.79,0.80,r);
                return half4(outputColor,saturate(alpha)*_Tint.a);
            }
            ENDHLSL
        }
    }
}
