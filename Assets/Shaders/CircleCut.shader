Shader "Hidden/Gallery/CircleCut"
{
    // Punches a circular alpha into the reference render before the 2D panels get it.
    // A UI Mask would do the same job, but masking makes UGUI draw a stencil copy of the
    // material, and edits to the real one then never show up on screen.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Softness ("Edge Softness", Range(0.001, 0.1)) = 0.01
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float d = distance(IN.uv, float2(0.5, 0.5));
                c.a *= 1.0 - smoothstep(0.5 - _Softness, 0.5, d);
                return c;
            }
            ENDHLSL
        }
    }
}
