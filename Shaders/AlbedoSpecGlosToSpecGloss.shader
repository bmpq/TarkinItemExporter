Shader "Hidden/AlbedoSpecGlosToSpecGloss"
{
    Properties
    {
        _AlbedoSpecTex ("Albedo (RGB) Specular (A)", 2D) = "white" {}
        _GlossinessTex ("Glossiness (R)", 2D) = "white" {}
    }
    SubShader
    {
        // No culling, no depth, always render
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _AlbedoSpecTex;
            sampler2D _GlossinessTex;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 albedoSpec = tex2D(_AlbedoSpecTex, i.uv);
                float4 gloss = tex2D(_GlossinessTex, i.uv);

                // Combine the two textures to create Specular-Glossiness map

                float specIntensity = albedoSpec.a;
                float glossiness = gloss.r; // Doesnt matter which channel, they are all the same

                return float4(specIntensity, specIntensity, specIntensity, glossiness);
            }
            ENDCG
        }
    }
}