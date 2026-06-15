Shader "Hidden/RadialFlashVFX"
{
    Properties
    {
        _FlashColor ("Flash Color",  Color)  = (1,1,1,1)
        _Radius     ("Radius",       Float)  = 0
        _Center     ("Center",       Vector) = (0.5, 0.5, 0, 0)
        _Softness   ("Softness",     Float)  = 0.08
        _Aspect     ("Aspect",       Float)  = 1.78
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos    : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _FlashColor;
            float  _Radius;
            float4 _Center;
            float  _Softness;
            float  _Aspect;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Correct for aspect ratio so the shape stays a true circle
                float2 uv  = i.uv - _Center.xy;
                uv.x      *= _Aspect;
                float dist = length(uv);

                // 1 inside the circle, 0 outside, smooth edge
                float alpha = smoothstep(_Radius + _Softness, _Radius - _Softness, dist);

                fixed4 col = _FlashColor;
                col.a     *= alpha;
                return col;
            }
            ENDCG
        }
    }
}
