Shader "Hidden/Invert"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        [Toggle(INVERT_ALPHA)] _InvertAlpha ("Invert Alpha Channel?", Float) = 1.0 // Default to ON (invert alpha)
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

            // this will create variants
            #pragma shader_feature INVERT_ALPHA

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
            
            sampler2D _MainTex;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed finalAlpha;

                #if INVERT_ALPHA
                    finalAlpha = 1.0 - col.a;
                #else
                    finalAlpha = col.a;
                #endif

                return float4(
                    1.0 - col.r, 
                    1.0 - col.g, 
                    1.0 - col.b, 
                    finalAlpha
                    );
            }
            ENDCG
        }
    }
}