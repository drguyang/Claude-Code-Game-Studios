// ============================================================================
// 死亡过渡呈现器(42 侧 · 表现态装配;2026-11-01)
// 权威来源: design/ux/death-transition-29.md §5(全屏水墨叠层)· §6(相位)· §10(数据)
//           · production/epics/death-respawn/story-006(AC-29-06/07)
// ============================================================================
// ⚠️ **呈现三件套同构纪律**:只渲染,永不持有游戏状态 ——
//   · 数据源 = `DeathTransitionQuery.Project` 的只读投影(逐帧轮询;委托注入,构造后可换源);
//   · **零写路径**(不触 IEventSink / 三流);
//   · **零交互**(raycastTarget = false —— 暂停/模态不受遮蔽,spec §7);
//   · 相位机 = `DeathTransitionDriver`(纯表现态;时长常量归数值轮)。
// ⚠️ **水墨纹理 = 程序化生成**(径向渐变 + 确定性值噪声;256² 一次生成)——
//   正式水墨素材归美术资产批(art-bible §4.5 三步流程);**生成失败/纹理缺失 ⇒ ΦE
//   纯黑兜底**(spec §6:节奏不变,零文字零结算)。
// ⚠️ **远端变体**(局部水墨标识)与**相机两段式**本批未落 —— 分别挂 45 的第二 QoS
//   装配轮与 `ICameraRig` 机位轮(如实登记,spec §6/§9 的对应行不记绿)。
// ============================================================================

