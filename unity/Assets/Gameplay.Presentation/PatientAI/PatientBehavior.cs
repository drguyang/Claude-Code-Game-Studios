// patient-ai Story 001 —— 每病人行为态持有者(滞回 `prev` + Terminal 锁存 + SessionState)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §States 一(滞回 prev)· 一-bis(SessionState 边界层赐予)
//     · 一-ter(Terminal 锁存)· §Core Rules 十二(OnExamSessionChanged)
//   ADR-009 §一 Q1(决策 = 派生态:不进流、不存档)* ADR-016 §一 补注
//
// ⚠️ **本类的字典住 13 自己的派生态** —— 不写回 sim(规则一)。
//    重建期该字典从锚点播种(story 004 的口径);`acc` / `p.Cell` 不属本 story。
// ⚠️ **Terminal 是锁存位** —— 一旦置位,**`Map()` 的输出不再被采用**(见 `Current`)。
//    这不是「新状态」,是「不再重新求值」(GDD 字面)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>单病人的 13 侧派生态 —— 滞回 `prev` 态 + 会诊态 + 终态锁存。
    /// <para>⚠️ **不持有 `position` / `trend` 真值** —— 那是 9 的;本类只在 tick 内
    /// 接收一次读数并缓存**上一 tick 的行为态**(滞回用)。</para></summary>
    public sealed class PatientBehavior
    {
        /// <summary>上一 tick 的行为态(`Map` 的滞回记忆)。首拍 = `Idle`。</summary>
        public BehaviorState Prev { get; private set; } = BehaviorState.Idle;

        /// <summary>会诊态 —— **只由 <see cref="ApplyExamSession"/> 写**(AC-13-B5 ①:无推断点)。</summary>
        public SessionState Session { get; private set; } = SessionState.None;

        /// <summary>终局锁存位(GDD §States 一-ter)。</summary>
        public TerminalFlag Terminal { get; private set; } = TerminalFlag.None;

        /// <summary>本 tick 的行为态 —— **终态置位后恒返回置位前最后一态**(闩锁,单调)。
        /// <para>⚠️ 这是 AC-13-B1 之外 GDD §States 一-ter 的「进入终态后 `Map` 不再改出」
        /// 的落点:终态后即便喂回升的 `position`,输出**不变**。</para></summary>
        public BehaviorState Current { get; private set; } = BehaviorState.Idle;

        /// <summary>本 tick 的症状表现档(只表现)。</summary>
        public SymptomTier Tier { get; private set; } = SymptomTier.Recover;

        /// <summary>推进一拍 —— 读一次体征读数,更新行为态 / 症状档 / 终态锁存。
        /// <para>⚠️ **终态已经置位 ⇒ 直接返回**(`Map` 不再被调用 —— 单调,AC-13-B1 的锁存半边)。</para></summary>
        /// <param name="position">`VitalsDto.Position`(13 **不夹取**)。</param>
        /// <param name="trend">`VitalsDto.Trend`。</param>
        /// <param name="bands">13 自有分档阈值。</param>
        /// <param name="knowsClinic">`KnowsClinic(p)`(F-13.2;story 002 落 `HomeRegion`)。</param>
        public void Step(float position, float trend, in BehaviorBands bands, bool knowsClinic)
        {
            // ① 终态锁存:**一旦置位不再重新求值**(GDD 字面 —— 痊愈的病人不因 position 回升而复活)
            if (!Terminal.Latched)
            {
                var t = BehaviorMap.EvaluateTerminal(position, trend, bands);
                if (t.Latched) Terminal = t;
            }

            // ② 症状档(只表现;终态后也更新无妨 —— 它不改行为)
            Tier = BehaviorMap.Tier(position, trend, bands);

            // ③ 行为态:终态后**冻结**(闩锁)
            if (Terminal.Latched) return;

            var next = BehaviorMap.Map(new BehaviorInput(position, trend, Prev), bands, knowsClinic);
            Prev = next;
            Current = next;
        }

        /// <summary>会诊态入口 —— **13 侧唯一写入者**(AC-13-B5 ①:反射断言写入点唯一)。
        /// <para>三条判据:① 唯一入口(本方法);② **幂等**(重复同值不叠加);
        /// ③ 加载后重置 `None`(见 <see cref="ResetForLoad"/>)。</para>
        /// <para>⚠️ **13 侧不得**用「玩家靠近 / 面对病人」推断会诊态 —— 那是**第四来源**(规则六),
        /// 且会让「玩家路过」误触发配合姿态(GDD 规则十二)。</para></summary>
        /// <param name="active">`true` ⇒ `InTreatment`;`false` ⇒ `None`(行为立即回落 `Map()`)。</param>
        public void ApplyExamSession(bool active)
        {
            // 幂等:同值重复调用不产生任何变化(AC-13-B5 ②)
            Session = active ? SessionState.InTreatment : SessionState.None;
        }

        /// <summary>加载后重置 —— **全部派生态归零**(AC-13-B5 ③ / EC-13-05 / story-004 B1 修复)。
        /// <para>⚠️ 会诊态**不进存档**(它由边界层在加载后重新赐予),故加载期必须显式清零 ——
        /// 否则「存档时会诊中」的病人会在加载后**永远**保持 `InTreatment`。</para>
        /// <para>⚠️ **story-004 修复**:原实现只重置 `Session`,遗漏 `Prev` / `Current` / `Terminal` / `Tier` ——
        /// 导致读档后滞回 `prev` 与终态闩锁**跨加载存活**,重建测试若复用同一批 director 会红。
        /// 现全部归零,与「 freshly constructed 」语义一致。</para></summary>
        public void ResetForLoad()
        {
            Prev = BehaviorState.Idle;
            Session = SessionState.None;
            Terminal = TerminalFlag.None;
            Current = BehaviorState.Idle;
            Tier = SymptomTier.Recover;
        }
    }

    /// <summary>13 的行为决策器 —— 持 `IVitalsQuery`(唯一取数入口)+ 每病人派生态字典。
    /// <para>⚠️ **不持 `IEventSink` / `IEventAuthority`**(AC-13-A1);**不持 PRNG**(AC-13-D5)。</para></summary>
    public sealed class PatientBehaviorDirector
    {
        private readonly IVitalsQuery _vitals;
        private readonly BehaviorBands _bands;
        private readonly Dictionary<int, PatientBehavior> _states = new Dictionary<int, PatientBehavior>();

        /// <summary>构造 —— 依赖注入 `IVitalsQuery`(AC-13-A2:取数唯一入口)。</summary>
        /// <param name="vitals">9 的体征查询门面(全案唯一浮点出口)。</param>
        /// <param name="bands">13 自有分档阈值 —— **构造期硬校验**(见下)。</param>
        /// <exception cref="ArgumentException">分档表破 GDD §Tuning 一 硬约束(如 `MILD_MIN > SEEK_MIN`)——
        /// 破表不是「难玩」,是**状态机自相矛盾**(两个转出条件同时为真)⇒ 硬失败,不静默降级。</exception>
        public PatientBehaviorDirector(IVitalsQuery vitals, in BehaviorBands bands)
        {
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            // ⚠️ **F-2 修复(2026-10-04 评审)**:`Validate` 此前**只有测试调用者** ——
            //    数值轮把真值落 `ai_patient.json` 后,破约束表会**静默生效**(测试绿 ≠ 生产表合法)。
            //    现构造期硬校验:破表即 throw(与 ADR-014 阶段 2 烘焙校验口径一致:
            //    破约束 = 构建期/启动期硬失败,非 Debug.Assert)。
            var errs = BehaviorBands.Validate(bands);
            if (errs.Count > 0)
                throw new ArgumentException(
                    "BEHAVIOR_BAND_* 破硬约束(GDD §Tuning 一)—— 状态机会自相矛盾:\n" +
                    string.Join("\n", errs), nameof(bands));
            _bands = bands;
        }

        /// <summary>取病人行为态(不存在则建 —— 首拍 `prev = Idle`)。
        /// <para>⚠️ 本方法**只读 `IVitalsQuery`**(AC-13-A2);不嗅探距离 / 不读表现态位置。</para></summary>
        public PatientBehavior For(PatientId id)
        {
            if (!_states.TryGetValue(id.Value, out var b))
            {
                b = new PatientBehavior();
                _states[id.Value] = b;
            }
            return b;
        }

        /// <summary>推进一拍(读一次 `VitalsDto`,`Map` 一次)。
        /// <para>⚠️ **`signs[]` 不参与** —— `Map` 的输入恰 `(position, trend, prev)`(AC-13-A5)。
        /// `VitalsDto` 的 `SignChannelMask` / `SignCount` **在本方法内被刻意忽略**
        /// (它们只喂 <see cref="Material"/>)。</para></summary>
        /// <param name="id">病人。</param>
        /// <param name="knowsClinic">`KnowsClinic(p)`(F-13.2;story 002 落 `HomeRegion`)。</param>
        public void Step(PatientId id, bool knowsClinic)
        {
            VitalsDto v = _vitals.GetVitals(id);       // 唯一取数入口(AC-13-A2)
            For(id).Step(v.Position, v.Trend, _bands, knowsClinic);
        }

        /// <summary>会诊态入口(转发;唯一写入者)。</summary>
        public void OnExamSessionChanged(PatientId id, bool active) => For(id).ApplyExamSession(active);

        /// <summary>加载后重置全部病人会诊态(AC-13-B5 ③)。</summary>
        public void ResetForLoad()
        {
            foreach (var b in _states.Values) b.ResetForLoad();
        }

        /// <summary>表现材质映射(F-13.8)—— **`signs[]` 的唯一消费点**(AC-13-A5)。
        /// <para>⚠️ 本方法**只读** DTO 的通道位 / 计数,**不推断病种、不改行为态**。</para></summary>
        public static PatientMaterial Material(in VitalsDto v)
        {
            // 空集 signs[] ⇒ 中性表现(GDD F-13.8 表「空集」行)—— 不做特判
            if (v.SignCount == 0) return PatientMaterial.Neutral;
            // 通道位 → 材质通道(词条→素材的解析住 13 的烘焙表;本 story 只签通道透传形状)
            return new PatientMaterial(v.SignChannelMask, v.SignCount);
        }
    }

    /// <summary>表现材质选择(F-13.8)—— **纯表现层**,不含任何决策语义。</summary>
    public readonly struct PatientMaterial
    {
        /// <summary>通道位(与 9 的 `SignChannel` 对齐)。</summary>
        public readonly int ChannelMask;
        /// <summary>词条计数(素材档选择用)。</summary>
        public readonly int Count;

        public PatientMaterial(int channelMask, int count) { ChannelMask = channelMask; Count = count; }

        /// <summary>空集 `signs[]` 的中性表现(GDD F-13.8:「无词条即无附加材质」)。</summary>
        public static PatientMaterial Neutral => new PatientMaterial(0, 0);
    }
}
