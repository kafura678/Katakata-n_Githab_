Shader "UI/SuppressionRadialBlock"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 制圧率（0〜1）
        _Suppression ("Suppression", Range(0,1)) = 0

        // 色
        _ColorA ("Low Color", Color) = (1,0,0,1)
        _ColorB ("High Color", Color) = (0,0.6,1,1)

        // 放射中心（UV）
        _Center ("Radial Center", Vector) = (0.5,0.5,0,0)

        // ブロック数（小さいほど荒い）
        _BlockCount ("Block Count", Range(4,128)) = 32

        // ランダム強度
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.15

        // 境界幅
        _EdgeWidth ("Edge Width", Range(0.001,0.2)) = 0.03

        // 境界色
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
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

            sampler2D _MainTex;
            fixed4 _Color;

            float _Suppression;
            fixed4 _ColorA;
            fixed4 _ColorB;
            float4 _Center;
            float _BlockCount;
            float _NoiseStrength;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // ブロック化
            float2 BlockUV(float2 uv, float blockCount)
            {
                return floor(uv * blockCount) / blockCount;
            }

            // ハッシュ（ランダム）
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;

                // ブロック化
                float2 buv = BlockUV(i.uv, _BlockCount);

                // 放射距離（正規化）
                float dist = distance(buv, _Center.xy);

                // UV空間最大距離（対角）
                float maxRadius = 1.41421356; // sqrt(2)

                float radial = saturate(dist / maxRadius);

                // ランダム加算
                float noise = Hash21(buv) * _NoiseStrength;

                float value = saturate(radial + noise);

                float threshold = saturate(_Suppression);

                // 塗り判定
                float fill = step(value, threshold);

                fixed4 baseCol = lerp(_ColorA, _ColorB, fill);

                // ===== 境界処理（安全版）=====
                float diff = abs(value - threshold);
                float edgeMask = step(diff, _EdgeWidth);

                baseCol = lerp(baseCol, _EdgeColor, edgeMask);

                baseCol.a *= tex.a;

                return baseCol;
            }
            ENDCG
        }
    }
}