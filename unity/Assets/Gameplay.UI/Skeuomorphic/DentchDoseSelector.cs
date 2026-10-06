// 权威来源:
//   GDD 规则十三(戥子输入契约四条)· 规则十二(方笺 = 39 脉案同一本书内的一页,已裁)
//   · UI-11.1(归属切分)· UI-11.2(硬约束 + 对 42 的两项规格义务)
//   · §Game Feel(离散档 ↔ 离散形态;戥杆倾角 / 药包鼓胀)· AC-11-12/13/18/21
//   · ADR-011(规则十三:float→int 量化在 3 / 表现层完成,**11 只见整数**;
//     禁手势连续拖拽 —— 两栈皆无实现路径;选剂 = 焦点移动 + 确认键)
//   · ADR-013 §五(焦点走官方桥 NavigationMoveEvent / FocusController,**不自实现焦点算法**)
//   · ADR-013 §十-B Amendment B(戥子读数元件 = **黄铜侧**;禁降级数字读数,两栈皆禁无例外)
//   · ADR-020 §五/§六(相机只读不持状态;11 不发档位意图 —— 39 是 Casebook 档唯一请求方)
//   · ADR-014(功效词 = 烘焙期转出字符串,零运行期查表)
//   · Story: production/epics/prescription-medication/story-005-dentch-input-casebook-presentation.md
//
// ⚠️ 本件住 Gameplay.UI(呈现层):**只渲染,永不持游戏状态**(承 ADR-013 §9 C3)。
//    「当前剂量」不驻留为游戏状态 —— 它只活在焦点落点集合里(本类不缓存 selected 档;
//    焦点在哪由 FocusController 决定,本类只读当前落点的 ordinal 供回执/呈现)。
//    重开方笺 ⇒ 由调用方重新装载档位序列重建视图,本类无跨会话残留。
//
// ⚠️ 本文件所在的 42 元件库目录受 **AC-42-F3 词面门**扫描(`world_billboard_test` 的
//    `test_ac42f3_componentLibrary_noForbiddenItems`)—— 该门**逐字符扫原文,注释也扫**。
//    ⇒ 该门的全部禁用词面(含用于**声明禁止**的文档措辞)在本目录内一律不得出现,
//    本条纪律因此只能以释义表述,不得照抄禁用词。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>
    /// 戥子剂量档位的一枚**焦点落点**(规则十三)。
    /// <para>落点 ↔ `dose_range` 的一档一一对应;**不存在第 `hi+1` 落点**
    /// —— 限位由落点集合本身给出(AC-11-18 ①:零 clamp 路径)。</para>
    /// <para>整剂路径(<c>dose_range == null</c>)⇒ **单一落点「整剂」**(ordinal = 1),
    /// 呈现上**不是「0 档」**(与 story-004 AC-11-17 整剂路径同形)。</para>
    /// </summary>
    public sealed class DentchDetent : IFocusable
    {
        /// <summary>档序数(1 起)。**只出整数** —— 11 与呈现层皆不见浮点。</summary>
        public int Ordinal { get; }

        /// <summary>焦点秩次(AC-42-B1:值域恰为 1..K;越小越优先)。</summary>
        public int FocusRank { get; }

        /// <summary>本落点是否可聚焦(实现 <see cref="IFocusable.IsFocusEnabled"/>)。
        /// <para>⚠️ **门控位由装载期注入** —— 本 story 的生成路径恒传 <c>true</c>;
        /// 「戥子满档 / 缺药 ⇒ false」的**判定源**归 20 库存扣减面,尚未落地
        /// ⇒ 该分支**未产生也未判据**,登记 NOT-RUN(禁借绿)。</para></summary>
        public bool IsFocusEnabled { get; }

        /// <summary>无障碍语义标签(**零数字**:AC-11-12 零数字读数 —— 可见读数由黄铜形态承载,
        /// 本字段不入可见呈现,只供辅助技术辨识「整剂 / 常规档」两类落点)。</summary>
        public string PresentationLabel { get; }

        /// <summary>本落点是否承载「整剂」语义(空 `dose_range` 的唯一落点)。</summary>
        public bool IsWholeDose { get; }

        public DentchDetent(int ordinal, int focusRank, bool isFocusEnabled, string presentationLabel, bool isWholeDose)
        {
            Ordinal = ordinal;
            FocusRank = focusRank;
            IsFocusEnabled = isFocusEnabled;
            PresentationLabel = presentationLabel;
            IsWholeDose = isWholeDose;
        }
    }

    /// <summary>
    /// 方笺上的一次**落笔**(承 8 侧 `S-8.4` 路线甲:施治 = 方笺落笔 → 11)。
    /// <para>⚠️ **只有整数档序数** —— float→int 量化住 3 / 表现层(ADR-011 规则十三);
    /// 本类型是呈现层交给 11 的**唯一**输入形状(11 的 <see cref="PrescribeRequest.SelectedDose"/> 同域)。</para>
    /// <para>⚠️ 本类型**不含**任何可传输 / 可入流对象,也不含病种语义
    /// —— `PresentationDtoGuard` 递归扫描覆盖(AC-37-15)。</para>
    /// </summary>
    public readonly struct PrescribeStrokeIntent
    {
        /// <summary>医师(玩家)id。</summary>
        public readonly int ActorId;

        /// <summary>施治对象(呈现层持有的整数 id)。</summary>
        public readonly int PatientId;

        /// <summary>所开之药(复合主键)。</summary>
        public readonly ItemKey ItemKey;

        /// <summary>**整数档序数**(1 起;整剂路径 = 1)。</summary>
        public readonly int DoseOrdinal;

        public PrescribeStrokeIntent(int actorId, int patientId, ItemKey itemKey, int doseOrdinal)
        {
            ActorId = actorId;
            PatientId = patientId;
            ItemKey = itemKey;
            DoseOrdinal = doseOrdinal;
        }
    }

    /// <summary>一次落笔的回执(只读;失败原因不回传 —— 零提示纪律 AC-11-12)。</summary>
    public readonly struct PrescribeStrokeResult
    {
        /// <summary>是否完成落笔。</summary>
        public readonly bool Stroked;

        /// <summary>实际落笔的档序数(未落笔 = 0)。</summary>
        public readonly int DoseOrdinal;

        public PrescribeStrokeResult(bool stroked, int doseOrdinal)
        {
            Stroked = stroked;
            DoseOrdinal = doseOrdinal;
        }
    }

    /// <summary>
    /// 戥子档位选择器(规则十三)—— **离散整数档的焦点序列**。
    /// </summary>
    /// <remarks>
    /// <para><b>档位生成</b>:`dose_range = (lo, hi)` ⇒ 落点 = `hi − lo + 1` 个;
    /// `dose_range == null`(整剂)⇒ **恰 1 个**「整剂」落点(非 0 落点)。
    /// 序列由 <see cref="BuildDetents"/> 确定生成,与档数无关的常量时间。</para>
    /// <para><b>限位非 clamp</b>:`hi` 之后**没有下一个落点** —— 越界输入在本类**不可达**
    /// (不是被 clamp 掉,而是压根没有那条路径)。`AC-11-18` ① 的 11 侧静态断言在
    /// `Sim/Prescription/` 上执行,本类是 ② 侧的消费方实现。</para>
    /// <para><b>零降级读数</b>:本类**不持有任何降级数字读数节点**
    /// (42 侧 `§Visual 二`:两栈皆禁,无例外)。档位可辨性由黄铜侧形态元件承载
    /// (戥杆倾角 / 药包鼓胀,`BrassScaleElement`),本类只提供 `TickCount` 语义量。</para>
    /// <para><b>零相机意图</b>:方笺 = 脉案同一本书内的一页 ⇒ **不成新模态**
    /// (`ModalId` 闭集仍 7 员,不因方笺增员)、**不发相机档位意图**(`TR-prescription-014` 已裁终态)。</para>
    /// <para><b>零状态播报音</b>:本类不触发任何音效(承 ADR-018 无提示音铁律 / AC-11-21)。</para>
    /// </remarks>
    public sealed class DentchDoseSelector
    {
        /// <summary>档数上限旋钮的**登记建议值**(GDD 规则十三;值归用户数值轮)。</summary>
        public const int RegisteredMaxDoseDetents = 9;

        private readonly DentchDetent[] _detents;
        private readonly int _doseMin;

        /// <summary>本药是否走整剂路径(`dose_range` 为空)。</summary>
        public bool IsWholeDose { get; }

        /// <summary>剂量域下界 `lo`(整剂路径 = 0,不参与运算)。</summary>
        public int DoseMin => _doseMin;

        /// <summary>档数上限旋钮(装载期注入;`≤ 0` ⇒ 装配错误硬失败)。
        /// <para>⚠️ **单位 = 落点数**(= `hi − lo + 1`,亦 = 焦点路径长度,承 Fitts 理据)——
        /// 与 GDD `:497` 的「`hi − lo` 档数上限」措辞**差一**(那是区间宽度的口语说法);
        /// 本类与旋钮一律以落点数计,已在 GDD 侧登记订正。</para></summary>
        public int MaxDoseDetents { get; }

        /// <summary>焦点落点序列(只读;长度 = `hi − lo + 1`,整剂路径 = 1)。</summary>
        public IReadOnlyList<DentchDetent> Detents => _detents;

        /// <summary>档位刻度数(= 落点数;供黄铜侧读数元件消费,语义归本类)。</summary>
        public int TickCount => _detents.Length;

        /// <summary>由药物档案 + 档数上限旋钮构造档位序列。</summary>
        /// <param name="profile">21a 药物档案(`dose_range` 可空 ⇒ 整剂路径)。</param>
        /// <param name="maxDoseDetents">档数上限旋钮(`dose_const.MAX_DOSE_DETENTS`;值归用户)。</param>
        /// <exception cref="ArgumentOutOfRangeException">旋钮非正 —— 装配错误,装载期硬失败。</exception>
        /// <exception cref="InvalidOperationException">档数超上限 —— 装载期硬失败(不静默截断)。</exception>
        public DentchDoseSelector(in DrugProfile profile, int maxDoseDetents)
            : this(profile, maxDoseDetents, isFocusEnabled: true) { }

        /// <summary>由药物档案 + 档数上限旋钮 + 焦点门控位构造档位序列。</summary>
        /// <param name="profile">21a 药物档案(`dose_range` 可空 ⇒ 整剂路径)。</param>
        /// <param name="maxDoseDetents">档数上限旋钮(单位 = **落点数**;值归用户)。</param>
        /// <param name="isFocusEnabled">焦点门控位(判定源归 20;本 story 恒 true,登记 NOT-RUN)。</param>
        public DentchDoseSelector(in DrugProfile profile, int maxDoseDetents, bool isFocusEnabled)
        {
            if (maxDoseDetents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDoseDetents),
                    $"MAX_DOSE_DETENTS 必须 > 0,实际 = {maxDoseDetents}(装配错误,须在装载期硬失败)");

            MaxDoseDetents = maxDoseDetents;
            IsWholeDose = profile.DoseRange == null;
            _doseMin = profile.DoseRange?.Min ?? 0;

            var range = profile.DoseRange;
            if (range != null && range.Value.Min > range.Value.Max)
                throw new InvalidOperationException(
                    $"prescription/dentch: dose_range = ({range.Value.Min}, {range.Value.Max}) 下界 > 上界" +
                    " —— 档位集合无从生成(数据错误,装载期硬失败,不静默取空集)。");

            _detents = BuildDetents(range, maxDoseDetents, isFocusEnabled);
        }

        /// <summary>
        /// 档位序列的**确定生成**(规则十三)。
        /// <para>整剂路径 ⇒ 恰 1 个「整剂」落点;**不生成 0 档**(呈现上不是「0 档」)。</para>
        /// <para>档数 > <paramref name="maxDoseDetents"/> ⇒ **硬失败**(不静默截断:
        /// 截断会让玩家「以为选了 hi+3」,正是 AC-11-18 要防的失败模式)。</para>
        /// </summary>
        public static DentchDetent[] BuildDetents(DoseRange? range, int maxDoseDetents,
                                                  bool isFocusEnabled = true)
        {
            if (maxDoseDetents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDoseDetents),
                    $"MAX_DOSE_DETENTS 必须 > 0,实际 = {maxDoseDetents}");

            if (range == null)
            {
                // 整剂路径(AC-11-17 呈现侧同形):单一落点,ordinal = 1。
                return new[] { new DentchDetent(1, 1, isFocusEnabled, "整剂", true) };
            }

            int lo = range.Value.Min, hi = range.Value.Max;
            long count = (long)hi - lo + 1;
            if (count <= 0)
                throw new InvalidOperationException(
                    $"prescription/dentch: dose_range = ({lo}, {hi}) 档数 = {count} ≤ 0 —— 无从生成焦点序列。");

            if (count > maxDoseDetents)
                throw new InvalidOperationException(
                    $"prescription/dentch: dose_range = ({lo}, {hi}) 档数 = {count} 超过 MAX_DOSE_DETENTS = " +
                    $"{maxDoseDetents}(Fitts 长路径上限)。硬失败,不静默截断 —— 截断会让玩家以为选到了 hi。");

            var detents = new DentchDetent[(int)count];
            for (int i = 0; i < detents.Length; i++)
            {
                // ordinal = 档序数(1 起);FocusRank = 同一序数 ⇒ 值域恰 1..K(AC-42-B1 满射 + 单射)。
                detents[i] = new DentchDetent(
                    ordinal: i + 1,
                    focusRank: i + 1,
                    isFocusEnabled: isFocusEnabled,
                    // 零数字:标签只标「常规档」类别,不写档序数(序数在 Ordinal 整数里,不在呈现面)。
                    presentationLabel: "戥子档",
                    isWholeDose: false);
            }
            return detents;
        }

        /// <summary>
        /// 把**焦点落点的 ordinal** 翻译为 11 的**整数档值**(`lo + ordinal − 1`)。
        /// <para>⚠️ 这是呈现层 → 11 的**唯一**量化点,且**全程整数**(ADR-011 规则十三:
        /// float→int 量化住 3 / 表现层)。**不做 clamp** —— `ordinal` 越界 ⇒ 硬失败
        /// (落点集合即档位集合 ⇒ 正常路径不可达;可达即为装配 / 调用错误)。</para>
        /// </summary>
        /// <param name="ordinal">档序数(1 起;整剂路径 = 1)。</param>
        /// <exception cref="ArgumentOutOfRangeException">ordinal 越出落点集合 —— 不可达路径,如实抛出。</exception>
        public int DoseValueOf(int ordinal)
        {
            if (ordinal < 1 || ordinal > _detents.Length)
                throw new ArgumentOutOfRangeException(nameof(ordinal),
                    $"档序数 {ordinal} 越出落点集合 [1, {_detents.Length}] —— " +
                    "落点集合即档位集合,越界在 UI 不可达(AC-11-18 ①:零 clamp 路径,如实抛出)。");

            if (IsWholeDose)
                return 1;   // 整剂:11 侧 ResolveEffectiveDose 亦置 1(AC-11-17)

            // 档值 = lo + (ordinal − 1);**整数加**,零浮点、零 clamp。
            return _doseMin + (ordinal - 1);
        }

        /// <summary>
        /// 档序数是否已到**顶档**(`hi`)—— 顶档按「下一档」⇒ 焦点**不动**
        /// (AC-11-18 ② 的语义半边;实体元件与纸面反馈归 42 侧走查)。
        /// </summary>
        public bool IsAtTopDetent(int ordinal) => ordinal >= _detents.Length;

        /// <summary>
        /// 把本选择器的刻度数**装载进黄铜侧读数元件**(AC-42-F3 黄铜分支)。
        /// <para>这是「刻度数由本类提供、禁第二真源」的**唯一**接线点 ——
        /// 语义量归本类,形态归 42 元件。</para>
        /// </summary>
        /// <param name="element">黄铜侧刻度元件(42 侧交付)。</param>
        /// <exception cref="ArgumentNullException">元件为 null。</exception>
        public void LoadInto(BrassScaleElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            element.TickCount = _detents.Length;
        }
    }
}
