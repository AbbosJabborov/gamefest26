Shader "Custom/UI_DissolveFromClick"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex  ("Noise Texture", 2D) = "white" {}
        _Color     ("Tint", Color) = (0.08, 0.08, 0.08, 1)

        _DissolveOrigin   ("Dissolve Origin (UV)", Vector) = (0.5, 0.5, 0, 0)
        _DissolveProgress ("Dissolve Progress",    Range(0, 1)) = 0
        _EdgeWidth        ("Edge Glow Width",      Range(0, 0.12)) = 0.025
        _EdgeColor        ("Edge Glow Color",      Color) = (1, 0.4, 0.05, 1)

        // Required by Unity UI masking system
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil     ("Stencil ID",         Float) = 0
        _StencilOp   ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask   ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref      [_Stencil]
            Comp     [_StencilComp]
            Pass     [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                fixed4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float4 worldPosition: TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            fixed4    _Color;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;
            float4    _MainTex_ST;

            float2 _DissolveOrigin;
            float  _DissolveProgress;
            float  _EdgeWidth;
            fixed4 _EdgeColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(v.vertex);
                o.texcoord      = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color         = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // UI rect clipping (Mask component support)
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                // ── Dissolve logic ──────────────────────────────────────────
                float noise = tex2D(_NoiseTex, i.texcoord).r;

                // Radial distance from click origin in UV space
                float2 diff = i.texcoord - _DissolveOrigin;
                float  dist = length(diff);

                // Mix distance with noise for an organic, burning edge
                // dist: 0 at origin, ~1.42 at far corner → normalise to ~0-1
                float dissolveVal = dist * 0.68 + noise * 0.32;

                // Progress drives the threshold (goes slightly past 1 to clear corners)
                float threshold = _DissolveProgress * 1.55;

                // Discard fully dissolved pixels (below the edge band)
                clip(dissolveVal - (threshold - _EdgeWidth));

                // Glowing edge band: pixels between (threshold - edgeWidth) and threshold
                float edgeMask = step(dissolveVal, threshold); // 1 inside edge band, 0 above

                col = lerp(col, _EdgeColor, edgeMask * _EdgeColor.a);

                return col;
            }
            ENDCG
        }
    }
}