using DaYiJingCheng.Gameplay.Presentation.DeathTransition;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace DaYiJingCheng.Gameplay.Presentation.DeathTransition
{
    /// <summary>
    /// 死亡/复活全屏过渡呈现器:全屏水墨叠层(零交互)按相位机渐入/渐出。
    /// </summary>
    public sealed class DeathTransitionPresenter : MonoBehaviour
    {
        private System.Func<System.Collections.Generic.IReadOnlyList<SimEvent>> _eventsOf;
        private IPayloadDecoder _decoder;
        private System.Func<int> _localActorIdOf;

        private DeathTransitionDriver _driver = DeathTransitionDriver.Inactive;
        private CanvasGroup _group;
        private RawImage _wash;

        /// <summary>注入只读数据源(BootRoot 装配段调用;模拟态只读,零写路径)。</summary>
        public void Bind(System.Func<System.Collections.Generic.IReadOnlyList<SimEvent>> eventsOf,
            IPayloadDecoder decoder, System.Func<int> localActorIdOf)
        {
            _eventsOf = eventsOf ?? throw new System.ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new System.ArgumentNullException(nameof(decoder));
            _localActorIdOf = localActorIdOf ?? throw new System.ArgumentNullException(nameof(localActorIdOf));
        }

        private void Awake()
        {
            BuildOverlay();
        }

        private void Update()
        {
            if (_eventsOf == null || _group == null) return;

            // 数据源可能已拆(BootRoot 拆卸段置 `_services = null` ⇒ 闭包返回 null)——
            // 此时**停更**(不抛、不刷错误):呈现层随场景一起退场。
            var events = _eventsOf();
            if (events == null) return;

            int actorId = _localActorIdOf();
            DeathTransitionDto dto = DeathTransitionQuery.Project(events, _decoder, actorId, 1);
            _driver.Step(dto.Phase, Time.deltaTime);

            _group.alpha = _driver.Alpha;
            _group.gameObject.SetActive(_driver.Phase != DeathTransitionVisualPhase.Inactive);
        }

        /// <summary>码建全屏叠层(UGUI;零交互 —— raycastTarget = false,spec §7)。</summary>
        private void BuildOverlay()
        {
            var canvasGo = new GameObject("DeathTransitionCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // 全屏过渡在全部界面之上(零交互 ⇒ 不挡点击)

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;   // 零交互(过渡无操作)
            _group.blocksRaycasts = false; // 不遮蔽暂停/模态(§7;呈现不持状态)

            var imageGo = new GameObject("InkWash");
            imageGo.transform.SetParent(canvasGo.transform, false);
            _wash = imageGo.AddComponent<RawImage>();
            _wash.raycastTarget = false;
            _wash.color = Color.white;

            Texture2D ink = InkWashTexture.Create();
            if (ink != null)
            {
                _wash.texture = ink;                     // 水墨晕染(程序化;正式素材归资产批)
                imageGo.transform.localScale = Vector3.one;
                var rt = imageGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                _wash.color = Color.black;               // ΦE 兜底:纯黑,节奏不变(spec §6)
            }
            canvasGo.SetActive(false);                   // Φ0 零叠层
        }
    }

    /// <summary>
    /// 程序化水墨晕染纹理:**墨自四边向内渗透 + 中央留白**(零外部资产依赖)。
    /// <para>⚠️ **方向由 spec 钉死,不是美术偏好**:`death-transition-29.md` §5.4 线框逐字写
    /// 「墨色由四边向中央晕染,中央刻意留白 —— 无任何元件」,§9 转场表亦写
    /// 「水墨自四边向中央渐入」/「自中央向四边消散」。⇒ 墨**重**在四边、**透**在中央。
    /// **初版做反了**(中心墨团 + 边缘透明),2026-11-01 图形走查截图
    /// `s237_death_falling.png` 实拍即为此形态,已按 spec 纠正。</para>
    /// <para>⚠️ **噪声必须是平滑多频值噪声**:初版逐像素 LCG 白噪声被拉伸到 1920×1080 后,
    /// 每个噪声像素放大成 ~7×7 方块 ⇒ 视觉呈粗颗粒斑驳,不像水墨(同轮实测)。</para>
    /// <para>⚠️ **「留白」是墨淡,不是墨无**(2026-11-01 图形走查第二轮实测纠正):首版把中央
    /// 做成 alpha=0,截图 `s237_revive_waking.png`(Φ3,`CanvasGroup.alpha = 1`)里**中央完全透明**
    /// ⇒ 世界绿地面清晰可见,读作「墨边框」而非 spec §6 要求的「画面**近满墨**」/「失去控制权的
    /// 静默」。重读 spec §5.2「晕染自然留出**呼吸空间**,不置任何元件」与 §5.4「中央刻意留白
    /// —— **无任何元件**」:留白指的是**信息层不放置元件**,墨本身仍应在中央**淡着**
    /// ⇒ 现取中央保底 <see cref="MinInk"/>,四边满墨,浓淡差即「呼吸空间」。</para>
    /// <para>⚠️ **为什么静态纹理 + alpha 就能表达「自四边向内」**:墨强度按**到最近边的距离**
    /// 递减 ⇒ alpha 从 0 升到 1 的过程中,四边最先变黑、中央最后才被渗透(淡墨区保持最久)
    /// —— 不需要几何动画,相位机推 alpha 即得 spec 的运动感。</para>
    /// </summary>
    public static class InkWashTexture
    {
        /// <summary>
        /// 中央保底墨浓度(0–1)。0.68 ⇒ 满屏时中央仍压掉约七成世界亮度(读作「近满墨」),
        /// 同时保留浓淡层次(读作「呼吸空间」)。**不得取 0** —— 那会退回「墨边框」形态。
        /// </summary>
        private const float MinInk = 0.68f;

        /// <summary>墨向内渗透的带宽(归一化 UV 比例;0.40 ⇒ 四边各渗入 40%,中央淡墨)。</summary>
        private const float InkBand = 0.40f;

        /// <summary>带宽边缘的羽化宽度(归一化;越小墨边界越硬)。</summary>
        private const float Feather = 0.25f;

        /// <summary>噪声对墨边界的扰动幅度(归一化;产生晕染破碎感而非几何直线)。</summary>
        private const float Roughness = 0.35f;

        /// <summary>生成水墨纹理(一次调用一次生成;确定性 —— 同参数逐字节同)。</summary>
        /// <param name="size">纹理边长(默认 512 —— 256 在 1080p 下放大 7.5×,边缘仍偏硬)。</param>
        /// <param name="seed">噪声种子(确定性 LCG)。</param>
        public static Texture2D Create(int size = 512, int seed = 0)
        {
            try
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float span = size - 1f;

                // 三级倍频值噪声(格子 8 / 24 / 72 ⇒ 由粗到细),权重 0.55 / 0.30 / 0.15
                // —— 逐级累加进同一张缓冲(不引第三方、不用数组扩展方法)。
                float[] octave = new float[size * size];
                AccumulateNoise(octave, size, seed, 8, 0.55f);
                AccumulateNoise(octave, size, seed + 1, 24, 0.30f);
                AccumulateNoise(octave, size, seed + 2, 72, 0.15f);

                for (int y = 0; y < size; y++)
                {
                    float v = y / span;                       // 0(下)→ 1(上)
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / span;                   // 0(左)→ 1(右)
                        byte alpha = InkAlphaAt(u, v, octave[y * size + x]);
                        pixels[y * size + x] = new Color32(16, 14, 15, alpha); // 近墨色(带一点暖灰)
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                return tex;
            }
            catch (System.Exception)
            {
                return null; // ΦE 兜底路径(呈现器转纯黑)
            }
        }

        /// <summary>
        /// **单点墨浓度**(归一化 UV `u,v ∈ [0,1]` + 噪声 `n ∈ [0,1]` ⇒ alpha 0–255)。
        /// <para>⚠️ **抽成纯函数是为了可判**:`Texture2D` 在 `-nographics` 下不可靠,
        /// 而「四边重 / 中央淡」这两条 spec 硬要求必须能被 EditMode 夹具逐点断言
        /// (承 coding-standards §「自述顶替门」—— 注释里声称「按 spec 做对了」不构成门)。</para>
        /// <para>返回 255 = 满墨;返回 `255*MinInk` = 中央保底淡墨。**不得**返回 0 ——
        /// 中央 alpha=0 会退回「墨边框」形态(2026-11-01 实测)。</para>
        /// </summary>
        public static byte InkAlphaAt(float u, float v, float n)
        {
            // 到**最近边**的归一化距离:0(贴边)→ 0.5(正中央)。
            // 用矩形边距而非径向 ⇒ 四边都吃墨(径向只覆盖左右两侧)。
            float edgeDist = Mathf.Min(
                Mathf.Min(u, 1f - u),
                Mathf.Min(v, 1f - v));

            // 墨强度:贴边最强(≥1 后截断),向内按带宽递减;噪声扰动晕散边界。
            float ink = Mathf.Clamp01((InkBand - edgeDist + (n - 0.5f) * Roughness) / Feather);
            // 中央保底:淡墨而非无墨(留白 = 信息层不置元件,不是墨消失)。
            ink = MinInk + (1f - MinInk) * ink;
            return (byte)(255 * ink);
        }

        /// <summary>
        /// 单级**平滑值噪声**并**累加**进 <paramref name="accum"/>:
        /// 在 <paramref name="cells"/>×<paramref name="cells"/> 的格点上取确定性随机值,
        /// 再**双线性插值 + smoothstep** 铺满整幅(⇒ 相邻像素连续,不是白噪声)。
        /// </summary>
        private static void AccumulateNoise(float[] accum, int size, int seed, int cells, float octaveAmp)
        {
            uint state = (uint)(seed * 73856093) + (uint)cells * 19349663u + 1u;
            float[] grid = new float[(cells + 1) * (cells + 1)];
            for (int i = 0; i < grid.Length; i++)
            {
                state = state * 1664525u + 1013904223u;
                grid[i] = (state >> 24) / 255f;   // 0–1
            }

            float scale = cells / (float)size;
            for (int y = 0; y < size; y++)
            {
                float fy = y * scale;
                int y0 = (int)fy;
                float ty = fy - y0;
                ty = ty * ty * (3f - 2f * ty);                 // smoothstep ⇒ 更柔的过渡
                for (int x = 0; x < size; x++)
                {
                    float fx = x * scale;
                    int x0 = (int)fx;
                    float tx = fx - x0;
                    tx = tx * tx * (3f - 2f * tx);

                    float v00 = grid[y0 * (cells + 1) + x0];
                    float v10 = grid[y0 * (cells + 1) + x0 + 1];
                    float v01 = grid[(y0 + 1) * (cells + 1) + x0];
                    float v11 = grid[(y0 + 1) * (cells + 1) + x0 + 1];
                    float v0 = v00 + (v10 - v00) * tx;
                    float v1 = v01 + (v11 - v01) * tx;
                    accum[y * size + x] += (v0 + (v1 - v0) * ty) * octaveAmp;
                }
            }
        }
    }
}
