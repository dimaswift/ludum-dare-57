Shader "ConsequenceCascade/Particle"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Opaque" }
        //Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
          
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };
            struct FieldCell {  
                float2 position;
                float2 previousPosition;
                float siderealTime;
                float precessionalTime;
            };
            
            StructuredBuffer<FieldCell> Particles;
            float Time;
            float Size;
            v2f vert(appdata_t i, uint instanceID : SV_InstanceID)
            {
                v2f o;
                const FieldCell cell = Particles[instanceID];
                float s = Size;
                float4x4 translationMatrix = float4x4(
                    s, 0, 0, cell.position.x,
                    0, s, 0, cell.position.y,
                    0, 0, s, 0,
                    0, 0, 0, 1
                );
                float vel = length(cell.previousPosition - cell.position);
                float4 pos = mul(translationMatrix, i.vertex);
                o.vertex = UnityObjectToClipPos(pos);
                o.color = float4(vel, 1, 1, 1);
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}