Shader "UI/RadialReveal"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}   // ✅ สำคัญ: ให้ UI มี _MainTex
        _Color   ("Color", Color) = (0,0,0,1)
        _Radius  ("Radius", Range(0,1.5)) = 0
        _Feather ("Feather", Range(0,0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            // รองรับระบบคลิป/มาส์กของ Unity UI
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile __ UNITY_UI_CLIP_RECT UNITY_UI_ALPHACLIP

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 pos    : SV_POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            UNITY_DECLARE_TEX2D(_MainTex);
            float4 _MainTex_ST;

            fixed4 _Color;
            float  _Radius;
            float  _Feather;

            float4 _ClipRect;        // จาก UnityUI.cginc

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ฐานตามสไปรต์ (รองรับกรณีมี Sprite/Mask)
                fixed4 baseCol = UNITY_SAMPLE_TEX2D(_MainTex, i.uv) * i.color;

                // วงกลมจากกึ่งกลางจอ (แก้อัตราส่วนไม่ให้วงรี)
                float2 p = i.uv - 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                p.x *= aspect;
                float d = length(p); // 0 = ใจกลาง

                // ขอบนุ่ม: ข้างในโปร่งใส, ข้างนอกทึบ
                float edge = smoothstep(_Radius - _Feather, _Radius + _Feather, d);

                // สีทึบดำคุมด้วย edge แล้วคูณกับ alpha ของสไปรต์ (ให้เข้ากับระบบ UI)
                fixed4 col = _Color;
                col.a *= edge;
                col.a *= baseCol.a;

                // รองรับ Clip Rect/Mask
                #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(i.worldPos, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
