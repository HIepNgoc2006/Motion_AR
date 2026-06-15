// ═══════════════════════════════════════════════════════════════════════════
// URP Outline Shader — Motion AR Project
// Single-pass outline effect using vertex extrusion along normals.
// Compatible with Universal Render Pipeline (URP) 14.x.
//
// Usage:
//   1. Create a Material using this shader
//   2. Assign to a second material slot on the target MeshRenderer
//   3. Or use ObjectHighlight.cs to apply dynamically via MaterialPropertyBlock
//
// Properties:
//   _OutlineColor:  Outline color (default: Cyan #00FFFF)
//   _OutlineWidth:  Outline thickness in object-space units
//   _PulseSpeed:    Pulse animation speed (0 = static, >0 = pulsing)
//   _PulseMin:      Minimum width during pulse
// ═══════════════════════════════════════════════════════════════════════════

Shader "MotionAR/OutlineShader"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.015
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3.0
        _PulseMin ("Pulse Minimum Width", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        // ─────────────── Pass 1: Outline (back-face extrusion) ───────────────
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front       // Render back faces only → creates outline ring
            ZWrite On
            ZTest LEqual
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  fogFactor  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                float  _OutlineWidth;
                float  _PulseSpeed;
                float  _PulseMin;
            CBUFFER_END

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Pulse animation: modulate outline width over time.
                float pulse = 1.0;
                if (_PulseSpeed > 0.01)
                {
                    pulse = lerp(_PulseMin, 1.0, (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5));
                }

                float width = _OutlineWidth * pulse;

                // Extrude vertex along its normal in object space.
                float3 extrudedOS = input.positionOS.xyz + input.normalOS * width;

                output.positionCS = TransformObjectToHClip(extrudedOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                half4 color = _OutlineColor;

                // Apply fog.
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
