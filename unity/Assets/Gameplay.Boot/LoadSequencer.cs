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
// 调用序(本类的全部语义,Load 内五步;前四步之外第 5 步为 2026-10-31 追加):
//   1. `SaveService.Load(slot)` —— 读档本体(Gating 门 / 校验和 + bak 回退 / 迁移 /
//      重放进恢复闭包)。**抛 ⇒ 后四步不动**(原子序:读档失败不得动缝合侧状态)。
//   1.5 🔁 **2026-11-04 新增:`restoreTick(_saveService.LastLoadedTick)` —— 逻辑时钟定位。**
//      病因(用户实测「再进入已有存档,console 很多红色报错」):`ITickProvider` 只读、
//      `SaveService` 又把档头 tick 丢掉 ⇒ 读档后驱动仍停在 0,而三流已到最后一条事件的 tick
//      ⇒ 之后每笔 sim 写入都落在流尾之前,`EventStream.Append` 以 `BCD-码-3` 逐条拒收:
//      刷屏 + **世界形同停摆**。⚠️ 次序在 `afterRestore` 之前(派生态重建读当前 tick)。
//   2. `_anchorTrigger.ResetCursor()` —— 义务② 判据(见上)。
//   3. `_fallback?.NotifyExternalCheckpoint()` —— 裁定③(fallback 可 null:与三件
//      存档侧组件同源装配,saveBasePath 为 null ⇒ 全 null ⇒ 本类也不构造)。
//   4. `_afterRestore?.Invoke()` —— 派生态重建(2026-10-27 新增;见构造器该形参 doc)。
//   5. **配置版本比对与记录**(🔁 2026-10-31 新增 · `AC-7a-14` 执行体)——
//      把档头 `ConfigVersion` 与当前烘焙产物版本比对,结果交注入的记录面。
//      **非致命**(ADR-010 §七):不一致只记录,不改流、不重算、不抛。
//      ⚠️ 此前**全仓零**执行体:档头的值在 `SaveService.Load` 里读进来就地丢弃 ⇒
//      「仅记录并供排查」没有任何东西在做。⚠️ 生产可达性 = 本类本身的可达性
//      (裁定② 通电待信,`BootRoot.RequestLoad` 今日零调用方)—— **不主张「读档时已记录」**,
//      主张「读档路径一旦被调用即记录」。
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
//   🔁 **2026-10-31**:存档位屏的确认动作已接到 `SaveSlotCommandRouter` → 本类
//   (`save_slot_load_wiring_test` 6 条)。**但屏本身开不出来**(42 无运行期换屏入口,
//   装配层零消费 `ModalId`)⇒ 「零调用方」在**运行期**仍成立,缺的是模态开启器,
//   不是本类这一环。
//   playtest「无法读档」不是缺陷登记,是本形态的诚实表现;存档位 UI 接线轮落地即激活。

