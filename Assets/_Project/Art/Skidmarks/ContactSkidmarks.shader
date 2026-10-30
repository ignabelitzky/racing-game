Shader "Tumbler/Contact Skidmarks HDRP"
{
    Properties
    {
        _Lifetime("Lifetime", Float) = 25
        _FadeDuration("Fade duration", Float) = 8
        _Now("Current time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Lifetime;
                float _FadeDuration;
                float _Now;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 ageStrength : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 ageStrength : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.ageStrength = input.ageStrength;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float age = max(0, _Now - input.ageStrength.x);
                float fade = 1 - smoothstep(_Lifetime - _FadeDuration, _Lifetime, age);
                float edge = smoothstep(0, 0.18, input.uv.x) * (1 - smoothstep(0.82, 1, input.uv.x));
                float grooves = 0.72 + 0.28 * smoothstep(0.12, 0.4, abs(sin(input.uv.x * 31.416)));
                float grain = 0.86 + 0.14 * sin(input.uv.y * 47 + sin(input.uv.x * 37));
                // Black alpha blending gently darkens the actual road color, independent of exposure.
                return float4(0, 0, 0, saturate(input.ageStrength.y * edge * grooves * grain * fade));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
