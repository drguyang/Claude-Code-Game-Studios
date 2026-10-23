// ============================================================================
// SkillGrown 事件发射器 —— Story 007
// 权威来源: production/epics/skill-system/story-007-skillgrown-event-emit.md
//   · AC-1 ~ AC-6(EmitGrowth / 载荷结构 / Level 哨兵 / patient_id / 全序键 / 无 disease_id)
//   · ADR-007 §一(主机唯一 Append) · ADR-009 §三(三流全序键)
//   · ADR-024 SkillGrown 已在 entities.yaml 具名登记(stream: history, author: 30)
//   · ADR-005 确定性 sim(纯函数构造,无随机)
//   · ADR-025 Sim.Contracts 引用集恰 = BCL,不可引 Sim.Codec
// ============================================================================
// EmitGrowth 不直接调用 IEventSink.Append —— 它构造 SkillGrownPayload 交调用方(主机),
// 由主机负责:① PayloadCodec.Encode 编码 → blob 池;② 构造 SimEvent + 填充 PayloadRef;
// ③ Append 入病史流。30 不监听事件流,不主动触发成长,只提供发射接口。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// SkillGrown 事件发射器。唯一对外入口 = <see cref="EmitGrowth"/>,
    /// 返回 <see cref="SkillGrownPayload"/>,由调用方(主机)编码 + 构造 SimEvent + Append。
    /// <para>载荷结构 = (actor_id, patient_id, skill_id, object_id, novelty_class, Level, xp_gain)。</para>
    /// <para>Level: 升级时 = 新等级(绝对等级); 未升级时 = 哨兵 -1(非 0)。</para>
    /// <para>XpGain: **每次成长都携带**(丙案 2026-10-23)—— 与 Level 正交,前者答「现在几级」,
    /// 后者答「这一下赚了多少」。重建 XP 余量靠它,不再靠快照。</para>
    /// <para>patient_id: 无病例语境 = PatientId.None(-1); 8 诊断传入具体病例 id。</para>
    /// </summary>
    public static class SkillGrownEmitter
    {
        /// <summary>构造 SkillGrown 载荷(不编码、不构造 SimEvent、不 Append,由调用方完成)。</summary>
        /// <param name="actorId">玩家/实体 id。</param>
        /// <param name="skillId">技能 id(SkillId ordinal)。</param>
        /// <param name="objectId">对象 id(病种/品种/对手类型 ordinal)。</param>
        /// <param name="novelty">新颖度类别。</param>
        /// <param name="level">升级时 = 新等级; 未升级 = null(写入哨兵 -1)。</param>
        /// <param name="currentTick">当前 tick(来自 ITickProvider)。</param>
        /// <param name="patientId">成长所依附的受伤实体 id; 无病例语境 = PatientId.None。</param>
        /// <param name="xpGain">本次所得经验(Q16.16;由调用方按
        /// <c>XP_gain = BASE × K_difficulty × K_novelty</c> 算好后推入 —— 30 不拉取,GDD `:132`)。
        /// <para>⚠️ <b>本重载无默认值</b> —— <c>xpGain</c> 是**必填**。省略它的调用走下方
        /// 7 参重载(按基准难度 <see cref="Fix.One"/> 求值,丙-3 口径)。</para></param>
        /// <returns>SkillGrownPayload 结构体(调用方负责 PayloadCodec.Encode + SimEvent 构造 + Append)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">任何 id 为负、novelty 不在有效范围、
        /// currentTick 为负,或 <paramref name="xpGain"/> 为负(负经验 = 调用方算错,fail-loud)。</exception>
        public static SkillGrownPayload EmitGrowth(int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick, PatientId patientId,
            Fix xpGain)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId),
                    $"actorId={actorId} 不能为负");
            if (skillId < 0)
                throw new ArgumentOutOfRangeException(nameof(skillId),
                    $"skillId={skillId} 不能为负");
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId),
                    $"objectId={objectId} 不能为负");
            if ((int)novelty < 0 || (int)novelty > (int)NoveltyClass.Normal)
                throw new ArgumentOutOfRangeException(nameof(novelty),
                    $"NoveltyClass ordinal {(int)novelty} 不在有效范围 [0, {(int)NoveltyClass.Normal}]");
            if (currentTick < 0)
                throw new ArgumentOutOfRangeException(nameof(currentTick),
                    $"currentTick={currentTick} 不能为负");
            if (xpGain.Raw < 0)
                throw new ArgumentOutOfRangeException(nameof(xpGain),
                    $"xpGain raw={xpGain.Raw} 不能为负(XP_gain 三因子均非负 ⇒ 负值 = 调用方算错)");

            int resolvedLevel = level ?? SkillGrownPayload.LevelNotGrown; // 未升级 → 哨兵
            int noveltyOrdinal = (int)novelty;

            return new SkillGrownPayload(actorId, patientId.Value, skillId, objectId,
                noveltyOrdinal, resolvedLevel, xpGain);
        }

        /// <summary>
        /// <b>便捷重载(丙-3 口径)</b>:<c>xpGain</c> 由本方法按**基准难度 <see cref="Fix.One"/>**
        /// 算得 —— <c>XP_gain = BASE(skillId) × 1.0 × K_novelty(novelty)</c>。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么缺省难度 = 1.0 而不是省略 <c>XpGain</c></b>(用户 2026-10-23 裁定 **丙-3**):
        /// 本重载的服务对象是**发射时点拿不到真实难度**的写者 —— 典型 = 8 诊断的查体投影
        /// (`SeenDiseaseProjector.OnExamined`):查体那一刻玩家**尚未落笔诊断**,「对错」无从得知,
        /// 而 GDD `:494` 规定错诊 `×0.5 K_difficulty`。若在此**猜**一个难度,就是把未定的量写死。</para>
        /// <para>⇒ 取**基准 1.0**(= GDD `:353` 区间 `1.0 ~ 3.0` 的下端),使
        /// ① 诊断成长**照常涨经验**(GDD `:238`「不论对错」);
        /// ② 错诊的 `×0.5` **不静默跳过、也不被伪造** —— 登记为具名债
        /// `30-诊断错诊难度未落`(归 8 的 GDD 轮,须把发射点后移到「落笔诊断」时刻)。</para>
        /// <para>⚠️ **能拿到真实难度的写者不得用本重载** —— 11 处方侧走 8 参重载
        /// (`AssemblySkillGrowthPort` 已在手 `kDifficulty`,2026-10-23 同批修掉「算了又丢」)。</para>
        /// </remarks>
        public static SkillGrownPayload EmitGrowth(int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick, PatientId patientId)
        {
            Fix xpGain = XpGainCalculator.ComputeXpGain(skillId, novelty, Fix.One);
            return EmitGrowth(actorId, skillId, objectId, novelty, level, currentTick,
                patientId, xpGain);
        }

        /// <summary>便捷重载: patientId 默认 PatientId.None(无病例语境)。</summary>
        public static SkillGrownPayload EmitGrowth(int actorId, int skillId, int objectId,
            NoveltyClass novelty, int? level, long currentTick)
        {
            return EmitGrowth(actorId, skillId, objectId, novelty, level, currentTick,
                PatientId.None);
        }
    }
}
