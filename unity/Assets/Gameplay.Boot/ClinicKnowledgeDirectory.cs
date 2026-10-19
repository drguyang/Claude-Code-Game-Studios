// story-021 —— `knowsClinic` 注入缝的生产真值派发面(AC-021-1)。
//
// 权威来源:
//   design/gdd/patient-ai.md F-13.2 —— `KnowsClinic(p) := ClinicKnownByRegion(HomeRegion(spawn_anchor(p)))`
//   ADR-016 §一(重建三源不变量:行为输入必须可从「事件流 / 版本化烘焙数据 / 二者纯函数」重构)
//   TD-audio-033 ①(装配层此前逐字写 `knowsClinic: false` ⇒ 全部病人永不进 Seeking)
//
// ⚠️ **本类不自造规则** —— 它只做三件事:① 取病人锚点格;② 转调 13 的 `ClinicKnowledge`;
//   ③ 按病人**首见缓存** `HomeRegion`(F-13.2「只在初始化时求一次」;`spawn_anchor` 是静态定义)。
//   真值源 = 13 的 `ClinicKnowledge` + 6 的 `EcozoneRegistry`(经 `RegistryClinicKnowledgeSource`),
//   本类只是把它们接成一个 `Func<PatientId, bool>` 给 `PatientCueDriver`(及视觉侧,TD-audio-033 ② 收敛时)。
//
// ⚠️ **无锚点格的病人 ⇒ false**,这是**如实表达**而非发明:没有锚点格就没有 `HomeRegion`,
//   F-13.2 无定义 ⇒ 判「不知道」= 最保守的读法(与 `BehaviorMap` 的
//   「`knowsClinic` 为假 ⇒ 不进 Seeking」同向,不会替玩家制造寻诊)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>`PatientId → KnowsClinic(p)` 的生产派发面(F-13.2 的接线期实装;story-021)。
    /// <para><see cref="KnowsClinic"/> 与 `Func&lt;PatientId, bool&gt;` 签名一致 ⇒ 组合根写
    /// <c>knowsClinic: directory.KnowsClinic</c>(方法组转换)。</para>
    /// </summary>
    public sealed class ClinicKnowledgeDirectory
    {
        private readonly ClinicKnowledge _knowledge;
        private readonly Func<PatientId, (bool found, WorldPos cell)> _locate;
        // 病人 → `HomeRegion`(首见求一次;F-13.2「只在初始化时求一次」)。
        private readonly Dictionary<int, EcozoneId> _homeRegion = new Dictionary<int, EcozoneId>();

        /// <summary>构造。</summary>
        /// <param name="knowledge">13 的 F-13.2 纯函数装配器(非 null)。</param>
        /// <param name="locate">病人 → (是否有格, 锚点格)(非 null;生产由组合根以 lambda 包装
        /// <c>presence.TryGetCell</c> —— 带 <c>out</c> 形参的方法组不能直接转成元组委托)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ClinicKnowledgeDirectory(ClinicKnowledge knowledge,
                                        Func<PatientId, (bool found, WorldPos cell)> locate)
        {
            _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            _locate = locate ?? throw new ArgumentNullException(nameof(locate));
        }

        /// <summary>已缓存 `HomeRegion` 的病人数(可观测面 —— 读档收敛轮须 `ResetForLoad`)。</summary>
        public int KnownPatientCount => _homeRegion.Count;

        /// <summary>`KnowsClinic(p)`(F-13.2)—— 取锚点格 → 首见求 `HomeRegion` → `ClinicKnownByRegion`。
        /// <para>锚点取自注入缝(不读表现态位置,ADR-016 §三);无锚点 ⇒ false(见文件头注)。</para></summary>
        /// <param name="patientId">病人。</param>
        public bool KnowsClinic(PatientId patientId)
        {
            var (found, cell) = _locate(patientId);
            return found && KnowsClinicAt(patientId, cell);
        }

        /// <summary>`KnowsClinic(p)` —— 调用方已持有锚点格时走此重载(不再回调定位缝)。</summary>
        /// <param name="patientId">病人。</param>
        /// <param name="spawnAnchor">锚点格(`spawn_anchor(p)`;ADR-016 §三 粗粒度整数格)。</param>
        public bool KnowsClinicAt(PatientId patientId, WorldPos spawnAnchor)
        {
            if (!_homeRegion.TryGetValue(patientId.Value, out EcozoneId region))
            {
                region = _knowledge.HomeRegion(spawnAnchor);
                _homeRegion[patientId.Value] = region;
            }
            return _knowledge.ClinicKnownByRegion(region);
        }

        /// <summary>加载后重置(派生态重新灌入;承既有导演 `ResetForLoad` 同口径)。</summary>
        public void ResetForLoad() => _homeRegion.Clear();
    }
}
