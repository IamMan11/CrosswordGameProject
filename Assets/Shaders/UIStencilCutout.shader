Shader "UI/StencilCutout"
{
    Properties {
        _Color ("Tint", Color) = (0,0,0,0.8)
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector]_StencilRef ("Stencil Ref", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_StencilRef]
            Comp NotEqual     // วาดเฉพาะ “ส่วนที่ไม่ใช่รู”
            Pass Keep
        }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f    { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 col:COLOR; };
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv,_MainTex); o.col = v.color * _Color; return o; }
            fixed4 frag (v2f i) : SV_Target { fixed4 c = tex2D(_MainTex, i.uv) * i.col; return c; }
            ENDCG
        }
    }
}
