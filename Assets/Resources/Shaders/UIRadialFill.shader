// Draws a texture clipped to a clockwise radial fill (0 = empty, 1 = full), starting at 12 o'clock.
// Used by the HUD for the art kit's power cooldown rings (Ring_Cooldown masked by a conic gradient).
Shader "BorrowGravity/UIRadialFill"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Fill ("Fill", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _Fill;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float t = frac(atan2(d.x, d.y) / 6.2831853 + 1.0);
                if (t > _Fill) discard;
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
