Shader "Hidden/WinterPack/TileOverlay"
{
    Properties
    {
        _MainTex ("Snow overlay", 2D) = "black" {}
        _BaseTex ("Original tile", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _BaseTex;
            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 snow = tex2D(_MainTex, input.uv);
                fixed4 original = tex2D(_BaseTex, input.uv);
                return fixed4(lerp(original.rgb, snow.rgb, snow.a), original.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
