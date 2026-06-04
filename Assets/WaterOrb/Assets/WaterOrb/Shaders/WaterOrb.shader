Shader "Custom/WaterOrb"
{
    Properties
    {
        // ── Fill & Wave ──────────────────────────────────────────────
        _FillAmount       ("Fill Amount",          Range(0, 1))    = 0.4
        _WaveAmplitude    ("Wave Amplitude",        Range(0, 0.15)) = 0.04
        _WaveFrequency    ("Wave Frequency",        Range(0, 20))   = 8.0
        _WaveSpeed        ("Wave Speed",            Range(0, 5))    = 1.2
        _WaveFrequency2   ("Wave Frequency 2",      Range(0, 30))   = 14.0
        _WaveSpeed2       ("Wave Speed 2",          Range(0, 5))    = 0.85

        // ── Water Colors ─────────────────────────────────────────────
        _WaterColorTop    ("Water Color Top",       Color)          = (0.62, 0.57, 0.94, 0.75)
        _WaterColorMid    ("Water Color Mid",       Color)          = (0.47, 0.39, 0.86, 0.85)
        _WaterColorBot    ("Water Color Bottom",    Color)          = (0.27, 0.22, 0.67, 0.95)
        _FoamColor        ("Foam / Surface Color",  Color)          = (0.87, 0.84, 1.0,  0.5)
        _FoamThickness    ("Foam Thickness",        Range(0, 0.05)) = 0.012

        // ── Interior Background ──────────────────────────────────────
        _BgColorInner     ("BG Color Inner",        Color)          = (0.10, 0.09, 0.25, 1.0)
        _BgColorOuter     ("BG Color Outer",        Color)          = (0.04, 0.04, 0.13, 1.0)

        // ── Blob Shape ───────────────────────────────────────────────
        _BlobRadius       ("Blob Radius",           Range(0.1, 0.6))= 0.38
        _BlobNoise1       ("Blob Distort Freq1",    Range(0, 10))   = 3.0
        _BlobNoise1Amp    ("Blob Distort Amp1",     Range(0, 0.1))  = 0.045
        _BlobNoise2       ("Blob Distort Freq2",    Range(0, 15))   = 5.0
        _BlobNoise2Amp    ("Blob Distort Amp2",     Range(0, 0.1))  = 0.03

        // ── Glow & Rim ───────────────────────────────────────────────
        _RimColor         ("Rim / Glow Color",      Color)          = (0.74, 0.71, 1.0, 1.0)
        _RimWidth         ("Rim Width",             Range(0, 0.04)) = 0.012
        _GlowColor        ("Outer Glow Color",      Color)          = (0.47, 0.43, 0.90, 1.0)
        _GlowRadius       ("Outer Glow Radius",     Range(0, 0.5))  = 0.18
        _GlowIntensity    ("Outer Glow Intensity",  Range(0, 3))    = 1.2

        // ── Stars ────────────────────────────────────────────────────
        _StarCount        ("Star Count (approx)",   Range(0, 30))   = 14
        _StarBrightness   ("Star Brightness",       Range(0, 1))    = 0.75
        _StarTwinkleSpeed ("Star Twinkle Speed",    Range(0, 5))    = 1.1

        // ── Inner Glint ──────────────────────────────────────────────
        _GlintIntensity   ("Inner Glint Intensity", Range(0, 1))    = 0.18
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ── Uniforms ─────────────────────────────────────────────
            float  _FillAmount;
            float  _WaveAmplitude;
            float  _WaveFrequency;
            float  _WaveSpeed;
            float  _WaveFrequency2;
            float  _WaveSpeed2;

            float4 _WaterColorTop;
            float4 _WaterColorMid;
            float4 _WaterColorBot;
            float4 _FoamColor;
            float  _FoamThickness;

            float4 _BgColorInner;
            float4 _BgColorOuter;

            float  _BlobRadius;
            float  _BlobNoise1;
            float  _BlobNoise1Amp;
            float  _BlobNoise2;
            float  _BlobNoise2Amp;

            float4 _RimColor;
            float  _RimWidth;
            float4 _GlowColor;
            float  _GlowRadius;
            float  _GlowIntensity;

            float  _StarCount;
            float  _StarBrightness;
            float  _StarTwinkleSpeed;
            float  _GlintIntensity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv * 2.0 - 1.0; // remap to [-1,1]
                return o;
            }

            // ── Helpers ───────────────────────────────────────────────
            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // Soft blob distance — returns signed dist from blob edge
            float blobDist(float2 uv)
            {
                float angle = atan2(uv.y, uv.x);
                float distort = 1.0
                    + _BlobNoise1Amp * sin(angle * _BlobNoise1 + 0.8)
                    + _BlobNoise2Amp * sin(angle * _BlobNoise2 + 1.2);
                float r = _BlobRadius * distort;
                return length(uv) - r;
            }

            // Stars: scatter N points, each a soft disk
            float stars(float2 uv, float time)
            {
                float result = 0.0;
                int n = (int)clamp(_StarCount, 0, 30);
                for (int i = 0; i < n; i++)
                {
                    float2 seed = float2(float(i) * 0.137, float(i) * 0.271);
                    float2 pos;
                    pos.x = (hash(seed) * 2.0 - 1.0) * _BlobRadius * 0.85;
                    pos.y = (hash(seed + 0.5) * 2.0 - 1.0) * _BlobRadius * 0.85;
                    float radius = 0.004 + hash(seed + 1.0) * 0.007;
                    float phase  = hash(seed + 2.0) * 6.2832;
                    float twinkle = 0.4 + 0.6 * sin(time * _StarTwinkleSpeed + phase);
                    float d = length(uv - pos);
                    result += twinkle * smoothstep(radius, 0.0, d);
                }
                return saturate(result) * _StarBrightness;
            }

            // ── Fragment ──────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;           // [-1,1]
                float  time = _Time.y;

                // ── Blob SDF ──────────────────────────────────────────
                float bd = blobDist(uv);

                // Outer glow (outside blob)
                float glow = exp(-max(bd, 0.0) / _GlowRadius) * _GlowIntensity;
                float4 glowCol = _GlowColor * glow;
                glowCol.a = saturate(glow * 0.55);

                // Inside blob?
                if (bd > 0.0)
                {
                    // Only glow outside
                    return glowCol;
                }

                // ── Interior background ───────────────────────────────
                float bgT = length(uv) / _BlobRadius;
                float4 bg = lerp(_BgColorInner, _BgColorOuter, saturate(bgT));

                // ── Wave surface ──────────────────────────────────────
                // map uv.y: -_BlobRadius..+_BlobRadius → 0..1 (bottom to top)
                float normY = (uv.y + _BlobRadius) / (2.0 * _BlobRadius);
                float fillLine = _FillAmount; // 0 = empty, 1 = full

                // Wave 1
                float w1 = _WaveAmplitude * sin(uv.x * _WaveFrequency + time * _WaveSpeed);
                // Wave 2
                float w2 = _WaveAmplitude * 0.45 * sin(uv.x * _WaveFrequency2 - time * _WaveSpeed2);
                float waveSurface = fillLine + (w1 + w2) / (2.0 * _BlobRadius);

                float belowWater = step(normY, waveSurface);

                // Water color gradient (bottom → top of water volume)
                float waterDepth = saturate((waveSurface - normY) / fillLine);
                float4 waterCol = lerp(
                    lerp(_WaterColorTop, _WaterColorMid, saturate(waterDepth * 2.0)),
                    _WaterColorBot,
                    saturate((waterDepth - 0.5) * 2.0)
                );

                // Foam at wave surface
                float foamMask = smoothstep(_FoamThickness, 0.0, abs(normY - waveSurface));
                float4 foam = _FoamColor * foamMask;

                // ── Stars (above waterline) ───────────────────────────
                float starMask = stars(uv, time) * (1.0 - belowWater);
                float4 starCol = float4(0.87, 0.84, 1.0, starMask);

                // ── Inner glint (top-left highlight) ──────────────────
                float2 glintCenter = float2(-_BlobRadius * 0.38, _BlobRadius * 0.45);
                float glintD = length(uv - glintCenter) / (_BlobRadius * 0.22);
                float glint = exp(-glintD * glintD * 3.0) * _GlintIntensity;
                float4 glintCol = float4(1, 1, 1, glint);

                // ── Rim ───────────────────────────────────────────────
                float rimMask = smoothstep(0.0, _RimWidth, -bd) * (1.0 - smoothstep(_RimWidth, _RimWidth * 2.5, -bd));
                float4 rim = _RimColor * rimMask;

                // ── Composite ─────────────────────────────────────────
                float4 col = bg;

                // Water fill
                col = lerp(col, waterCol, belowWater * waterCol.a);

                // Foam
                col = lerp(col, foam, foam.a);

                // Stars
                col = lerp(col, starCol, starCol.a * 0.9);

                // Glint
                col.rgb += glintCol.rgb * glintCol.a;

                // Rim
                col = lerp(col, rim, rim.a * _RimColor.a);

                // Add outer glow contribution on edge
                col.rgb += glowCol.rgb * 0.15;

                // Soft edge fade
                float edgeFade = smoothstep(0.0, 0.025, -bd);
                col.a = edgeFade;

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
