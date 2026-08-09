Shader "QuizBattle/GlowAdditive"
{
    // Unlit additive glow: a UV-based radial falloff stands in for a soft particle
    // texture, so rings/beams/particles need no texture asset. Vertex color (from mesh
    // vertex colors or a ParticleSystemRenderer's color stream) tints and fades per-instance.
    Properties
    {
        _TintColor("Tint Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 10)) = 1
        _SoftEdge("Soft Edge", Range(0.01, 1)) = 0.5
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 0
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Intensity;
                half _SoftEdge;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half dist = length(input.uv - 0.5) * 2.0;
                half falloff = 1.0 - smoothstep(1.0 - _SoftEdge, 1.0, dist);

                // _PulseAmount defaults to 0, so this is a no-op for every non-pulsing
                // use (HP bars, ground discs, ember/halo accents) regardless of speed.
                half pulse = 1.0 - _PulseAmount * 0.5 * (1.0 + sin(_Time.y * _PulseSpeed));

                half3 rgb = _TintColor.rgb * input.color.rgb * _Intensity * pulse;
                half a = _TintColor.a * input.color.a * falloff * pulse;
                return half4(rgb * a, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
