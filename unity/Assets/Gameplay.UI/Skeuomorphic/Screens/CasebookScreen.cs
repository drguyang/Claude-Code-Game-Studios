// 权威来源:production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//   · design/ux/casebook-39.md
//   · ADR-013 §四 Key Interfaces
//   · diagnosis-system GDD UI-8.1(五通道区 + 两栏布局铁律)/ UI-8.2(焦点序)
//     —— story-006 结构订正(承 OQ-8-9 ✅ 2026-10-06)
//
// 设计说明:
//   · 脉案页 = 五通道区(面色/语声/姿态/呼吸/触感)+ 问诊栏(第 6 行)+ 右栏(病名/置信度)
//   · 五行 + 问诊栏**固定成序、永不隐藏、永不留空**(空行 = 显式「我还没做」——
//     无灰字 / 无占位文字 / 无进度徽章,AC-8-23 / AC-8-43 负向声明)
//   · 病名与体征分栏(归属不同,UI-8.1 铁律③);「?」只在病名/置信度处(AC-8-39)
//   · 焦点序(AC-8-23 措辞以 GDD UI-8.2 2026-10-06 订正面为准):面色 → 语声 → 姿态 →
//     呼吸 → 触感 → 问诊栏 → 病名;置信度不落独立控件(OQ-CB-5 K=6 病名/置信度循环)
//     —— 卡面旧序差异登记见卡 Completion Notes(本轮回填)
//   · **双真源声明(评审 S-3/q1)**:同屏两面 —— `Casebook39.uxml`(UXML spec 面,AC-42-F1
//     走查加载)与本类 `BuildUI()`(代码运行面,driver 未接前的唯一可构造面);
//     两面 **name 结构序必须等价**,由 PlayMode `casebook_render_test` 的
//     cross 断言守护(XDocument 解析 UXML ≡ 代码树序列)—— 改任一面若不同步即红
//   · 继承 PresentationRoot,实现 IModalState;使用 SkeuoElementLibrary 创建元件
//   · 42 只渲染永不持状态(ADR-013 §9 C3)—— 本屏零游戏状态字段,数据由调用方喂入
//   · 「?」显隐登记(评审 S-6):现恒渲染;显/隐与 确定/疑似 文案切换归 39/11 喂值接缝
//     (story-011/接线轮),本屏不持置信度状态

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 脉案页(39)——五通道区 + 问诊栏 + 右栏(病名/置信度)。
    /// <para>ModalId.Casebook,手柄走查 + 目视零按键提示浮层。</para>
    /// </summary>
    public sealed class CasebookScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建脉案页。</summary>
        public CasebookScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Casebook;

        /// <summary>构建脉案页 UI(UI-8.1 结构 + UI-8.2 焦点序)。</summary>
        private void BuildUI()
        {
            // 标题(与 UXML spec 面对齐 —— 交叉断言锁 name 序等价)
            _root.Add(new Label("脉案") { name = "casebook-title" });

            // ── 左页:五通道区 + 问诊栏(第 6 行,OQ-8-9)──
            // 焦点序 = 树序(面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊栏),自上而下固定,
            // 不因已查集合重排(UI-8.2);focusable 由官方桥导航,本处只定序。
            var channels = new VisualElement { name = "five-channels" };
            AddChannel(channels, "channel-complexion", "面色");  // 1
            AddChannel(channels, "channel-voice", "语声");        // 2
            AddChannel(channels, "channel-posture", "姿态");      // 3
            AddChannel(channels, "channel-breathing", "呼吸");    // 4
            AddChannel(channels, "channel-palpation", "触感");    // 5
            AddChannel(channels, "channel-inquiry", "问诊");      // 6(问诊栏,非体征通道)
            _root.Add(channels);

            // ── 右页:病名 + 置信度(「?」只落此处,AC-8-39)──
            // 置信度不落独立焦点控件(OQ-CB-5 K=6:病名焦点内循环 确定/疑似)
            var rightColumn = new VisualElement { name = "right-column" };
            AddChannel(rightColumn, "channel-disease-name", "病名");
            var confidence = _library.Create(SkeuoElement.Paper);
            confidence.name = "confidence-mark";
            confidence.Add(new Label("?") { name = "confidence-question" });
            rightColumn.Add(confidence);
            _root.Add(rightColumn);
        }

        /// <summary>
        /// 添加一行通道:**行存在即非空**(有行头标签),行内读数格留空由 39 喂值
        /// (空行 = 显式「我还没做」—— 本方法不产生任何占位文字,AC-8-23/43)。
        /// </summary>
        /// <param name="parent">父容器。</param>
        /// <param name="rowName">行名(测试遍历锚点)。</param>
        /// <param name="labelText">行头标签(通道名,非读数)。</param>
        private void AddChannel(VisualElement parent, string rowName, string labelText)
        {
            var row = _library.Create(SkeuoElement.Paper);
            row.name = rowName;
            row.focusable = true;
            row.Add(new Label(labelText));
            parent.Add(row);
        }
    }
}
