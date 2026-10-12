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
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Diagnosis; // SignLexemeRow / ChannelReading
    using DiagnosisSignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;
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

        /// <summary>写通道(sprint-08 S1.1 件 F1)—— 病名栏 Submit 转发目标。
        /// <para>⚠️ 可为 null(未接线 ⇒ Submit 留空,不崩、不造动作)。</para></summary>
        private ICasebookCommandSink _commandSink;

        /// <summary>病名册是否摊开(**纯呈现态**,零游戏状态 —— 与
        /// <see cref="ClinicPanelScreen"/> 的 <c>_refreshPending</c> 同格;
        /// 翻页 / 折页零写入,<c>AC-39-10</c>)。</summary>
        private bool _bookOpen;

        /// <summary>病名册容器(**匿名**,运行时建于右页内 —— 不进 UXML spec 面 name 序,
        /// 故交叉断言零回归;见 <see cref="RefreshLexiconBook"/>)。</summary>
        private VisualElement _lexiconBook;

        /// <summary>最近一次 <see cref="ApplyData"/> 传入的脉案源(病名册条目集的重读面 ——
        /// 每次摊开 / 刷新都重读它,**不缓存副本**;ADR-013 §9 C3)。</summary>
        private ICasebookDtoSource _sourceForBook;

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

            // ── 右页:病名 + 置信度(「?」只落此处,AC-8-39)──
            // 置信度不落独立焦点控件(OQ-CB-5 K=6:病名焦点内循环 确定/疑似)
            var rightColumn = new VisualElement { name = "right-column" };
            AddChannel(rightColumn, "channel-disease-name", "病名");
            // 病名栏 Submit ⇒ 摊开病名册(casebook-39 §5.2 / §7「病名栏 Submit」行;
            // OQ-CB-1 甲:键鼠与手柄**同一册同一路径**)。⚠️ 焦点回调挂在此行 —— 由官方桥
            // NavigationSubmitEvent 触发(ADR-013 §五;ADR-011 Amendment C 2026-10-12 明确
            // Submit 属 42 焦点导航职责,不受 §二 50ms 直读约束)。
            // ⚠️ 经 Q<> 取回行(不改 AddChannel 签名 —— texture_binding_gate_test 以
            //    `private void AddChannel(...)` 为**结构锚点**,签名一改门即红,禁削弱该门)。
            rightColumn.Q<VisualElement>("channel-disease-name")
                .RegisterCallback<NavigationSubmitEvent>(OnDiseaseNameSubmit);
            var confidence = _library.Create(SkeuoElement.Paper);
            confidence.name = "confidence-mark";
            confidence.Add(new Label("?") { name = "confidence-question" });
            rightColumn.Add(confidence);

            // ── 左右并排(sprint-05 T2.5 钉 · 2026-10-11)──
            // UI Toolkit 默认 flex 纵向 ⇒ five-channels(左)与 right-column(右)直接挂 root
            //   = 上下堆叠,「?」变成「第 7 行」而非右列(playtest 第 3 轮 Q1 实测)。
            // 修:插**匿名** row wrapper(UI-8.1「五通道区 + 两栏」铁律③ 物理分栏)。
            //   ⚠️ 匿名 = 无 name ⇒ 不进 story-011 双真源 name 序(cross 断言仍绿);
            //     但**子树照常遍历收集** ⇒ UXML 具名序不变(casebook-title, five-channels…,
            //     right-column…),与 Suite 测试断言一致。
            var rowLayout = new VisualElement();   // 匿名夹具:不出现在任何 name 断言
            rowLayout.style.flexDirection = FlexDirection.Row;
            rowLayout.style.flexGrow = 1f;
            channels.style.flexGrow = 1f;         // 左:弹性吃满
            channels.style.minWidth = 0f;
            rightColumn.style.width = 260f;       // 右:固定窄栏(病名 + 置信度)
            rightColumn.style.marginLeft = 24f;
            rowLayout.Add(channels);
            rowLayout.Add(rightColumn);
            _root.Add(rowLayout);
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
            row.AddToClassList("ruled"); // 线格(M2 形态件① · story-021):字段由线格划出(art-bible §7.7)
            row.Add(new Label(labelText));
            parent.Add(row);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 写通道与病名册(sprint-08 S1.1 件 F/G · 2026-10-12)
        //
        // 病名栏 Submit 的**两个面**:
        //   ① 首次 Submit ⇒ **摊开病名册**(右页内嵌,非新模态;casebook-39 §5.2);
        //   ② 病名册条目行 Submit ⇒ **指认**该条词(`lexicon_id`)⇒ 写通道转发。
        //
        // ⚠️ 病名册容器**运行时建**(BuildUI 不建)⇒ UXML spec 面 name 序**一字未改**
        //   (交叉断言 `casebook_render_test` / `casebook_lexicon_test` 零回归);
        //   容器与条目行**匿名 / 动态名**,不进 spec 面。
        // ⚠️ 42 只渲染不持状态(ADR-013 §9 C3):本屏只持**病名册开合**(纯呈现态,
        //   翻页 / 折页零写入,AC-39-10)与写通道引用;**档位不在此**(住装配层,
        //   经 DTO 回读 —— 见 ICasebookDtoSource.PendingConfidence)。
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>注入写通道(42 UI 事件 → 装配层写者;未注入 ⇒ Submit 留空)。</summary>
        public void BindCommandSink(ICasebookCommandSink sink) => _commandSink = sink;

        /// <summary>病名册是否摊开(纯呈现态观测面)。</summary>
        public bool IsLexiconBookOpen => _bookOpen;

        /// <summary>病名栏 Submit 处理(焦点桥入口 → 语义方法)。</summary>
        /// <remarks>⚠️ 生产路径 = 官方桥 <c>NavigationSubmitEvent</c>(ADR-013 §五);
        /// 判据面 = <see cref="SubmitDiseaseNameBar"/>(语义层直调 —— sprint-08 §4 已裁
        /// 「判据与输入路径解耦:语义层直调,输入路径另立 wiring 测」)。</remarks>
        private void OnDiseaseNameSubmit(NavigationSubmitEvent _) => SubmitDiseaseNameBar();

        /// <summary>条目行 Submit 处理(焦点桥入口 → 语义方法)。</summary>
        private void OnLexiconEntrySubmit(NavigationSubmitEvent evt)
        {
            if (evt.target is VisualElement row && row.userData is int lexiconId)
                PointLexicon(lexiconId);
        }

        /// <summary>
        /// 病名栏落笔(**语义动作面** —— 未摊开 ⇒ 摊开病名册;已摊开 ⇒ 循环落笔)。
        /// </summary>
        /// <remarks>⚠️ 循环落笔的第 2/3 步(确定 / 擦除)经
        /// <see cref="ICasebookCommandSink.SubmitConfidence"/> 交给装配层 ——
        /// 42 **不持档位**(OQ-CB-5 甲;档位 = 事件载荷值 = 游戏状态)。</remarks>
        public void SubmitDiseaseNameBar()
        {
            if (!_bookOpen)
            {
                _bookOpen = true;
                RefreshLexiconBook();
                return;
            }
            _commandSink?.SubmitConfidence();
        }

        /// <summary>指认一条词(**语义动作面**;条目行 Submit 的转发目标)。</summary>
        /// <param name="lexiconId">显式 <c>lexicon_id</c>(非册内显示索引 —— TD 第五风险)。</param>
        public void PointLexicon(int lexiconId) => _commandSink?.SubmitDiseaseName(lexiconId);

        /// <summary>
        /// 重建病名册条目(从只读 DTO 源**重读** —— 零缓存,ADR-013 §9 C3)。
        /// <para>条目集 = <see cref="ICasebookDtoSource.SeenLexiconIds"/>(已见过病 ⇒ 词表反查);
        /// 未见过的病**不在册**(AC-8-43/44 反幻想二)。**零搜索 / 零分类 / 零自动补全 / 零体征线索**。</para>
        /// <para>条目文本经 <see cref="ICasebookDtoSource.TextOfLexicon"/> 取(42 不持词表)。</para>
        /// </summary>
        private void RefreshLexiconBook()
        {
            if (_sourceForBook == null) return;

            if (_lexiconBook == null)
            {
                // 匿名容器(无 name ⇒ 不进 spec 面 name 序)—— 挂在右页病名栏行之后
                _lexiconBook = new VisualElement();
                var anchor = _root.Q<VisualElement>("channel-disease-name");
                anchor?.parent?.Add(_lexiconBook);
            }
            _lexiconBook.Clear();

            IReadOnlyList<int> ids = _sourceForBook.SeenLexiconIds;
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++)
            {
                int lexiconId = ids[i];
                string text = _sourceForBook.TextOfLexicon(lexiconId);
                var entry = _library.Create(SkeuoElement.Paper);
                entry.focusable = true;                 // 条目可聚焦(手柄翻册指认)
                entry.userData = lexiconId;             // 显式 lexicon_id(非显示索引 —— TD 第五风险)
                entry.Add(new Label(text ?? string.Empty));
                entry.RegisterCallback<NavigationSubmitEvent>(OnLexiconEntrySubmit);
                _lexiconBook.Add(entry);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 值区施加(sprint-05 T2.1 / T2.3 / T2.5 · 2026-10-10)
        //
        // **BuildUI() 一字未改** —— story-011 双真源声明:UXML spec 面 ≡ 代码面
        // name 序等价,由 PlayMode `casebook_render_test` 的交叉断言守护。
        // 本方法只往**已存在的行**里填读数格(行头 Label 是既有唯一子节点),
        // 不改 name / 不改 class / 不改树序 ⇒ 六条既有断言零回归。
        //
        // 规则:
        //   ① 读数格 = `channel-reading` 子元素,名 `reading-<rowName>`
        //     (测试遍历锚点;值区形态对归 story-024 AC-024-2):
        //     阳性 = `.channel-reading`(ink-fg 墨字)·
        //     阴性 / 读不出 = `.channel-reading-negative`(ink-faded 降档)
        //   ② 多词条**并列保序**(:1183 不相斥)—— 一行内 N 个读数格按 Order 排
        //   ③ 全通道无词条 ⇒ **零读数格**(行只剩行头 = 空行 = 显式「我还没做」,
        //     `:329` 未查 = 空行非 badge)—— 本方法**不造**未查徽章 / 占位 / 灰字
        //   ④ 42 只画:词 / 通道 / 极性全来自 8 的表(经 SignChannelBinder 分流),
        //     零自定义映射(story-024 边界②);42 零游戏状态字段(ADR-013 §9 C3)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>通道枚举 → 脉案行名(GDD UI-8.1 五通道 + 问诊栏第 6 行,OQ-8-9)。
        /// <para>方向与 <see cref="SignChannelBinder"/> 的枚举是同一闭集
        /// (五通道 + 病史例外);8 的 `SignChannel.History` = 问诊栏。</para>
        private static string RowNameFor(DiagnosisSignChannel channel)
        {
            switch (channel)
            {
                case DiagnosisSignChannel.FaceColor: return "channel-complexion";
                case DiagnosisSignChannel.Voice: return "channel-voice";
                case DiagnosisSignChannel.Posture: return "channel-posture";
                case DiagnosisSignChannel.Breathing: return "channel-breathing";
                case DiagnosisSignChannel.Touch: return "channel-palpation";
                case DiagnosisSignChannel.History: return "channel-inquiry";
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(channel), channel,
                        "[CasebookScreen] 体征通道超出五通道 + 病史例外闭集(AC-8-32)。");
            }
        }

        /// <summary>
        /// 以只读 DTO 源刷新值区(fail-loud:绑定缺失 = 具名异常,**不退回假数据**)。
        /// </summary>
        /// <remarks>
        /// <para>刷新 = 重读源(零缓存副本);同一源重复调用结果等价(幂等)。</para>
        /// <para><c>ActiveSignCount</c> 是**门**(AC-20 白名单内的整数):为 0 ⇒ 全行走空行。
        /// 掩码→行的高粒度映射归 8 / 9 的连接表(真表已冻结同族字段),
        /// 42 不发明「哪个掩码位该亮」的真源。</para>
        /// <para><c>InkAgeTicks</c> 是**墨龄**(规则十三 派生态):本方法零计时器读取。</para>
        /// </remarks>
        /// <param name="dtoSource">42 侧只读数源(测试 = 合成 fixture;生产 = 装配层提供者)。</param>
        /// <exception cref="System.ArgumentNullException">数据源为 null。</exception>
        public void ApplyData(ICasebookDtoSource dtoSource)
        {
            if (dtoSource == null)
                throw new System.ArgumentNullException(nameof(dtoSource));

            _sourceForBook = dtoSource;   // 病名册重读面(零缓存副本,只存引用)
            if (_bookOpen) RefreshLexiconBook();   // 已摊开 ⇒ 随 tick 边沿刷新条目集

            // 门:零体征 ⇒ 全空行(tripwire 语义,DiseaseVitalsService 恒 mask=0/count=0;
            // 一旦 9 侧接线产出真词条,本门自动放行,无需改此处)。
            IReadOnlyList<SignLexemeRow> revealed =
                dtoSource.ActiveSignCount > 0 ? dtoSource.RevealedRows : null;

            IReadOnlyDictionary<DiagnosisSignChannel, IReadOnlyList<ChannelReading>> byChannel =
                SignChannelBinder.Bind(revealed ?? System.Linq.Enumerable.Empty<SignLexemeRow>(),
                                       dtoSource.PrecisionSlot);

            // 墨龄阈值归**数值轮**(art-bible §4.5 未定墨干时刻);此处用常量承载,
            // 派生自 ITickProvider 的 tick 差(1 tick = 50 ms,TICK_SECONDS 已裁定)
            // ⇒ INK_DRY_AFTER_TICKS = 墨龄超过该 tick 数即视为已干。
            const long InkDryAfterTicks = 200L;   // = 10 秒(200 × 50 ms)
            long inkAge = dtoSource.InkAgeTicks;

            foreach (var kv in byChannel)
            {
                string rowName = RowNameFor(kv.Key);
                var row = _root.Q<VisualElement>(rowName);
                if (row == null) continue;   // 行由 BuildUI 建;缺行 = 结构被改,由交叉断言先红

                // 先清旧读数格(重入 / 源切换 ⇒ 不留上一源的残留 = 零第二真相残影)
                for (int i = row.childCount - 1; i >= 0; i--)
                {
                    var child = row[i];
                    if (child.name != null && child.name.StartsWith("reading-"))
                        row.RemoveAt(i);
                }

                // 值区:每词条一格,阳性 / 阴性形态对(AC-024-2);零词条 = 不加(空行)
                foreach (ChannelReading reading in kv.Value)
                {
                    var cell = new VisualElement { name = "reading-" + rowName };
                    bool negative = reading.Word == null ||
                                    reading.Polarity == SignPolarity.Negative;
                    cell.AddToClassList(negative ? "channel-reading-negative"
                                                 : "channel-reading");
                    // 墨龄状态(story-022 形态件② / 规则十三):湿墨(刚落笔)/ 干墨(墨龄超阈)。
                    // 与极性类**可叠加**(状态 ≠ 变体 —— story-022 边界①:
                    // 状态类随数据切换,不进 Registry)。
                    // ⚠️ 加载序冲突登记(story-021 边界⑥ / SkeuoFocusVisible.uss 头注):
                    // `.ink-wet`/`.ink-dry` 与 `.channel-reading*` 同写 `color`,单元素双类时
                    // 胜负由未钉死的 stylesheet 加载序决定 —— 焦点接线前不可观测(今日零施加者),
                    // 非缺陷;裁决项登记不擅自裁。
                    cell.AddToClassList(inkAge >= InkDryAfterTicks ? "ink-dry" : "ink-wet");
                    cell.Add(new Label(reading.Word ?? string.Empty));
                    row.Add(cell);
                }
            }
        }
    }
}
