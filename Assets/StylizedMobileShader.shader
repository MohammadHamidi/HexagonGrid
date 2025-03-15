Shader "Custom/StylizedMobileShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.8
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8.0)) = 3.0
        _Brightness ("Brightness", Range(0,2)) = 1.2
        _Saturation ("Saturation", Range(0,2)) = 1.4
        _Contrast ("Contrast", Range(0,2)) = 1.2
        _BevelAmount ("Bevel Amount", Range(0,1)) = 0.3
        _BevelWidth ("Bevel Width", Range(0,0.1)) = 0.01
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows
        #pragma target 3.0
        
        sampler2D _MainTex;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };
        
        half _Glossiness;
        fixed4 _Color;
        float4 _RimColor;
        float _RimPower;
        float _Brightness;
        float _Saturation;
        float _Contrast;
        float _BevelAmount;
        float _BevelWidth;
        
        // Color adjustment function
        float3 AdjustColor(float3 color)
        {
            // Brightness
            float3 adjusted = color * _Brightness;
            
            // Saturation
            float luminance = dot(adjusted, float3(0.299, 0.587, 0.114));
            adjusted = lerp(float3(luminance, luminance, luminance), adjusted, _Saturation);
            
            // Contrast
            adjusted = (adjusted - 0.5) * _Contrast + 0.5;
            
            return saturate(adjusted);
        }
        
        void surf (Input IN, inout SurfaceOutputStandardSpecular o)
        {
            // Sample base texture
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Apply color adjustments
            float3 adjusted = AdjustColor(c.rgb);
            
            // Calculate rim lighting
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), IN.worldNormal));
            float rimIntensity = pow(rim, _RimPower);
            
            // Create a simple bevel effect
            float2 dx = ddx(IN.uv_MainTex);
            float2 dy = ddy(IN.uv_MainTex);
            float2 uvDelta = sqrt(dx * dx + dy * dy);
            float edgeFactor = saturate(length(uvDelta) / _BevelWidth);
            float bevel = lerp(1.0 - _BevelAmount, 1.0, edgeFactor);
            
            // Apply rim light and bevel to the color
            o.Albedo = adjusted * bevel;
            o.Specular = 0.2; // Low specular to keep the cartoon look
            o.Smoothness = _Glossiness;
            o.Emission = _RimColor.rgb * rimIntensity * 0.5; // Subtle rim emission
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}