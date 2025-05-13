Shader "Hidden/ChannelToGrayscale"
{
    Properties
    {
        _MainTex ("Input Texture", 2D) = "white" {}
        [Enum(R,0,G,1,B,2,A,3)] _ChannelSelect ("Source Channel", Int) = 0
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
            int _ChannelSelect; // Will be 0 for R, 1 for G, 2 for B, 3 for A

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 inputColor = tex2D(_MainTex, i.uv);
                fixed selectedChannelValue;

                if (_ChannelSelect == 0) // R
                {
                    selectedChannelValue = inputColor.r;
                }
                else if (_ChannelSelect == 1) // G
                {
                    selectedChannelValue = inputColor.g;
                }
                else if (_ChannelSelect == 2) // B
                {
                    selectedChannelValue = inputColor.b;
                }
                else // A (and fallback)
                {
                    selectedChannelValue = inputColor.a;
                }
                
                // Output: selected channel duplicated into R, G, B. Alpha is 1.0.
                return fixed4(selectedChannelValue, selectedChannelValue, selectedChannelValue, selectedChannelValue);
            }
            ENDCG
        }
    }
}