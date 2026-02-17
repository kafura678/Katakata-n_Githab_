Shader "UI/SuppressionRadialBlock"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _RedColor  ("Uncontrolled Color", Color) = (1,0,0,1)
        _BlueColor ("Controlled Color",   Color) = (0,0.6,1,1)

        _Progress  ("Suppression Progress (0-1)", Range(0,1)) = 0
        _Center    ("Invasion Center (UV)", Vector) = (0.5,0.5,0,0)

        _Blocks    ("Blocks Per Axis", Range(4,256)) = 64
        _HardEdge  ("Hard Edge (0=smooth,1=hard)", Range(0,1)) = 1
        _EdgeWidth ("Edge Softness", Range(0.0,0.2)) = 0.0

        _StepCount ("Progress Steps", Range(1,512)) = 128

        _TieBreak  ("Tie Break Amount", Range(0,0.05)) = 0.01
        _Seed      ("Noise Seed", Float) = 1

        _BorderNoiseAmount ("Border Noise Amount", Range(0,1)) = 0.4
        _BorderNoiseWidth  ("Border Noise Width", Range(0,0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
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

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _RedColor;
            fixed4 _BlueColor;

            float  _Progress;
            float2 _Center;

            float _Blocks;
            float _HardEdge;
            float _EdgeWidth;

            float _StepCount;
            float _TieBreak;
            float _Seed;

            float _BorderNoiseAmount;
            float _BorderNoiseWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Max4(float a, float b, float c, float d)
            {
                return max(max(a, b), max(c, d));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                float blocks = max(1.0, _Blocks);

                // アスペクト補正（ブロックを正方形に見せる）
                float aspect = _MainTex_TexelSize.z / max(1.0, _MainTex_TexelSize.w);

                float2 uvA = float2(i.uv.x * aspect, i.uv.y);
                float2 cA  = float2(_Center.x * aspect, _Center.y);

                // ブロック中心にスナップ
                float2 cellUV = floor(uvA * blocks);
                float2 uvQ = (cellUV + 0.5) / blocks;

                float2 cellC = floor(cA * blocks);
                float2 centerQ = (cellC + 0.5) / blocks;

                float dist = distance(uvQ, centerQ);

                // Progress=1で必ず全域に届く最大距離
                float d0 = distance(centerQ, float2(0.0, 0.0));
                float d1 = distance(centerQ, float2(aspect, 0.0));
                float d2 = distance(centerQ, float2(0.0, 1.0));
                float d3 = distance(centerQ, float2(aspect, 1.0));
                float maxDist = Max4(d0, d1, d2, d3);

                float nd = (maxDist > 1e-6) ? (dist / maxDist) : 0.0;

                // 同距離ブロックの順番付け
                float h = Hash21(cellUV + _Seed);
                nd += (h - 0.5) * _TieBreak;

                // 段階化
                float steps = max(1.0, _StepCount);
                float p = saturate(_Progress);
                p = floor(p * steps) / steps;

                float tSmooth = smoothstep(p - _EdgeWidth, p + _EdgeWidth, nd);
                float tHard   = step(p, nd);
                float t = lerp(tSmooth, tHard, saturate(_HardEdge));

                fixed4 col = lerp(_BlueColor, _RedColor, t);

                // ===== 境界黒ノイズ =====
                float border = abs(nd - p);

                if (border < _BorderNoiseWidth)
                {
                    if (h < _BorderNoiseAmount)
                    {
                        col.rgb = float3(0,0,0);
                    }
                }

                col.a *= baseCol.a;
                return col;
            }
            ENDCG
        }
    }
}