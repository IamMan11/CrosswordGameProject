Shader "UI/DimWithHole"
{
    Properties
    {
        _Color ("Tint", Color) = (0,0,0,1)
        _DimAlpha ("Dim Alpha", Range(0,1)) = 0.7
        _UseHole ("Use Hole", Float) = 0
        _HoleCenter ("Hole Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _HoleSize ("Hole HalfSize (UV)", Vector) = (0.15, 0.06, 0, 0)
        _HoleSoftness ("Hole Softness (UV)", Range(0,0.3)) = 0.02
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                half2 uv      : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _DimAlpha;
            float _UseHole;
            float4 _HoleCenter; // xy
            float4 _HoleSize;   // xy halfsize
            float _HoleSoftness;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // พื้นดำ
                fixed4 col = _Color;
                col.a = _DimAlpha;

                if (_UseHole > 0.5)
                {
                    float2 uv = i.uv;

                    // สร้างรู "สี่เหลี่ยมขอบมน" ด้วยการคำนวณ distance แบบ rounded-rect
                    float2 d = abs(uv - _HoleCenter.xy) - _HoleSize.xy;
                    float2 dmax = max(d, 0.0);
                    float dist = length(dmax); // 0 = ภายใน, >0 = ภายนอก

                    float m;
                    if (_HoleSoftness > 0.0001)
                        m = saturate(dist / _HoleSoftness);
                    else
                        m = dist > 0.0 ? 1.0 : 0.0;

                    // m=0 (ในรู) => โปร่งใส / m=1 => ทึบ
                    col.a *= m;
                }

                return col;
            }
            ENDCG
        }
    }
}
