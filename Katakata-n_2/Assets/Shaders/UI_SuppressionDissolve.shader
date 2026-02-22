Shader "UI/SuppressionRadialBlock"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 制圧率（0=赤、1=青）
        _Suppression ("Suppression 0-1", Range(0,1)) = 0

        // 侵入口（UV 0-1）
        _Center ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)

        // ブロック粗さ（大きいほど細かい）
        _BlockCount ("Block Count", Range(4,128)) = 32

        // ランダム侵食
        _NoiseScale ("Noise Scale", Range(1,64)) = 16
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.25

        // 境界の黒
        _BorderWidth ("Border Width", Range(0,0.25)) = 0.03
        _BorderStrength ("Border Strength", Range(0,2)) = 1.0

        // 送信1回の「じわ」演出（0=無し、1=強）
        _Pulse ("Send Pulse", Range(0,1)) = 0

        // アルファ内だけ描画（通常はSpriteのアルファでOK）
        _AlphaCut ("Alpha Cut", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
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
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float _Suppression;
            float4 _Center;

            float _BlockCount;
            float _NoiseScale;
            float _NoiseStrength;

            float _BorderWidth;
            float _BorderStrength;

            float _Pulse;
            float _AlphaCut;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 0-1 hash
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv) * IN.color;

                // 透明は捨てる（クリック/見た目の安定）
                if (tex.a <= _AlphaCut) discard;

                // ブロック化UV
                float2 bc = max(4.0, _BlockCount).xx;
                float2 cell = floor(IN.uv * bc);
                float2 cellUV = (cell + 0.5) / bc;     // セル中心
                float2 local = frac(IN.uv * bc);       // セル内

                // 侵入口
                float2 c = _Center.xy;

                // 放射距離（セル中心から計算：ブロック単位で反転しやすい）
                float d = distance(cellUV, c);

                // 正規化距離（ざっくり。最大を sqrt(2) として 0-1へ）
                float dn = saturate(d / 1.41421356);

                // ランダム侵食：セルごとに乱数
                float n = hash21(cell * _NoiseScale);

                // Pulse でノイズ強度を増やす（送信演出）
                float noiseStr = saturate(_NoiseStrength + _Pulse * 0.35);
                float jitter = (n - 0.5) * noiseStr;

                // 侵食境界：Suppression が大きいほど「青」が増える
                // dn + jitter < _Suppression なら青、そうでなければ赤
                float t = dn + jitter;
                float fill = step(t, _Suppression);

                // 色（赤→青）
                float3 colR = float3(1,0,0);
                float3 colB = float3(0,0.55,1);
                float3 baseCol = lerp(colR, colB, fill);

                // 境界（黒）：
                // step境界付近（t ≒ _Suppression）を黒くする
                float bw = saturate(_BorderWidth + _Pulse * 0.02);
                float edge = 1.0 - smoothstep(0.0, bw, abs(t - _Suppression));
                float3 withBorder = lerp(baseCol, float3(0,0,0), edge * saturate(_BorderStrength));

                tex.rgb = withBorder;
                return tex;
            }
            ENDCG
        }
    }
}