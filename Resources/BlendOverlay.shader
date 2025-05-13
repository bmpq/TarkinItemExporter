Shader "Hidden/BlendOverlay"
{
    Properties
    {
        _MainTex ("Base (Backdrop)", 2D) = "white" {}
        _TopTex ("Overlay (Source)", 2D) = "grey" {}
        _MaskTex ("Mask (R Channel)", 2D) = "white" {}
        _Factor ("Overlay Intensity", Range(0.0, 1.0)) = 1.0
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

            sampler2D _MainTex;
            sampler2D _TopTex;
            sampler2D _MaskTex;
            float _Factor;

            fixed3 overlay_rgb(fixed3 base, fixed3 blend)
            {
                float3 m = step(0.5, base);
                return (1.0 - m) * (2.0 * base * blend) + m * (1.0 - 2.0 * (1.0 - base) * (1.0 - blend));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 topCol = tex2D(_TopTex, i.uv);
                fixed4 maskCol = tex2D(_MaskTex, i.uv);

                fixed3 overlayResult = overlay_rgb(baseCol.rgb, topCol.rgb);

                fixed maskValue = maskCol.r;
                float blendFactor = _Factor * maskValue;

                fixed3 finalRGB = lerp(baseCol.rgb, overlayResult, blendFactor);

                fixed finalAlpha = baseCol.a;

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
}