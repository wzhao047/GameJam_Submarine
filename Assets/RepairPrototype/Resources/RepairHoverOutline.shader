Shader "Submarine/Repair Hover Outline"
{
    Properties { _OutlineWidth ("Outline width (pixels)", Range(1, 8)) = 3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "WhiteOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(positionWS);
                float4 tip = TransformWorldToHClip(positionWS + normalWS);
                float2 direction = tip.xy * clip.w - clip.xy * tip.w;
                float2 pixels = direction * _ScaledScreenParams.xy;
                float lengthPixels = length(pixels);
                if (lengthPixels > 0.00001)
                    clip.xy += (pixels / lengthPixels) * (2.0 * _OutlineWidth / _ScaledScreenParams.xy) * clip.w;
                output.positionCS = clip;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return half4(1, 1, 1, 1); }
            ENDHLSL
        }
    }
}