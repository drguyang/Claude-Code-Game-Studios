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
    /// 程序化水墨晕染纹理:径向 alpha 渐变 + 确定性值噪声边缘(零外部资产依赖)。
    /// </summary>
    public static class InkWashTexture
    {
        /// <summary>生成水墨纹理(一次调用一次生成;确定性 —— 同参数逐字节同)。</summary>
        public static Texture2D Create(int size = 256, int seed = 0)
        {
            try
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float center = (size - 1) * 0.5f;
                float maxR = center;
                uint state = (uint)seed * 2654435761u + 1u;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - center) / maxR;
                        float dy = (y - center) / maxR;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);          // 0(心)→ ~1.41(角)
                        state = state * 1664525u + 1013904223u;           // LCG(确定性噪声)
                        float noise = (state >> 24) / 255f;               // 0–1
                        // 晕染:边缘(0.72 起)随噪声破碎,向中心快速实心。
                        float a = Mathf.Clamp01((r - 0.55f + noise * 0.25f) / 0.55f);
                        byte alpha = (byte)(255 * (1f - a));
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
    }
}
