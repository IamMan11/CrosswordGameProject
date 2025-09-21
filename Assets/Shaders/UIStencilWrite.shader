Shader "UI/StencilWrite"
{
    Properties {
        [HideInInspector]_StencilRef ("Stencil Ref", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace      // เขียนค่า stencil = Ref (ทำให้พื้นมืดไม่วาดในบริเวณนี้)
        }
        Cull Off Lighting Off ZWrite Off ZTest Always
        ColorMask 0          // ไม่วาดสี (โปร่งใสจริง ๆ)
        Pass { }
    }
}
