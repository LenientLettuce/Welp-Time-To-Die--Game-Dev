Shader "Welp Time To Die/Combat Ghost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GhostFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 GhostFragment(v2f input) : SV_Target
            {
                fixed4 sprite = SampleSpriteTexture(input.texcoord);
                fixed detail = lerp(0.72, 1.0, dot(sprite.rgb, fixed3(0.299, 0.587, 0.114)));
                fixed alpha = sprite.a * input.color.a;
                return fixed4(input.color.rgb * detail * alpha, alpha);
            }
            ENDCG
        }
    }
}
