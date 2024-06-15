Shader "Custom/VelocityColor"
{
    Properties
    {
        _VelocityField ("Velocity Field", 3D) = "" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler3D _VelocityField;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uvw : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uvw = v.vertex.xyz / float3(_ScreenParams.xy, 1.0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 velocity = tex3D(_VelocityField, i.uvw).rgb;
                float speed = length(velocity);

                // Map speed to a color for visualization
                fixed4 color = lerp(fixed4(0, 0, 1, 1), fixed4(1, 1, 0, 1), speed);
                return color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
