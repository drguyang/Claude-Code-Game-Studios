// 全屏遮罩 · 标题卡(42 侧原语 · V-12 · 2026-11-14 用户裁定)
//
// 权威来源:
//   `design/gdd/skeuomorphic-ui.md` §一 V-12(全屏遮罩 / 标题卡)
//   `design/levels/prologue-beat-script.md` P1-06(那一夜:黑底字幕)/ P3-05(黑底 + 标题卡)
//
// ─────────────────────────────────────────────────────────────────────────────
// 它是什么
// ─────────────────────────────────────────────────────────────────────────────
// 一块**绝对铺满**的墨黑面片,可选居中一行**题签**(标题卡)。
//   · 淡入 / 淡出是**纯呈现**:不透明度逐帧推进,**零计时器字段**
//     (承 V-4 的口径:42 不持计时器 —— 时长由调用方每帧喂 `deltaSeconds`);
//   · 时长走 `MotionScaleHook`(规则十钩子④):`Scale = 0` ⇒ 瞬切,界面仍完全可用;
//   · **不**参与焦点 / 模态:遮罩没有可聚焦元素,`pickingMode = Ignore`。
//
// ─────────────────────────────────────────────────────────────────────────────
// 它不是什么(三条硬线)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不是模态**:不进 `ModalId` 闭集、不动 `_currentModal` —— 它挡的是**画面**,
//    不是「玩家摊开的册子」。过场期间玩家能不能动,归 N3 的 `CutscenePlayer`
//    (已并进 `ModalInputFreeze`),遮罩自己**不判**输入。
// ② **不持状态**:「该不该遮黑」是装配层(序章节拍 / 过场)的事实,
//    本类只执行 `Show / Hide`。它连「第几拍」都不知道。
// ③ **不写时间**:`Step(deltaSeconds)` 由调用方每帧喂;本类没有 `Update`。
//
// ⚠️ 为什么不做 USS transition(与 `CasebookScreen.FadeInPage` 的分工差异):
//    翻页淡入是**一次性视觉糖**,调度器 `schedule.Execute` 在无面板的 EditMode
//    夹具里不派发;而遮罩的「淡出完成」是 P3-05 的**完成判据** —— 它必须可判、
//    可复现 ⇒ 走显式 `Step`(纯函数式,夹具直调)。

