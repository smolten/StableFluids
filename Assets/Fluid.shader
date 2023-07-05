// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// https://github.com/keijiro/StableFluids

Shader "Hidden/StableFluids"
{
    Properties
    {
        _MainTex("", 2D) = ""
        _SubTex("", 2D) = ""
        _VelocityField("", 2D) = ""

        _CycleLength("", float) = 0
        _Phase1("", float) = 0
        _Phase2("", float) = 0
        _LerpTo2("", float) = 0
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _SubTex;

    sampler2D _VelocityField;

    float2 _ForceOrigin;
    float _ForceExponent;

    float2 dim = float2(512,512);
    float _CycleLength;
    float _Phase1;
    float _Phase2;
    float _LerpTo2;

    half4 frag_advect(v2f_img i) : SV_Target
    {
	    float2 velocity = tex2D(_VelocityField, i.uv).xy;

        // Color advection with the velocity field
        float2 delta1 = tex2D(_VelocityField, (i.uv) + velocity * _Phase1 ).xy;
        float2 delta2 = tex2D(_VelocityField, (i.uv) + velocity * _Phase2 ).xy;

        float3 color1 = tex2D(_MainTex, i.uv - delta1).xyz;
        float3 color2 = tex2D(_SubTex, i.uv - delta2).xyz;

        // Blend
        float HalfCycle = _CycleLength / 2.0;
        //float lerpto2 = ( abs(HalfCycle - _Phase1) / HalfCycle );
        //float2 offset = float2(0,0);
        //offset = lerp(delta1, delta2, _LerpTo2);

        // Sample color at previous position
        //float3 color = tex2D(_MainTex, i.uv - offset).xyz;
        float3 color = lerp(color1, color2, _LerpTo2);

        // float3 color1 = tex2D(_MainTex, i.uv - delta1).xyz;
        // float3 color2 = tex2D(_SubTex, i.uv - delta2).xyz;
        // color = lerp(color1, color2, _LerpTo2);

        // Dye (injection color)
        // float3 dye = saturate(sin(time * float3(2.72, 5.12, 4.98)) + 0.5);

        // // Blend dye with the color from the buffer.
        // float2 pos = (i.uv - 0.5) * aspect;
        // float amp = exp(-_ForceExponent * distance(_ForceOrigin, pos));
        // color = lerp(color, dye, saturate(amp * 100));

        // // Base Texture
        // color = tex2D(_MainTex, i.uv).xyz;

        return half4(color, 1);
    }

    half4 frag_render(v2f_img i) : SV_Target
    {
        half3 rgb = tex2D(_MainTex, i.uv).rgb;

        // Mixing channels up to get slowly changing false colors
        //rgb = sin(float3(3.43, 4.43, 3.84) * rgb +
        //          float3(0.12, 0.23, 0.44) * _Time.y) * 0.5 + 0.5;

        return half4(GammaToLinearSpace(rgb), 1);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_advect
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_render
            ENDCG
        }
    }
}
