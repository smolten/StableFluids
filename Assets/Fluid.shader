// StableFluids - A GPU implementation of Jos Stam's Stable Fluids on Unity
// https://github.com/keijiro/StableFluids

Shader "Hidden/StableFluids"
{
    Properties
    {
        _MainTex("Main", 2D) = ""
        _Tex1("Texture 1", 2D) = ""
        _Tex2("Texture 2", 2D) = ""
        _Noise("Noise", 2D) = ""
        _VelocityField("Velocity Field", 2D) = ""

        _CycleLength("Cycle Duration", float) = 0
        _Phase1("Phase 1", float) = 0
        _Phase2("Phase 2", float) = 0
        _LerpTo2("Lerp to 2", float) = 0
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _Tex1;
    sampler2D _Tex2;
    sampler2D _Noise;

    sampler2D _VelocityField;

    float2 dim = float2(512,512);
    float _Phase1;
    float _Phase2;
    float _LerpTo2;

    half4 frag_advect(v2f_img i) : SV_Target
    {
        float2 aspect_inv = float2(_MainTex_TexelSize.x * _MainTex_TexelSize.w, 1);

	    float2 velocity = tex2D(_VelocityField, i.uv).xy;
        float noise = tex2D(_Noise, i.uv).x;

        float phase1 = noise * 0.5 + _Phase1;
        float phase2 = noise * 0.5 + _Phase2;

        // Color advection with the velocity field
        float2 delta1 = tex2D(_VelocityField, (i.uv) + velocity * phase1 ).xy * aspect_inv;
        float2 delta2 = tex2D(_VelocityField, (i.uv) + velocity * phase2 ).xy * aspect_inv;

        float3 color1 = tex2D(_Tex1, i.uv + delta1).xyz;
        float3 color2 = tex2D(_Tex2, i.uv + delta2).xyz;

        // Sample color at previous position
        float3 color = lerp(color1, color2, _LerpTo2);

        return half4(color, 1);
        //return half4(1,1,1,1);
    }

    half4 frag_render(v2f_img i) : SV_Target
    {
        half3 rgb = tex2D(_MainTex, i.uv).xyz;

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
