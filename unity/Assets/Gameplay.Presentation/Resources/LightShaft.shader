// 光柱用的**唯一**自定义 shader(2026-11-13 · §8.7)。
//
// ═══ 为什么必须自己写一个 ═════════════════════════════════════════════════
//   ① URP 自带的 `Unlit` 在打包版里 `Shader.Find` 找不到(没有材质引用 ⇒ 被剥,`TD-build-03`);
//   ② 更关键的是:**玩家会走进光柱里**。加法混合 + `Cull Off` 的锥台,一旦相机进入体内,
//      整屏就糊成一层白雾(实测:α=0.30 时全画面发灰,2016 字面意义上的「看不见东西」)。
//      所以这段要按**到相机的距离**做淡出 —— 近处的片元直接归零。
//   ③ 这不是 Renderer Feature,只是一段 unlit 的 vertex/fragment,ADR-023 §⑧ 不涉及。
//
// 放在 `Resources/` 下 ⇒ 随包必进(Resources 里的资源不会被剥)。

Shader "DaYiJingCheng/LightShaft"
{
    Properties
    {
        _MainTex ("Gradient", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.94, 0.82, 0.1)
        _NearFade ("Near Fade (m)", Float) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _NearFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float dist : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dist = distance(_WorldSpaceCameraPos, world);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                // 近处淡出:相机贴到柱子上时不糊屏;远处不受影响。
                c.a *= saturate((i.dist - _NearFade) / max(_NearFade, 0.001));
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
