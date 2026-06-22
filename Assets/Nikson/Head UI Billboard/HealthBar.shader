Shader "Nikson/HealthBar"
{
    Properties
    {
        _FillAmount      ("Fill",              Range(0,1))    = 1
        _BarColorTop     ("Bar Color Top",     Color)         = (0.4,0,0,1)
        _BarColorBottom  ("Bar Color Bottom",  Color)         = (1,0,0,1)
        _BackgroundColor ("Background Color",  Color)         = (0,0,0,1)
        _GradientPower   ("Gradient Power",    Range(0.5, 2)) = 1
        _BorderSize      ("Border Size",       Range(0,0.4))  = 0.2
        _Aspect          ("Aspect Ratio (W/H)",Float)         = 7.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            float  _FillAmount, _GradientPower, _BorderSize, _Aspect;
            float4 _BarColorBottom, _BarColorTop, _BackgroundColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float bY = _BorderSize;
                float bX = _BorderSize / _Aspect;

                bool inInner = i.uv.x > bX && i.uv.x < (1.0 - bX) &&
                               i.uv.y > bY && i.uv.y < (1.0 - bY);

                if (!inInner)
                    return _BackgroundColor;

                float2 innerUV;
                innerUV.x = (i.uv.x - bX) / (1.0 - 2.0 * bX);
                innerUV.y = (i.uv.y - bY) / (1.0 - 2.0 * bY);

                if (innerUV.x > _FillAmount)
                    return _BackgroundColor;

                float t = pow(innerUV.y, _GradientPower);
                return lerp(_BarColorBottom, _BarColorTop, t);
            }
            ENDCG
        }
    }
}