using System;
using System.Collections.Generic;               // IReadOnlyList(ADR-037 逐份版本)
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;                 // ConfigVersionCheck(AC-7a-14 记录面)

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
        private readonly Action _afterRestore;

        /// <summary>读档后把逻辑时钟定位到存档 tick(可 null)。见 <see cref="Load"/> 第 1.5 步。</summary>
        private readonly Action<long> _restoreTick;
        /// <summary>当前烘焙产物的配置版本快照(组合值 + 逐份版本;AC-7a-14 / ADR-037 §⑦);
        /// 与 <see cref="_configVersionRecorder"/> **同生同灭**(见构造器 all-or-nothing 校验)。</summary>
        private readonly Func<ConfigVersionSnapshot> _currentConfigVersion;
        /// <summary>比对结果的记录面(AC-7a-14·ADR-010 §七「记录并供排查」)。</summary>
        private readonly Action<ConfigVersionCheck, ConfigVersionDiff> _configVersionRecorder;

        /// <param name="saveService">读档本体(生产 = 组合根装配实例)。</param>
        /// <param name="anchorTrigger">锚点触发器(读档后 <see cref="AnchorCheckpointTrigger.ResetCursor"/>;
        /// 与 saveService 同源装配 —— 规则十四「一个量」共享同一瞬时源)。</param>
        /// <param name="fallback">定时兜底(读档后 <see cref="CheckpointFallback.NotifyExternalCheckpoint"/>;
        /// 可 null —— saveBasePath 为 null 时三件同 null,本类也不构造)。</param>
        /// <param name="afterRestore">**读档完成后**的派生态重建钩子(可 null)。
        /// <para>🔁 <b>2026-10-27 新增(S20-10 B-1 接线)</b>:本类头注的「派生态未重建清单」
        /// 此前**不含库存投影** —— `InventoryFold.Fold` 因此在生产侧零调用点、契约 C-1/C-6
        /// 零兑现面。<b>库存投影是纯派生态**(ADR-009 §四:由事件流确定性重建、不持状态、
        /// 不写流),与在场视图 / 视觉侧同格,故归本序列器的读档后钩子。</para>
        /// <para>⚠️ <b>次序 = 在 <c>ResetCursor</c> 之后</b>:流已在 <c>SaveService.Load</c>
        /// 内 `Restore` 完毕,钩子读的是**新流**。若钩子抛 ⇒ 读档整体失败(投影重建不了
        /// 就说明存档有问题,静默吞掉会让下游拿到旧投影 —— 那是更坏的失败)。</para></param>
        /// <exception cref="ArgumentNullException">saveService 或 anchorTrigger 为 null。</exception>
        /// <param name="currentConfigVersion">
        /// 当前烘焙产物的配置版本**快照**提供者(`ConfigVersionSnapshot` = 组合值 + 逐份版本,
        /// 二者同源,ADR-037 §⑦)。
        /// <para>🔁 <b>2026-10-31 新增(AC-7a-14 执行体)</b>:读档成功后须把档头携带的
        /// `ConfigVersion` 与它比对并**记录**(ADR-010 §七「存档头记 `ConfigVersion`
        /// 以便排查回放不符」)。此前全仓没有任何路径做过这次比对 —— 档头的值读进
        /// `SaveService.Load` 就地丢弃,AC-7a-14 的末句因此是 `NOT-RUN`。</para>
        /// <para>⚠️ `null` 与 <paramref name="configVersionRecorder"/> `null` **必须成对**:
        /// 只给一个 ⇒ 构造即抛(见下)。两者皆 null = 本实例**不承担**记录义务
        /// (今日仅测试的部分装配如此;生产装配恒给)。</para>
        /// </param>
        /// <param name="configVersionRecorder">比对结果的记录面(非 null 时,每次**成功**读档
        /// 恰好调用一次)。不匹配**非致命** —— 本类不因 `Match == false` 而抛、不改流、不重算。
        /// <para>⚠️ 记录器**自身**抛 ⇒ 读档整体失败(与 <paramref name="afterRestore"/> 同口径):
        /// 吞掉它等于「说好要记录却什么都没记」,排查时会被读成「版本一致」。</para>
        /// </param>
        /// <exception cref="ArgumentNullException">saveService 或 anchorTrigger 为 null。</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="currentConfigVersion"/> 与 <paramref name="configVersionRecorder"/>
        /// **只给了一个** —— 半对配置的两种形态都只会静默地什么也不做。
        /// </exception>
        public LoadSequencer(SaveService saveService,
                             AnchorCheckpointTrigger anchorTrigger,
                             CheckpointFallback fallback = null,
                             Action afterRestore = null,
                             Func<ConfigVersionSnapshot> currentConfigVersion = null,
                             Action<ConfigVersionCheck, ConfigVersionDiff> configVersionRecorder = null,
                             Action<long> restoreTick = null)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _anchorTrigger = anchorTrigger ?? throw new ArgumentNullException(nameof(anchorTrigger));
            _fallback = fallback;
            _afterRestore = afterRestore;
            // all-or-nothing:`currentConfigVersion` 缺 ⇒ 记不了;`recorder` 缺 ⇒ 记了没处去。
            // 两种半对都**不会**报错地退化成「不记录」,而「不记录」与「版本一致」不可区分。
            if ((currentConfigVersion == null) != (configVersionRecorder == null))
                throw new ArgumentException(
                    "AC-7a-14:currentConfigVersion 与 configVersionRecorder 必须成对给出" +
                    $"(本次 currentConfigVersion = {(currentConfigVersion == null ? "null" : "非 null")}," +
                    $" configVersionRecorder = {(configVersionRecorder == null ? "null" : "非 null")})。" +
                    "半对配置会静默退化成「不记录」—— 那与「两版本一致」在外部观测上不可分");
            _currentConfigVersion = currentConfigVersion;
            _configVersionRecorder = configVersionRecorder;
            _restoreTick = restoreTick;
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

            // ── 1.2 回退 `.bak` 的记录(🔁 2026-11-04 · `TD-persist-22` 日志半边)──
            // 病因:`SaveService.LoadBytes` 回退 `.bak` 时**零日志**(门 A 无日志面)⇒ 玩家
            //   以为读的是最新档,实际是**上一版**,丢的那段进度没有任何痕迹。
            // 位置 = 本类:`BootRoot.RequestLoad`(「继续诊治」)与 `SaveSlotCommandRouter.TryLoad`
            //   (存档册「启封」)是**两个入口**,唯一共同下游就是这里 —— 记在此处两者都覆盖。
            // ⚠️ **级别 = Log,不是 Warning / Error**:抢救是设计内行为(ADR-041 §D4),不是故障;
            //   但它是**异常态**,所以要看得见。⚠️ 文案归 7a(存储层事实),不是给玩家的提示语
            //   —— 玩家侧要不要告知、怎么告知归 42(`TD-persist-22` 的册子半边,仍 open)。
            if (_saveService.LastLoadSource == SaveLoadSource.Bak)
            {
                UnityEngine.Debug.Log(
                    "[7a][载入] 槽位 " + slot.SlotSeq + " 的正式档" +
                    (_saveService.LastLoadFallback == SaveLoadFallback.FinalCorrupt
                        ? "校验和不通过(内容被写坏)"
                        : "缺失") +
                    " ⇒ 已回退 .bak:**本次读到的是上一版**。盘上正式的坏文件**未修复**" +
                    "(GDD 规则十一:损坏分支零 I/O)—— 下一次成功写入该槽时才会被覆盖。");
            }

            // ── 1.5 逻辑时钟定位(2026-11-04 · 用户报「再进入已有存档,console 很多红色报错」)──
            // 病因:`ITickProvider` 只读、`SaveService` 又把档头 tick 丢掉 ⇒ 读档后驱动仍停在
            //   0(或旧值),而三流已恢复到最后一条事件的 tick ⇒ 之后每笔 sim 写入都早于流尾,
            //   `EventStream.Append` 以 BCD-码-3(Tick 非降)**逐条拒绝**:console 刷屏,
            //   且**sim 一个事件都写不进去**(读进来的世界形同停摆)。
            // ⚠️ **次序** = 在 `afterRestore` **之前**:派生态重建读的是当前 tick。
            _restoreTick?.Invoke(_saveService.LastLoadedTick);

            _anchorTrigger.ResetCursor();
            _fallback?.NotifyExternalCheckpoint();
            // 🔁 **2026-10-27 新增(S20-10 B-1 接线)** —— 第 4 步:派生态重建。
            // ⚠️ **次序 = 最后**:前三步都是本类原有的缝合义务,与流内容无关;
            //    钩子读的是**新流**,放最后可保证它看到的流已是最终态(ResetCursor 不改流)。
            // ⚠️ **钩子抛 ⇒ 读档整体失败**(不 catch):投影重建不了说明存档或静态数据
            //    有问题,静默吞掉会让下游拿到旧投影 —— 那是更坏的失败(同 `SaveService.Load`
            //    的 fail-loud 口径)。
            _afterRestore?.Invoke();
            // 🔁 **2026-10-31 新增(AC-7a-14 执行体)** —— 第 5 步:配置版本比对与记录。
            // ⚠️ **次序 = 最后**:前四步任一抛 ⇒ 读档整体失败 ⇒ **不得**留下一条「读过档」的记录
            //    (记录一旦落下,排查者会以为这份档已经装进去了)。
            RecordConfigVersionOnLoad();
        }

        /// <summary>
        /// AC-7a-14 · ADR-010 §七:把档头 `ConfigVersion` 与当前产物版本比对,结果交记录面。
        /// <para><b>非致命</b>:两版本不同**不**抛、**不**改流、**不**重算任何已发生事件
        /// —— 已发生事件不受影响是 ADR-010 §七 的明文裁决,后续窗口用新配置由各系统自行装载。</para>
        /// <para>⚠️ 未装配记录面(`_configVersionRecorder == null`)⇒ 直接返回 —— 这是
        /// **部分装配**(测试 / 未接 51 的开发期形状)的显式形态,由构造器的成对校验钉住:
        /// 生产装配要么带完整记录面,要么两件都不给。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 已装配记录面,但 <see cref="SaveService.LastLoadedConfigVersion"/> 为 null ——
        /// 说明「<c>SaveService.Load</c> 完整返回了却没有交出档头版本」,那是 codec 侧的契约破坏。
        /// 静默跳过会让「记录了一次」与「根本没记录」不可区分,故 fail-loud。
        /// </exception>
        private void RecordConfigVersionOnLoad()
        {
            if (_configVersionRecorder == null) return;

            uint? saveSide = _saveService.LastLoadedConfigVersion;
            if (!saveSide.HasValue)
                throw new InvalidOperationException(
                    "AC-7a-14:SaveService.Load 已返回,但 LastLoadedConfigVersion 为 null —— " +
                    "读档路径没有把档头 ConfigVersion 交出来(codec 契约破坏);拒绝以「无记录」冒充「版本一致」");

            ConfigVersionSnapshot current = _currentConfigVersion();
            var check = new ConfigVersionCheck(saveSide.Value, current.Combined);

            // ADR-037 §④:逐表归因。**三种结论不可混读** ——
            //   · 本档无逐份段(count=0,旧档)/ 本方无逐份数据 ⇒ Unknown(「无法归因」);
            //   · 两侧都有 ⇒ Compare(可能是空集 = 逐份全等)。
            // ⚠️ 不得把 Unknown 降级成「无差异」—— 那正是本件要消灭的假绿形态。
            IReadOnlyList<ArtifactConfigVersion> savedArtifacts =
                _saveService.LastLoadedArtifactConfigVersions;
            ConfigVersionDiff diff =
                savedArtifacts == null || savedArtifacts.Count == 0 || current.Artifacts == null
                    ? ConfigVersionDiff.Unknown
                    : ConfigVersionDiff.Compare(savedArtifacts, current.Artifacts);

            _configVersionRecorder(check, diff);
        }
    }
}