using System;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>
    /// 全屏遮罩(黑场 + 可选标题卡)—— 42 的换场原语(V-12)。
    /// </summary>
    public sealed class FullScreenMask
    {
        /// <summary>淡入 / 淡出的**基准**时长(秒;种子值,数值归用户数值轮)。</summary>
        /// <remarks>
        /// 取 0.8 s:短到不拖沓、长到「换场」与「闪一下」可辨。
        /// ⚠️ 实际时长 = 本值 ÷ <see cref="MotionScaleHook.Scale"/> 的反比
        /// (即 `Scale = 0.5` ⇒ 1.6 s);`Scale = 0` ⇒ 瞬切(<c>AC-42-G4</c> 后半)。
        /// </remarks>
        public const float BaseFadeSeconds = 0.8f;

        private readonly VisualElement _host;
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly FadeInOutDriver _fade = new FadeInOutDriver();

        /// <summary>目标态(<c>true</c> = 正在淡入 / 已全黑;<c>false</c> = 正在淡出 / 已退场)。</summary>
        private bool _targetUp;

        /// <summary>当前是否在屏上(含淡出中;完全退场后为 <c>false</c>)。</summary>
        private bool _isUp;

        /// <summary>创建遮罩并挂到宿主(通常是 `UIDocument` 的面板根)。</summary>
        /// <param name="host">宿主容器(非空;遮罩绝对铺满它)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> 为 null。</exception>
        public FullScreenMask(VisualElement host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            _root = new VisualElement { name = "full-screen-mask" };
            _root.AddToClassList("full-screen-mask");
            // 铺满后默认会吞掉整块面板的指针拾取 —— 遮罩不是交互面(与 `.modal-host` 同一条理由)。
            _root.pickingMode = PickingMode.Ignore;
            _root.style.display = DisplayStyle.None;

            _title = new Label { name = "full-screen-mask-title" };
            _title.AddToClassList("full-screen-mask__title");
            _title.pickingMode = PickingMode.Ignore;
            _title.style.display = DisplayStyle.None;
            _root.Add(_title);

            _host.Add(_root);
        }

        /// <summary>动效缩放钩子(规则十钩子④)。</summary>
        public MotionScaleHook Motion { get; } = new MotionScaleHook();

        /// <summary>遮罩当前是否在屏上(含淡出中)。</summary>
        public bool IsUp => _isUp;

        /// <summary>是否已全黑(淡入完成)。</summary>
        public bool IsFullyOpaque => _isUp && _fade.Opacity >= 1f;

        /// <summary>是否已完全透明(淡出完成 —— P3-05 的完成判据)。</summary>
        public bool IsFullyTransparent => _fade.Opacity <= 0f;

        /// <summary>当前不透明度(0..1;夹具观测面)。</summary>
        public float Opacity => _fade.Opacity;

        /// <summary>当前题签(空串 = 无题签,只有黑场)。</summary>
        public string Title => _title.text ?? string.Empty;

        /// <summary>根元素(诊断 / 夹具观测面;不要往它上面挂别的东西)。</summary>
        public VisualElement Root => _root;

        /// <summary>
        /// 拉起遮罩并从**全透明**淡入;已拉起时只换题签(**不**重置不透明度)。
        /// </summary>
        /// <param name="title">题签;`null` / 空串 = 无题签(纯黑场,如 P1-06)。</param>
        public void Show(string title)
        {
            SetTitle(title);

            if (_isUp)
            {
                // 淡出中途被叫回(或重复 Show)⇒ 只反向目标,不闪回全透明。
                _targetUp = true;
                return;
            }

            _isUp = true;
            _targetUp = true;
            _root.style.display = DisplayStyle.Flex;
            // 重新挂到宿主末尾 ⇒ 压住此前已摊开的任何屏(遮罩必须是最上层)。
            _root.RemoveFromHierarchy();
            _host.Add(_root);

            float duration = Motion.EvaluateDuration(BaseFadeSeconds);
            if (duration <= 0f)
            {
                _fade.Snap(true);
                Apply();
                return;
            }

            _fade.Snap(false);   // 从全透明起步(不在过渡前闪一帧全黑)
            Apply();
        }

        /// <summary>淡出遮罩;完全退场后 `IsUp` 变 <c>false</c>。</summary>
        public void Hide()
        {
            if (!_isUp) return;
            _targetUp = false;

            float duration = Motion.EvaluateDuration(BaseFadeSeconds);
            if (duration <= 0f)
            {
                _fade.Snap(false);
                _isUp = false;
            }
            Apply();
        }

        /// <summary>瞬切到指定可见性(动效缩放 = 0 的降级路径;也可用于读档复位)。</summary>
        /// <param name="visible">`true` = 全黑;`false` = 退场。</param>
        public void Snap(bool visible)
        {
            _targetUp = visible;
            _isUp = visible;
            _fade.Snap(visible);
            if (visible)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.RemoveFromHierarchy();
                _host.Add(_root);
            }
            Apply();
        }

        /// <summary>
        /// 推进一步淡入 / 淡出(调用方每帧喂一次;无面板也能推进)。
        /// </summary>
        /// <param name="deltaSeconds">距上一帧的秒数(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaSeconds"/> 为负。</exception>
        public void Step(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds,
                    "帧时间不得为负(时钟回拨不是本层的合法输入)");
            if (!_isUp) return;

            float duration = Motion.EvaluateDuration(BaseFadeSeconds);
            if (duration <= 0f)
            {
                // 无障碍:无动效 ⇒ 瞬切。目标态即终态。
                _fade.Snap(_targetUp);
                if (!_targetUp) _isUp = false;
                Apply();
                return;
            }

            // ⚠️ 归一化步长 = 帧时间 ÷ 实际时长;`FadeInOutDriver.MotionScale` 保持 1
            //    —— 缩放已由 `EvaluateDuration` 吸收,再乘一次会把时长平方。
            float normalizedStep = deltaSeconds / duration;
            if (_targetUp) _fade.StepFadeIn(normalizedStep);
            else _fade.StepFadeOut(normalizedStep);

            if (!_targetUp && _fade.Opacity <= 0f) _isUp = false;
            Apply();
        }

        private void SetTitle(string title)
        {
            string text = title ?? string.Empty;
            _title.text = text;
            _title.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void Apply()
        {
            _root.style.opacity = _fade.Opacity;
            if (!_isUp)
                _root.style.display = DisplayStyle.None;
        }
    }
}
