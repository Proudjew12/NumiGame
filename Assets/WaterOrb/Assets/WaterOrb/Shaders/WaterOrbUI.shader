Shader "Custom/WaterOrbUI"
{
    Properties
    {
        _MainTex              ("Main Tex (UI)",           2D)             = "white" {}
        _Color                ("Tint",                    Color)          = (1,1,1,1)

        // ── Illustration Texture ─────────────────────────────────────
        _IllustrationTex      ("Illustration Texture",    2D)             = "black" {}
        _IllustrationScale    ("Illustration Scale",      Range(0.1,3))   = 1.0
        _IllustrationOffsetX  ("Illustration Offset X",   Range(-1,1))    = 0.0
        _IllustrationOffsetY  ("Illustration Offset Y",   Range(-1,1))    = 0.0
        _IllustrationOpacity  ("Illustration Opacity",    Range(0,1))     = 1.0
        // 0 = always visible, 1 = rises with water (illustration sits on fill surface)
        _IllustrationRiseWithWater ("Rise With Water",    Range(0,1))     = 1.0

        // ── Fill & Wave ──────────────────────────────────────────────
        _FillAmount           ("Fill Amount",             Range(0,1))     = 0.4
        _WaveAmplitude        ("Wave Amplitude",          Range(0,0.15))  = 0.04
        _WaveFrequency        ("Wave Frequency",          Range(0,20))    = 8.0
        _WaveSpeed            ("Wave Speed",              Range(0,5))     = 1.2
        _WaveFrequency2       ("Wave Frequency 2",        Range(0,30))    = 14.0
        _WaveSpeed2           ("Wave Speed 2",            Range(0,5))     = 0.85

        // ── Water Colors ─────────────────────────────────────────────
        _WaterColorTop        ("Water Color Top",         Color)          = (0.62,0.57,0.94,0.75)
        _WaterColorMid        ("Water Color Mid",         Color)          = (0.47,0.39,0.86,0.85)
        _WaterColorBot        ("Water Color Bottom",      Color)          = (0.27,0.22,0.67,0.95)
        _FoamColor            ("Foam Color",              Color)          = (0.87,0.84,1.0,0.5)
        _FoamThickness        ("Foam Thickness",          Range(0,0.05))  = 0.012

        // ── Interior Background ──────────────────────────────────────
        _BgColorInner         ("BG Color Inner",          Color)          = (0.10,0.09,0.25,1)
        _BgColorOuter         ("BG Color Outer",          Color)          = (0.04,0.04,0.13,1)

        // ── Blob Shape ───────────────────────────────────────────────
        _BlobRadius           ("Blob Radius",             Range(0.1,0.6)) = 0.38
        _BlobNoise1           ("Blob Distort Freq1",      Range(0,10))    = 3.0
        _BlobNoise1Amp        ("Blob Distort Amp1",       Range(0,0.1))   = 0.045
        _BlobNoise2           ("Blob Distort Freq2",      Range(0,15))    = 5.0
        _BlobNoise2Amp        ("Blob Distort Amp2",       Range(0,0.1))   = 0.03

        // ── Glow & Rim ───────────────────────────────────────────────
        _RimColor             ("Rim Color",               Color)          = (0.74,0.71,1.0,1)
        _RimWidth             ("Rim Width",               Range(0,0.04))  = 0.012
        _GlowColor            ("Outer Glow Color",        Color)          = (0.47,0.43,0.90,1)
        _GlowRadius           ("Outer Glow Radius",       Range(0,0.5))   = 0.18
        _GlowIntensity        ("Outer Glow Intensity",    Range(0,3))     = 1.2

        // ── Stars ────────────────────────────────────────────────────
        _StarCount            ("Star Count",              Range(0,30))    = 14
        _StarBrightness       ("Star Brightness",         Range(0,1))     = 0.75
        _StarTwinkleSpeed     ("Star Twinkle Speed",      Range(0,5))     = 1.1

        // ── Inner Glint ──────────────────────────────────────────────
        _GlintIntensity       ("Inner Glint Intensity",   Range(0,1))     = 0.18

        _StencilComp          ("Stencil Comparison",      Float)          = 8
        _Stencil              ("Stencil ID",              Float)          = 0
        _StencilOp            ("Stencil Operation",       Float)          = 0
        _StencilWriteMask     ("Stencil Write Mask",      Float)          = 255
        _StencilReadMask      ("Stencil Read Mask",       Float)          = 255
        _ColorMask            ("Color Mask",              Float)          = 15
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
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            sampler2D _IllustrationTex;
            fixed4    _Color;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;

            float  _IllustrationScale;
            float  _IllustrationOffsetX;
            float  _IllustrationOffsetY;
            float  _IllustrationOpacity;
            float  _IllustrationRiseWithWater;

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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float2 uv       : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.worldPos = v.vertex;
                o.color    = v.color * _Color;
                o.uv       = v.texcoord * 2.0 - 1.0;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float blobDist(float2 uv)
            {
                float angle   = atan2(uv.y, uv.x);
                float distort = 1.0
                    + _BlobNoise1Amp * sin(angle * _BlobNoise1 + 0.8)
                    + _BlobNoise2Amp * sin(angle * _BlobNoise2 + 1.2);
                return length(uv) - _BlobRadius * distort;
            }

            float stars(float2 uv, float time)
            {
                float result = 0.0;
                int n = (int)clamp(_StarCount, 0, 30);
                for (int i = 0; i < n; i++)
                {
                    float2 seed   = float2(float(i) * 0.137, float(i) * 0.271);
                    float2 pos;
                    pos.x         = (hash(seed)       * 2.0 - 1.0) * _BlobRadius * 0.82;
                    pos.y         = (hash(seed + 0.5)  * 2.0 - 1.0) * _BlobRadius * 0.82;
                    float radius  = 0.004 + hash(seed + 1.0) * 0.007;
                    float phase   = hash(seed + 2.0) * 6.2832;
                    float twinkle = 0.4 + 0.6 * sin(time * _StarTwinkleSpeed + phase);
                    float d       = length(uv - pos);
                    result       += twinkle * smoothstep(radius, 0.0, d);
                }
                return saturate(result) * _StarBrightness;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv   = i.uv;
                float  time = _Time.y;

                // ── Blob SDF ──────────────────────────────────────────
                float bd = blobDist(uv);

                float glow   = exp(-max(bd, 0.0) / _GlowRadius) * _GlowIntensity;
                float4 glowC = _GlowColor * glow;
                glowC.a      = saturate(glow * 0.55);

                if (bd > 0.0)
                {
                    #ifdef UNITY_UI_CLIP_RECT
                    glowC.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                    #endif
                    #ifdef UNITY_UI_ALPHACLIP
                    clip(glowC.a - 0.001);
                    #endif
                    return glowC * i.color;
                }

                // ── Wave surface ──────────────────────────────────────
                float normY    = (uv.y + _BlobRadius) / (2.0 * _BlobRadius);
                float w1       = _WaveAmplitude * sin(uv.x * _WaveFrequency  + time * _WaveSpeed);
                float w2       = _WaveAmplitude * 0.45 * sin(uv.x * _WaveFrequency2 - time * _WaveSpeed2);
                float waveSurf = _FillAmount + (w1 + w2) / (2.0 * _BlobRadius);
                float belowWater = step(normY, waveSurf);
                float aboveWater = 1.0 - belowWater;

                // ── Interior background — gradient outside→inside, above water only ──
                // distFromEdge: 0 at blob edge, 1 at centre
                float distFromEdge = 1.0 - saturate((-bd) / _BlobRadius);
                // Only apply gradient in the above-water region; below water is filled
                float4 bg = lerp(_BgColorInner, _BgColorOuter, distFromEdge);

                float  waterDepth = saturate((waveSurf - normY) / max(_FillAmount, 0.001));
                float4 waterCol   = lerp(
                    lerp(_WaterColorTop, _WaterColorMid, saturate(waterDepth * 2.0)),
                    _WaterColorBot,
                    saturate((waterDepth - 0.5) * 2.0)
                );

                float  foamMask = smoothstep(_FoamThickness, 0.0, abs(normY - waveSurf));
                float4 foam     = _FoamColor * foamMask;

                // ── Stars — only above waterline ──────────────────────
                float  starMask = stars(uv, time) * aboveWater;
                float4 starCol  = float4(0.87, 0.84, 1.0, starMask);

                // ── Glint ─────────────────────────────────────────────
                float2 glintCenter = float2(-_BlobRadius * 0.38, _BlobRadius * 0.45);
                float  glintD      = length(uv - glintCenter) / (_BlobRadius * 0.22);
                // Glint only shows above water too
                float  glint       = exp(-glintD * glintD * 3.0) * _GlintIntensity * aboveWater;

                // ── Rim ───────────────────────────────────────────────
                float  rimMask = smoothstep(0.0, _RimWidth, -bd)
                               * (1.0 - smoothstep(_RimWidth, _RimWidth * 2.5, -bd));
                float4 rim     = _RimColor * rimMask;

                // ── Composite base ────────────────────────────────────
                float4 col = bg;
                col        = lerp(col, waterCol,  belowWater * waterCol.a);
                col        = lerp(col, foam,       foam.a);
                col        = lerp(col, starCol,    starCol.a * 0.9);
                col.rgb   += glint;
                col        = lerp(col, rim,        rim.a * _RimColor.a);
                col.rgb   += glowC.rgb * 0.15;

                // ── Illustration texture ──────────────────────────────
                // Centre of illustration follows the water surface when RiseWithWater=1
                // waterSurfaceY in [-1,1] uv space:
                float surfaceY   = waveSurf * 2.0 * _BlobRadius - _BlobRadius;
                float centreY    = lerp(0.0, surfaceY, _IllustrationRiseWithWater);

                // Remap uv relative to illustration centre, apply scale & offset
                float2 illUV = uv;
                illUV.y     -= centreY;
                illUV        = illUV / (_BlobRadius * 2.0 * _IllustrationScale) + 0.5;
                illUV.x     += _IllustrationOffsetX;
                illUV.y     += _IllustrationOffsetY;

                float4 illSample = tex2D(_IllustrationTex, illUV);

                // Brightness of pixel drives base alpha (white lines on black)
                float illLuma = dot(illSample.rgb, float3(0.299, 0.587, 0.114));

                // How much water has risen OVER this specific pixel's Y position
                // normY = this pixel's vertical position (0=bottom, 1=top)
                // waveSurf = water surface position
                // When water is above the pixel: coverAmount approaches 1
                float coverAmount = saturate((waveSurf - normY) / 0.25);

                // Glow brightness: dim when dry, bright when submerged/reached by water
                // Peaks at the wave surface for a "lit up as water touches it" effect
                float distToSurface = abs(normY - waveSurf);
                float surfaceGlow   = exp(-distToSurface / 0.08) * 2.5; // bright halo at surface
                float brightness    = saturate(coverAmount + surfaceGlow) * _IllustrationOpacity;

                float illAlpha = illLuma * illSample.a * brightness;

                // Color shifts from dim cool to bright white-blue as water covers it
                float3 dimColor    = float3(0.3, 0.28, 0.5);   // dark/unlit
                float3 litColor    = float3(0.92, 0.90, 1.0);  // bright white-blue when lit
                float3 glowColor2  = float3(1.0,  0.95, 1.0);  // extra bright at wave surface
                float3 illColor    = lerp(dimColor, litColor, coverAmount);
                illColor           = lerp(illColor, glowColor2, saturate(surfaceGlow * 0.5));

                col.rgb = lerp(col.rgb, illColor, illAlpha);

                // Resolution-aware edge — 1 pixel wide regardless of RawImage size
                float edgeWidth = fwidth(bd) * 1.5;
                col.a = smoothstep(edgeWidth, 0.0, bd);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col * i.color;
            }
            ENDCG
        }
    }
}
