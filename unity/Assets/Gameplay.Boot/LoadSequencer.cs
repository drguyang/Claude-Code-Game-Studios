// 读档缝合序列器 —— 生产 Load 接线轮(2026-10-25 · TD-persist-04 义务② · 三裁全取推荐)。
//
// 权威来源:
//   AnchorCheckpointTrigger.cs:31 头注 —— 「生产 Load 接线落地时,读档路径必须调
//     `ResetCursor()`」(义务②;债册二十七订同文)。缝的机理:restore 换流后,
//     锚点游标若不归零,restore 与首次 Drain 之间落流的新事件(索引可能落在旧游标
//     之下)会被 Drain 的流尾对齐**静默跳过** —— 事件触发 checkpoint 半边就此哑一段。
//   TD-persist-04 义务② 接线轮三裁(2026-10-25 用户全取推荐):
//     ① 范围 = 窄缝合轮(全量派生态重建另立项,见下「未重建清单」);
//     ② 触发点 = 公开方法·通电待信(BootRoot.RequestLoad,零 UI 接线 —— UI 存档位
//        接线轮直接调,零代码改动激活;同 52 接线轮「通电待信」形态);
//     ③ CheckpointFallback 计数 = 读档后 `NotifyExternalCheckpoint()` 清零
//        (读档把世界回滚到存档时刻 ⇒ 读档点相当于「刚做过 checkpoint」;
//        不清零则读档后不久会触发一次冗余全量落盘 —— 无害但多余)。
//
// 调用序(本类的全部语义,Load 内三步):
//   1. `SaveService.Load(slot)` —— 读档本体(Gating 门 / 校验和 + bak 回退 / 迁移 /
//      重放进恢复闭包)。**抛 ⇒ 后两步不动**(原子序:读档失败不得动缝合侧状态)。
//   2. `_anchorTrigger.ResetCursor()` —— 义务② 判据(见上)。
//   3. `_fallback?.NotifyExternalCheckpoint()` —— 裁定③(fallback 可 null:与三件
//      存档侧组件同源装配,saveBasePath 为 null ⇒ 全 null ⇒ 本类也不构造)。
//
// ⚠️ **派生态未重建清单(裁定① 窄轮的如实登记,禁借绿)** —— 读档 Restore 只重建
//   三流与折叠行,以下派生态**不在本轮重建面**:
//   · 在场视图(PresenceRegistry):stream.Restore 的恢复闭包刻意绕 CAP 的原因
//     (读档时在场视图未重建,CAP 必误抛)—— 全量重建归独立大轮(涉 ADR-008/023 口径);
//   · PatientAppearedDriver 的 `_lastSpawnTick` / `_spawnedCount` 内存态;
//   · PatientVisualSpawner 的视觉侧;
//   · RandomEventsDirectorDriver 的 `_lastDrivenTick`(读档后 tick 回退 < 该值时
//     `OnTickEdge` 的同 tick 防御不拦 —— 判等只拦相等,更小照常驱动,无累积危害);
//   · 其余流读组件(经 `() => stream.Events` 闭包取流)自动适应换流,零缝合。
//
// ⚠️ **通电待信(裁定②)**:生产今日零调用方 —— `RequestLoad` 无 UI / 调试入口调用。
//   playtest「无法读档」不是缺陷登记,是本形态的诚实表现;存档位 UI 接线轮落地即激活。

using System;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 读档缝合序列器:SaveService.Load 成功后依序复位锚点游标(义务②)与兜底计数(裁定③)。
    /// </summary>
    public sealed class LoadSequencer
    {
        private readonly SaveService _saveService;
        private readonly AnchorCheckpointTrigger _anchorTrigger;
        private readonly CheckpointFallback _fallback;

        /// <param name="saveService">读档本体(生产 = 组合根装配实例)。</param>
        /// <param name="anchorTrigger">锚点触发器(读档后 <see cref="AnchorCheckpointTrigger.ResetCursor"/>;
        /// 与 saveService 同源装配 —— 规则十四「一个量」共享同一瞬时源)。</param>
        /// <param name="fallback">定时兜底(读档后 <see cref="CheckpointFallback.NotifyExternalCheckpoint"/>;
        /// 可 null —— saveBasePath 为 null 时三件同 null,本类也不构造)。</param>
        /// <exception cref="ArgumentNullException">saveService 或 anchorTrigger 为 null。</exception>
        public LoadSequencer(SaveService saveService,
                             AnchorCheckpointTrigger anchorTrigger,
                             CheckpointFallback fallback = null)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _anchorTrigger = anchorTrigger ?? throw new ArgumentNullException(nameof(anchorTrigger));
            _fallback = fallback;
        }

        /// <summary>本序列器消费的锚点触发器(同源钉观测面 —— 组合根测试断言它 == 服务袋实例)。</summary>
        public AnchorCheckpointTrigger AnchorTrigger => _anchorTrigger;

        /// <summary>
        /// 读档 + 缝合(调用序见类头注;<see cref="SaveService.Load"/> 抛 ⇒ 缝合侧不动)。
        /// </summary>
        /// <param name="slot">读档槽位(Gating 门 / 校验和回退 / 迁移均在 Load 内)。</param>
        public void Load(SaveSlot slot)
        {
            _saveService.Load(slot);
            _anchorTrigger.ResetCursor();
            _fallback?.NotifyExternalCheckpoint();
        }
    }
}
