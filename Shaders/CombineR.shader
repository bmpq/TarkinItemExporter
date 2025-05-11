Shader "Hidden/CombineR"
{
    Properties
    {
        _RTex ("R", 2D) = "white" {}
        _GTex ("G", 2D) = "white" {}
        _BTex ("B", 2D) = "white" {}
        _ATex ("A", 2D) = "white" {}
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
            
            sampler2D _RTex;
            sampler2D _GTex;
            sampler2D _BTex;
            sampler2D _ATex;

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(
                    tex2D(_RTex, i.uv).r, 
                    tex2D(_GTex, i.uv).r, 
                    tex2D(_BTex, i.uv).r, 
                    tex2D(_ATex, i.uv).r
                    );
            }
            ENDCG
        }
    }
}