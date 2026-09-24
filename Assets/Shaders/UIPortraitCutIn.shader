Shader "UI/PortraitCutIn"
{
    Properties
    {
        _MainTex ("Hero Camera RenderTexture", 2D) = "white" {}
        
        [Header(Polygonal Corner Vertex (Kink Point))]
        _KinkPoint ("Corner Pivot Point (X, Y in UVs)", Vector) = (0.65, 0.20, 0, 0)
        
        [Header(Edge Slopes)]
        _BottomSlope ("Bottom Edge Slope", Range(-10.0, 10.0)) = 0.25
        _TopSlope ("Top Edge Slope", Range(-10.0, 10.0)) = 0.60
        
        [Header(Outer Drop Shadow Settings)]
        _ShadowWidth ("Outer Shadow Width", Range(0.001, 0.30)) = 0.035
        _ShadowIntensity ("Outer Shadow Opacity", Range(0.0, 1.0)) = 0.85
        _ShadowColor ("Outer Shadow Tint Color", Color) = (0.0, 0.0, 0.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _KinkPoint;
            float _BottomSlope;
            float _TopSlope;
            float _ShadowWidth;
            float _ShadowIntensity;
            fixed4 _ShadowColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Calculates the maximum allowed X boundary for a given Y coordinate
            float GetPolyBoundaryX(float y)
            {
                if (y < _KinkPoint.y)
                {
                    // Bottom segment equation: from left edge up to the kink point
                    return _KinkPoint.x + (y - _KinkPoint.y) * _BottomSlope;
                }
                else
                {
                    // Upper segment equation: continuous diagonal straight to top edge
                    return _KinkPoint.x + (y - _KinkPoint.y) * _TopSlope;
                }
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float maxX = GetPolyBoundaryX(i.uv.y);

                // Pixel is inside the cut-in area: render clean hero image without inner shadow
                if (i.uv.x <= maxX)
                {
                    return tex2D(_MainTex, i.uv);
                }

                // Pixel is outside the cut-in area: render outer drop shadow gradient
                float shadowEnd = maxX + _ShadowWidth;
                if (i.uv.x < shadowEnd)
                {
                    // Linear falloff gradient from full shadow at boundary to zero shadow
                    float shadowFactor = 1.0 - ((i.uv.x - maxX) / _ShadowWidth);
                    float shadowAlpha = saturate(shadowFactor) * _ShadowIntensity;
                    
                    return fixed4(_ShadowColor.rgb, shadowAlpha);
                }

                // Pixel is completely beyond the shadow radius
                discard;
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}