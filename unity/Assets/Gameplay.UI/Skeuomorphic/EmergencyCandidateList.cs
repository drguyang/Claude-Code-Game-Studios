// sprint-05 T2.4 · story-023 形态件③ 的运行时载体(2026-10-10)
//
// 权威来源:
//   production/epics/skeuomorphic-ui/story-023(急救零数字 + 可跳过)
//     · 边界裁定 ①:只交形态半 —— **施加点 / 候选动作列表载体 / Idle 态可达时序归
//       10 实现轮 + 数据绑定轮**;⚠️ AB-3 宽半(列表项宽 ≥44)由**10 轮载体**锁定。
//     · `O-10-1`:跳过入口 owner = 42(10 不拥有任何 UI 元素,10 向 42 提规格)
//     · 规则六之甲(:301-325):跳过入口 = **候选动作列表上的一项**
//   ADR-013 §三(呈现归 42)· §9 C3(只渲染不持状态)
//   art-bible G2(反数值化:无血条无小地图时玩家仍能找到信息)
//
// 设计说明(为何本类不是「跨越归属自行接线」):
//   story-023 的边界裁定把**载体**明判给 10 轮。但 sprint-05 §1 实测:候选列表
//   在整个 Gameplay.UI 装配内**零载体**(无任何类持有列表)—— 若本 sprint 不建,
//   T2.4 判据「候选列表载体运行时实例化;跳过入口可用」无处挂接,只能空转。
//   本类因此只做**载体形态**(列表容器 + 跳过入口项),并且:
//     ① **零动作语义** —— 列表项文字由调用方喂;本类不认识任何急救动作
//     ② **零时序** —— Idle 态何时可达归 10 的状态机,本类只提供 Show/Hide 门
//     ③ **零数字** —— AC-023-2 门(USS 侧零 `content:` 数字 / 零 `X/N`)在呈现结构面
//     ④ AB-3 宽半**只锁本类**(min-width) —— 与 story-023-3 只锁高半对称;
//        10 轮的真载体若另起布局,须自行承接宽半(登记为接缝,不由本类越界承诺)

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.UIElements;

    /// <summary>
    /// 急救候选动作列表载体(story-023 形态件③ · sprint-05 T2.4)。
    /// <para>跳过入口 = 列表上的一项(`.skip-entry`);列表项文字**全部**由调用方喂入
    /// —— 本类零动作语义、零时序、零数字(承 story-023 边界裁定①的三不)。</para>
    /// <para>只渲染不持状态(ADR-013 §9 C3):字段只有 VisualElement 产物。</para>
    /// </summary>
    public sealed class EmergencyCandidateList
    {
        /// <summary>列表容器名(测试 / 走查遍历锚点)。</summary>
        public const string ListName = "emergency-candidates";

        /// <summary>跳过入口项名(规则六之甲:跳过 = 列表项,不是按钮组外的旁路)。</summary>
        public const string SkipEntryName = "skip-entry";

        /// <summary>候选列表根容器。</summary>
        private readonly VisualElement _root = new VisualElement { name = ListName };

        /// <summary>已渲染的候选项(不含跳过入口;只作观测面)。</summary>
        private readonly List<VisualElement> _entries = new List<VisualElement>();

        /// <summary>跳过入口项(恒存在 —— 可跳过是结构承诺,不是条件项)。</summary>
        private VisualElement _skipEntry;

        /// <summary>跳过入口 Submit 回调(story-009 · 2026-10-14):由组合根注入
        /// (→ CasebookCommandRouter.SubmitEmergencySkip → EmergencyManualDriver.TryManual)。
        /// null = 未接线( Submit 零操作)。</summary>
        private Action _onSkipSubmit;

        /// <summary>UiExceptionGuard 同款洪流守护(2026-10-14 · CasebookScreen 先例):
        /// Submit 回调抛异常且**每帧重复** ⇒ InputSystemUIInputModule 派发栈反复炸 ⇒
        /// Console 洪流 ⇒ EPIPE ⇒ 编辑器进程被杀。30 帧窗口内同型重复 ⇒ 吞并停投。</summary>
        private int _skipGuardCount;
        private string _skipGuardKey;

        /// <summary>候选项数(不含跳过入口)。</summary>
        public int EntryCount => _entries.Count;

        /// <summary>跳过入口是否已实例化(恒 true —— <see cref="Build"/> 之后)。</summary>
        public bool HasSkipEntry => _skipEntry != null;

        /// <summary>跳过入口是否可获焦点(AC-9-07 观测面;Build 内置位 true)。</summary>
        public bool SkipFocusable => _skipEntry != null && _skipEntry.focusable;

        /// <summary>列表根容器(挂到 Screen / Panel 上)。</summary>
        public VisualElement Root => _root;

        /// <summary>
        /// 以候选动作标签集建列表(跳过入口恒追加在**最后**—— 规则六之甲:
        /// 跳过 = 列表上的一项,位置在末尾 = 「不选也是一种选择」)。
        /// </summary>
        /// <param name="labels">候选动作标签(**调用方喂入的文字**;null / 空 = 零候选项,
        /// 列表仍存在且仍带跳过入口 —— 零候选 + 可跳过是合法形态)。</param>
        /// <exception cref="ArgumentException">任一标签为 null 或空串
        /// (空标签 = 隐形项 = 焦点落点不可见,破 AB-3)。</exception>
        public void Build(IReadOnlyList<string> labels)
        {
            _root.Clear();
            _entries.Clear();

            if (labels != null)
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    string label = labels[i];
                    if (string.IsNullOrEmpty(label))
                        throw new ArgumentException(
                            $"候选项 [{i}] 标签为 null / 空串 —— 空标签 = 焦点落点不可见(破 AB-3)",
                            nameof(labels));

                    var entry = new VisualElement { name = "candidate-" + i };
                    // ⚠️ story-009 勘误(2026-10-14):候选项此前误挂 `.skip-entry`
                    //    (与真跳过入口混淆 —— 疑似复制粘贴)。候选项的正确语义类 =
                    //    `.candidate-entry`(宽半承接类,story-023 警示②);USS 门
                    //    (emergency_skip_entry_test Assert ⑤)只锁声明块恰一,不受影响。
                    entry.AddToClassList("candidate-entry");
                    entry.Add(new Label(label));
                    _entries.Add(entry);
                }
            }

            // 跳过入口:恒存在,恒在末尾。
            // ⚠️ 复用 `.skip-entry` 类(语义样式类,story-023 边界④:不进 Registry)
            //    —— 焦点单槽铁律同款(零 background-image / 零 border-*)
            _skipEntry = new VisualElement { name = SkipEntryName };
            _skipEntry.AddToClassList("skip-entry");
            _skipEntry.Add(new Label("跳过"));

            // ── 交互接线(story-009 · AC-9-07:候选列表零交互绑定此前 = 死 UI)──
            // focusable = true ⇒ 可获焦点(FocusController 导航落点);
            // NavigationSubmitEvent = 键盘 / 手柄路径(官方桥 NavigationMoveEvent 同栈);
            // ClickEvent = 鼠标路径。两条路径同投 <see cref="SubmitSkip"/>(洪流守护内)。
            _skipEntry.focusable = true;
            _skipEntry.RegisterCallback<NavigationSubmitEvent>(_ => SubmitSkip());
            _skipEntry.RegisterCallback<ClickEvent>(_ => SubmitSkip());

            foreach (var e in _entries) _root.Add(e);
            _root.Add(_skipEntry);
        }

        /// <summary>注入跳过 Submit 回调(story-009;组合根指向 CasebookCommandRouter。
        /// Build 重跑后接线保持 —— 回调是实例字段,不是元素上的闭包)。</summary>
        public void SetSkipCallback(Action onSubmit) => _onSkipSubmit = onSubmit;

        /// <summary>跳过 Submit(洪流守护内投递 —— 守护语义见字段注)。</summary>
        private void SubmitSkip()
        {
            var cb = _onSkipSubmit;
            if (cb == null) return;
            try
            {
                cb();
                _skipGuardCount = 0;
                _skipGuardKey = null;
            }
            catch (Exception ex)
            {
                var st = ex.StackTrace;
                string key = ex.GetType().FullName + "|" + (st == null ? "" : st.Split('\n')[0].Trim());
                if (key != _skipGuardKey) { _skipGuardKey = key; _skipGuardCount = 1; }
                else if (++_skipGuardCount >= 30) return;   // 30 帧窗口同型重复 ⇒ 停投
            }
        }

        /// <summary>
        /// 可见性门(Idle 态可达**时序**归 10 的状态机 —— 本类只提供门,
        /// 不判定何时该开)。false = 列表不渲染(display: None)。
        /// </summary>
        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>当前是否可见。</summary>
        public bool IsVisible => _root.resolvedStyle.display != DisplayStyle.None;
    }
}
