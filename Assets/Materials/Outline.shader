Shader "Blackout/Outline"
{
    Properties
    {
        _OutlineColor ("Highlight Color", Color) = (0.3, 0.9, 1, 1)
        _OutlineWidth ("(unused)", Float) = 0.025
        _Strength ("Strength", Range(0, 3)) = 1
        _PulseSpeed ("Pulse Speed", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "HighlightGlow"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One      // additive: adds light on top of the object
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _Strength;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = _WorldSpaceCameraPos - posWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Fresnel: edges facing away from the camera glow more.
                float facing  = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float rim     = pow(1.0 - facing, 2.0);
                float pulse   = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                return _OutlineColor * (0.15 + rim * 1.2) * pulse * _Strength;
            }
            ENDHLSL
        }
    }
}