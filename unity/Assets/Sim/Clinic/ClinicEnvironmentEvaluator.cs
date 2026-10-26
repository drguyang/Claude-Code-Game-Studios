// 权威来源:
//   GDD `design/gdd/clinic-machine.md`
//     · States and Transitions `:123-127` —— 「24 没有自有状态机,它是纯函数派生」;
//       缓存 = **表现层 Memoize**,不是状态;「无内部缓存、无语境隐藏状态」。
//     · EC-24-05 `:291` —— 缓存失效:以 `Structure*` 事件为失效键;缓存与否不改结果。
//     · AC-24-04 `:404` —— 同一布局同进程两次求值,乘子**逐位一致**(EditMode 双求值对拍;
//       明确**不**断言跨平台 —— 跨平台归 ADR-012 黄金夹具批)。
//     · 规则一–五 `:71-127`(析出 → 类型判定 → 情境 → 乘子 → 交付)
//   ADR-005(确定性模拟:全部输出 = 输入的函数,零隐藏状态)
//   ADR-016 §一(三源不变量:决策 / 派生量的全部输入 ∈ {事件流, 版本化烘焙数据, 二者纯函数}
//     —— 本件的输入**恰**是「布局快照(事件流的当下投影)」+「四份 cooked 表(版本化烘焙数据)」,
//     无第四来源)
//   ADR-015 §三(单一整数格)· §五(4 邻接判定,非物理 / 非 NavMesh)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts} ⇒ 门 A:本文件零 `UnityEngine`)
//   story `production/epics/clinic-machine/story-005-purity-replay-memoize.md`
//     §Implementation Notes 2(contexts[] 输出顺序钉死为确定性序)· 5(24 不感知 tick 频率)
//
// ⚠️ **本件是「组合」不是「算法」**:析出归 `ClinicRoomExtraction`(story-002)、
//   类型判定归 `ClinicRoomMatcher`(story-006)、乘子归 `ClinicModifierEvaluator`(story-003)、
//   表归 `ClinicMachineTables`(story-001)。本件**零新增判据**,只把四者按 GDD 规则一–五
//   的顺序接成**一个**纯函数入口 —— 存在的理由是 story-005 的三条判据(双求值对拍 /
//   输入封闭 / 重放等价)需要一个**具名的求值函数**作观测对象,否则只能断言「三个函数各自是
//   纯的」,断言不到「这条链整体是纯的」。
//
// ⚠️ **输入封闭(本件最硬的一条)**:`Evaluate` 的形参**恰**五件 ——
//   ① 布局快照(`IPlacedStructureReadView`,23 的只读投影)
//   ②–⑤ 四份 cooked 表句柄(`ClinicFurnitureCatalogue` / `ClinicRoomTable` /
//        `ClinicContextTable` / `ClinicAdjTable`)
//   **刻意不收**:`long tick`、`SimEvent`、墙钟、`UnityEngine.Random`、任何表现态位置。
//   ⇒ 24 不感知 tick 频率(卡面 §Implementation Notes 5):改 `TICK_SECONDS` 不会改变本件的
//   任何输出。判据由 `clinic_purity_replay_test` 的**类型面断言**机械执行(反射扫形参类型),
//   不是靠本条注释的自我声明。
//
// ⚠️ **无状态(可断言,非口头约定)**:本文件只含 `static` 纯函数与 `readonly struct`;
//   零 static 可变字段、零实例字段。同一条由 `clinic_purity_replay_test` 的反射扫描
//   对 `DaYiJingCheng.Sim.Clinic` 命名空间下**全部**类型执行(不只本文件)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 系统 24 一次完整求值的**输出快照**(纯函数产物;无缓存、无隐藏状态)。
    /// <para>三段语义各不相同,故并列给出:<see cref="Extraction"/> 回答「有没有析出房间」,
    /// <see cref="Matched"/> / <see cref="RoomType"/> 回答「是哪个类型」,
    /// <see cref="Diagnostics"/> 回答「为什么是这个 / 为什么没有」。</para>
    /// <para>⚠️ <b>未匹配(<see cref="Matched"/> == false)时三个乘子仍是合法值</b>:
    /// <see cref="EnvMod"/> / <see cref="EquipMod"/> = 0(EC-24-01 / 02 / 07 的中性出口),
    /// <see cref="KContextMax"/> 仍为表派生值 —— 它与布局**无关**(F-24-3 是表上界),
    /// 不因「无房间」而归零。把两者混为一谈会让「空医馆」与「表缺失」不可区分。</para>
    /// </summary>
    public readonly struct ClinicEnvEvaluation
    {
        private static readonly IReadOnlyList<string> EmptyContexts = Array.Empty<string>();

        /// <summary>房间析出结果(story-002)。</summary>
        public readonly ClinicRoomExtractionResult Extraction;

        /// <summary>类型匹配诊断(多匹配个数 / 未知子句类型引用条数)。</summary>
        public readonly ClinicRoomMatchDiagnostics Diagnostics;

        /// <summary>是否匹配到房间类型(= <c>ClinicRoomMatcher.TryMatchRoom</c> 的返回值)。</summary>
        public readonly bool Matched;

        /// <summary>匹配到的房间类型 id;`ROOM_NONE` ⇒ <c>null</c>。</summary>
        public readonly string RoomType;

        /// <summary>`EnvMod` 医馆分量(Q16.16 raw,**未钳制** —— 唯一钳制点在 21a F1,D-21-31)。</summary>
        public readonly Fix EnvMod;

        /// <summary>`EquipMod`(Q16.16 raw,非负,已在 F-24-2 单点钳入 [0, `EQUIP_MOD_CAP`])。</summary>
        public readonly Fix EquipMod;

        /// <summary>`K_CONTEXT_MAX` = `max(CONTEXT_TABLE 的 K_speed)`(F-24-3,**表派生**,与布局无关)。</summary>
        public readonly Fix KContextMax;

        /// <summary>
        /// 本次求值涉及的情境 id 集(**规范序**:`StringComparer.Ordinal` 升序)。
        /// <para>⚠️ **P0 至多 1 元素**:P0 是单房间单情境(EC-24-06 的多房间邻近 / 情境叠加归 P1a),
        /// 而 `CONTEXT_TABLE` 的构造器保证 `room_type` 唯一 ⇒ 匹配房间恰对应一行。
        /// 集合形态与 ordinal 排序是为 P1a 的多情境预留(那时同一房间可有多个情境行)。</para>
        /// <para>⚠️ **空集 = 空数组,不是 null**(承 `ClinicEnvDto.ContextIds` 的同一契约):
        /// 未匹配 / 未析出房间 ⇒ 空数组。</para>
        /// </summary>
        public readonly IReadOnlyList<string> ContextIds;

        /// <summary>构造一份求值快照。</summary>
        /// <param name="extraction">析出结果。</param>
        /// <param name="diagnostics">匹配诊断。</param>
        /// <param name="matched">是否匹配到房间类型。</param>
        /// <param name="roomType">房间类型 id(`ROOM_NONE` ⇒ null)。</param>
        /// <param name="envMod">医馆环境分量(未钳制)。</param>
        /// <param name="equipMod">器具分量(已钳)。</param>
        /// <param name="kContextMax">表派生速限上界。</param>
        /// <param name="contextIds">情境 id 集(规范序;null ⇒ 空数组)。</param>
        public ClinicEnvEvaluation(in ClinicRoomExtractionResult extraction,
                                   in ClinicRoomMatchDiagnostics diagnostics,
                                   bool matched, string roomType,
                                   Fix envMod, Fix equipMod, Fix kContextMax,
                                   IReadOnlyList<string> contextIds)
        {
            Extraction = extraction;
            Diagnostics = diagnostics;
            Matched = matched;
            RoomType = roomType;
            EnvMod = envMod;
            EquipMod = equipMod;
            KContextMax = kContextMax;
            ContextIds = contextIds ?? EmptyContexts;
        }
    }

    /// <summary>
    /// 系统 24 的**完整求值入口**(GDD 规则一–五的单一组合点)—— 一个纯函数。
    /// <para>无状态:同一 (布局快照, 四份表) 两次求值 ⇒ 逐位相同的输出;不缓存、不持有引用、
    /// 不读墙钟 / 表现态 / tick(AC-24-04 · ADR-005 · ADR-016 §一)。</para>
    /// <para>⚠️ **缓存不在这里**。若上层要 Memoize,它属**表现层**
    /// (`Gameplay.Presentation.ClinicEnvironmentMemo`),失效键 = `Structure*` 事件的
    /// 全序键(EC-24-05);本类**永远**按传入的布局快照现算 —— 这是「缓存与否不改结果」的
    /// 结构性保证,不是纪律。</para>
    /// </summary>
    public static class ClinicEnvironmentEvaluator
    {
        /// <summary>
        /// 求值:布局快照 + 四份 cooked 表 ⇒ 房间析出 + 房间类型 + 三乘子 + 情境集。
        /// </summary>
        /// <param name="layout">23 的**只读布局快照**(本方法不持有其引用)。</param>
        /// <param name="furniture">21a 家具目录(`FURN_TYPES` 闭集 + 逐件 `tier` / `e_env`)。</param>
        /// <param name="rooms">24 `ROOM_TABLE`(房间类型判定谓词)。</param>
        /// <param name="contexts">24 `CONTEXT_TABLE`(情境 / `K_speed` / `base_env`)。</param>
        /// <param name="adj">24 `ADJ_TABLE`(邻接权重)。</param>
        /// <returns>求值快照(结构体,绝不为 null)。</returns>
        /// <exception cref="ArgumentNullException">任一入参为 null(接线错误,非设计态)。
        /// **本方法直接抛出**(体内五条守卫)。</exception>
        /// <exception cref="InvalidOperationException">某格属性查询返回
        /// <see cref="ClinicCellLookup.Unknown"/>(源不完整 —— 静默跳过会少算 `e_env` / 子句计数)。
        /// **propagated from** `ClinicRoomMatcher.TryMatchRoom` / `ClinicModifierEvaluator.*`
        /// —— 本方法不直接抛,由下游传播。</exception>
        /// <exception cref="OverflowException">线性求和在 int64 内越界(ADR-012 F7 的显式守卫)。
        /// **propagated from** `ClinicModifierEvaluator.ClinicEnvModOf` / `ClinicEquipModOf`
        /// —— 本方法不直接抛。</exception>
        /// <exception cref="ArgumentException">房间格数越出 `n_max`(F-24-1 的 int64 域前置)。
        /// **propagated from** `ClinicRoomExtraction.Extract` —— 本方法不直接抛。</exception>
        public static ClinicEnvEvaluation Evaluate(
            IPlacedStructureReadView layout,
            ClinicFurnitureCatalogue furniture,
            ClinicRoomTable rooms,
            ClinicContextTable contexts,
            ClinicAdjTable adj)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (adj == null) throw new ArgumentNullException(nameof(adj));

            // ── 1. 格属性索引(布局快照 + 家具目录的纯派生;每次现建,不留引用)──
            var traits = new ClinicFurnitureTraitsSource(layout, furniture);

            // ── 2. 房间析出(真判据入口:type(c) ∈ FURN_TYPES,不是代理版)──
            ClinicRoomExtractionResult extraction =
                ClinicRoomExtraction.Extract(layout, furniture.FurnTypes);

            // ── 3. 房间类型判定(ROOM_TABLE 子句求值;多匹配 = 声明序首个,OQ-24-7 已裁)──
            bool matched = ClinicRoomMatcher.TryMatchRoom(
                extraction, traits, rooms, out string roomType,
                out ClinicRoomMatchDiagnostics diagnostics);

            // ── 4. 未匹配 ⇒ 中性出口(EC-24-01 / 02 / 07)──
            if (!matched)
                return Unmatched(extraction, diagnostics, contexts.KContextMax);

            // ── 5. 乘子求值 ──
            // base_env 由调用方按 room_type 解析(F-24-1 的输入;表缺失 ⇒ 0,表构造期已校验
            // 行集非空,故「匹配到类型但表里没有」只在装配漂移时出现 —— 取 0 而非抛,
            // 与 `TryGetBaseEnv` 的 bool 形契约一致)。
            Fix baseEnv = contexts.TryGetBaseEnv(roomType, out Fix resolved) ? resolved : Fix.Zero;

            Fix envMod = ClinicModifierEvaluator.ClinicEnvModOf(extraction, traits, adj, baseEnv);
            Fix equipMod = ClinicModifierEvaluator.ClinicEquipModOf(extraction, traits);

            // ── 6. 情境集(规范序)──
            IReadOnlyList<string> contextIds = CollectContextIds(roomType, contexts);

            return new ClinicEnvEvaluation(
                extraction, diagnostics, matched: true, roomType,
                envMod, equipMod, contexts.KContextMax, contextIds);
        }

        /// <summary>
        /// 未匹配 ⇒ 中性出口(EC-24-01 / 02 / 07):`EnvMod` / `EquipMod` 皆 0、`RoomType` 为 null、
        /// 情境集为空。
        /// <para>⚠️ `KContextMax` **不**随之归零 —— 它是表派生量,与布局无关(F-24-3)。
        /// 归零会让「空医馆」与「表缺失」不可区分。</para>
        /// </summary>
        private static ClinicEnvEvaluation Unmatched(
            in ClinicRoomExtractionResult extraction,
            in ClinicRoomMatchDiagnostics diagnostics,
            Fix kContextMax)
            => new ClinicEnvEvaluation(
                extraction, diagnostics, matched: false, roomType: null,
                Fix.Zero, Fix.Zero, kContextMax, EmptyContexts());

        /// <summary>未匹配出口用的空集(单例 —— 空数组本身不可变,共享安全)。</summary>
        private static IReadOnlyList<string> EmptyContexts() => Array.Empty<string>();

        /// <summary>
        /// 收集该房间类型的情境 id,**按 `StringComparer.Ordinal` 升序**。
        /// <para>⚠️ **排序不是冗余**:输入是 <see cref="ClinicContextTable.Rows"/> 的**产物行序**,
        /// 而行序是烘焙写方的一个实现细节(`ClinicMachineCookedWriter` 对行做
        /// `room_type` → `context_id` 的 ordinal 全序排序)。依赖它 = 让输出成为
        /// 「作者态 JSON 手写序」的函数,而不是「表内容的函数」——
        /// 承 ADR-016 §一 三源不变量的同一纪律(判据只依赖内容,不依赖容器序)。</para>
        /// <para>生产路径薄包装 —— 实际收集逻辑见
        /// <see cref="CollectContextIdsFromRows"/>(internal,供夹具直接喂行集)。</para>
        /// </summary>
        private static IReadOnlyList<string> CollectContextIds(string roomType, ClinicContextTable contexts)
            => CollectContextIdsFromRows(roomType, contexts.Rows);

        /// <summary>
        /// 从**任意行集**收集该房间类型的情境 id,按 `StringComparer.Ordinal` 升序。
        /// <para>⚠️ **为什么参数是行集而不是表**:<see cref="ClinicContextTable"/> 的构造器
        /// 拒 `room_type` 重复(`ClinicMachineTables.cs:124-127`,注释明写「每房间类型恰一情境行」)
        /// ⇒ 从**表**取行时,对给定 `roomType` 恒 ≤ 1 元素 ⇒ <c>ids.Sort</c> 在 P0 是
        /// **不可达的死代码**,任何变异都不改变输出(F7 存活即此)。</para>
        /// <para><b>2026-10-26 用户裁定(B 路 · 保留代码 + 补可证伪测试)</b>:不删排序
        /// (多情境是 P1a 的已裁形态 F-24-4,删了 P1a 要重写),而是把「收集逻辑」与
        /// 「表的构造期约束」解耦 —— 本方法收行集,夹具即可直接喂
        /// <b>多行同 `roomType`</b> 的行集,排序属性自此**可证伪**。
        /// 生产路径经 <see cref="CollectContextIds"/> 薄包装,行为零变化。</para>
        /// <para>可见性 = <c>internal</c> + <c>InternalsVisibleTo("Sim.Contracts.Tests")</c>
        /// (`Sim/AssemblyInfo.cs`),承 `Sim.Codec/FixCodec` 的既有先例 —— 生产面不扩大。</para>
        /// </summary>
        /// <param name="roomType">房间类型(与 <see cref="ClinicContextRow.RoomType"/> ordinal 比较)。</param>
        /// <param name="rows">待筛行集(不要求来自表;夹具可自造多行同类型)。</param>
        /// <returns>升序情境 id;无匹配 ⇒ 空集单例。</returns>
        internal static IReadOnlyList<string> CollectContextIdsFromRows(
            string roomType, IReadOnlyList<ClinicContextRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var ids = new List<string>(1);
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].RoomType, roomType, StringComparison.Ordinal))
                    ids.Add(rows[i].ContextId);
            }
            if (ids.Count == 0) return EmptyContexts();

            ids.Sort(StringComparer.Ordinal);
            // ⚠️ 经 `ReadOnlyCollection` 包装而非裸数组:裸数组按 `IReadOnlyList` 暴露后可被
            // 强转回 `string[]` 就地改写,「输出快照不可变」这条契约会静默失效
            // (承 `ClinicContextTable.Rows` 的 W-4 先例)。
            return new System.Collections.ObjectModel.ReadOnlyCollection<string>(ids);
        }
    }
}
