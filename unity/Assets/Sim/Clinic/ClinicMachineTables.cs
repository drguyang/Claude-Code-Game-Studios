// 权威来源:
//   GDD design/gdd/clinic-machine.md F-24-1 `:155-245`(CONTEXT_TABLE / ADJ_TABLE / ROOM_TABLE 的形状)
//   ADR-014 §二/§五(作者态 JSON → 构建期烘焙 `*.cooked` → 运行期只读产物)
//   ADR-005 / ADR-006(整数定点域;本文件的全部数值字段 = `Fix`)
//   写方严格镜像:`Editor.Tools.Bake/ClinicMachineCookedWriter.cs`(字段序逐字同)
//   运行期解码:`Gameplay.Presentation/ClinicMachineCookedCodec.cs`
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Clinic`(门 A / b2:引用集 = BCL + Sim.Contracts,零引擎)。
//
// ⚠️ **构造器拒收 = Sim 侧纵深防御**,与 `DelayTable53.Table` / `Outcome53.Table` 同口径:
//   构建期主校验点在 Editor.Tools.Bake 的三个 Binder;此处防「绕过 Bake 直构表」与「产物被改」。
//   ⇒ 本文件只做**可判定的结构性拒收**(重复键 / 域越界 / 对称性 / 行数为零),
//     不重做 Schema 的字符串词表与白名单(那些是作者态的事,运行期再做一遍只会造成两份规则)。
//
// ⚠️ 三张表的 `Create` 一律**拷贝**入参(不直存调用方集合)—— 杜绝「校验过的值在构造后被外部改写」。
//
// ⚠️ 字符串键一律 `StringComparer.Ordinal`:
//   `Dictionary<string,T>` 默认比较器虽是 ordinal,但**显式写出**才让「文化无关」成为可读的契约,
//   也避免后人换成 `StringComparer.CurrentCulture` 时无人察觉(clinic 的 room_type 是 ASCII id)。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>ROOM_TABLE 子句类型闭集(F-24-1)。
    /// <para>⚠️ 序号 = 烘焙产物的 `u8 kind` 字节值,**与** `Editor.Tools.Bake.ClinicMachineSchema.ClinicClauseKind`
    /// 逐项同序(跨侧断言见 `clinic_machine_tables_cross_side_test`;改序 = 已烘产物全部错读)。</para>
    /// </summary>
    public enum ClinicRoomClauseKind : byte
    {
        /// <summary>「<c>type</c> 的数量 ≥ <c>min</c>」。</summary>
        MinCount = 0,

        /// <summary>「存在 <c>type</c>」。</summary>
        HasType = 1,

        /// <summary>「<c>type</c> 在 <c>direction</c> 上邻接 <c>neighbor_type</c> 的数量 ≥ <c>min</c>」。</summary>
        AdjacencyMinCount = 2,
    }

    /// <summary>ROOM_TABLE 邻接子句的方向闭集(序号 = 产物 `u8 direction` 字节值)。</summary>
    public enum ClinicRoomClauseDirection : byte
    {
        /// <summary>不适用(非邻接子句)。</summary>
        None = 0,

        /// <summary>+x 方向。</summary>
        PlusX = 1,

        /// <summary>+y 方向。</summary>
        PlusY = 2,
    }

    /// <summary>CONTEXT_TABLE 的一行(房间类型 → 情境)。</summary>
    public readonly struct ClinicContextRow
    {
        /// <summary>房间类型 id(与 ROOM_TABLE 的 `room_type` 同一词空间)。</summary>
        public readonly string RoomType;

        /// <summary>情境 id(如「手术室 · 无菌」)。</summary>
        public readonly string ContextId;

        /// <summary>房间格情境乘数 K_speed(∈ (0, 1],F-24-1 档集 {1, 7/8, 3/4})。</summary>
        public readonly Fix KSpeed;

        /// <summary>房间基础环境 base_env(∈ [ENV_MOD_MIN, ENV_MOD_MAX],F-24-1)。</summary>
        public readonly Fix BaseEnv;

        /// <summary>构造一行。</summary>
        public ClinicContextRow(string roomType, string contextId, Fix kSpeed, Fix baseEnv)
        {
            RoomType = roomType; ContextId = contextId; KSpeed = kSpeed; BaseEnv = baseEnv;
        }
    }

    /// <summary>CONTEXT_TABLE(24 医馆)的运行期只读视图。
    /// <para>派生导出值 <see cref="KContextMax"/> = max(K_speed)(F-24-3,交付系统 1 的全局时钟缩放)。</para>
    /// </summary>
    public sealed class ClinicContextTable
    {
        private readonly Dictionary<string, ClinicContextRow> _byRoomType;
        private readonly ClinicContextRow[] _rows;
        private readonly ReadOnlyCollection<ClinicContextRow> _rowView;

        /// <summary>表派生导出值 = max(K_speed)(F-24-3)。</summary>
        public Fix KContextMax { get; }

        /// <summary>行数。</summary>
        public int Count => _rows.Length;

        /// <summary>全部行(声明序 = 产物行序;只读视图)。
        /// <para>⚠️ 必须经 <see cref="ReadOnlyCollection{T}"/> 包装,不直接返回数组 ——
        /// 裸数组按 <c>IReadOnlyList</c> 暴露后可被强转回 <c>ClinicContextRow[]</c> 就地改写,
        /// 「构造期校验过的值不可变」这条契约会静默失效(W-4 先例)。</para></summary>
        public IReadOnlyList<ClinicContextRow> Rows => _rowView;

        /// <summary>构造并校验。</summary>
        /// <param name="rows">行集(构造期拷贝)。</param>
        /// <param name="kContextMax">产物头导出的 max(K_speed);须等于按行重算值。</param>
        /// <exception cref="ArgumentException">① 行集为空(整表静默失效)② `room_type` 空 / 重复
        /// ③ `K_speed ∉ (0, 1]` ④ 导出值与重算值不符(产物被改 / 写方与读方漂移)。</exception>
        public ClinicContextTable(IReadOnlyList<ClinicContextRow> rows, Fix kContextMax)
        {
            if (rows == null || rows.Count == 0)
                throw new ArgumentException("CONTEXT_TABLE 行数为 0 —— 空表让全部房间退回默认情境 = 静默失效");

            _rows = new ClinicContextRow[rows.Count];
            _byRoomType = new Dictionary<string, ClinicContextRow>(rows.Count, StringComparer.Ordinal);

            Fix recomputedMax = default;
            bool hasAny = false;
            for (int i = 0; i < rows.Count; i++)
            {
                ClinicContextRow row = rows[i];
                if (string.IsNullOrEmpty(row.RoomType))
                    throw new ArgumentException($"CONTEXT_TABLE 第 {i} 行 room_type 为空 —— 拒收");
                if (row.KSpeed.Raw <= 0 || row.KSpeed.Raw > Fix.OneRaw)
                    throw new ArgumentException(
                        $"CONTEXT_TABLE 行「{row.RoomType}」K_speed raw={row.KSpeed.Raw} ∉ (0, 1.0] —— " +
                        "F-24-1 档集 {1, 7/8, 3/4};K ≤ 0 会让房间内时间停滞");
                if (_byRoomType.ContainsKey(row.RoomType))
                    throw new ArgumentException(
                        $"CONTEXT_TABLE room_type「{row.RoomType}」重复 —— 每房间类型恰一情境行" +
                        "(重复会让「取哪一行」成为隐式规则 = 静默失序)");

                _byRoomType.Add(row.RoomType, row);
                _rows[i] = row;

                if (!hasAny || row.KSpeed.Raw > recomputedMax.Raw)
                {
                    recomputedMax = row.KSpeed;
                    hasAny = true;
                }
            }

            if (recomputedMax.Raw != kContextMax.Raw)
                throw new ArgumentException(
                    $"CONTEXT_TABLE 头导出 K_CONTEXT_MAX raw={kContextMax.Raw} ≠ 按行重算 max(K_speed) " +
                    $"raw={recomputedMax.Raw} —— 产物被改或写方/读方漂移(F-24-3)");

            KContextMax = kContextMax;
            _rowView = Array.AsReadOnly(_rows);
        }

        /// <summary>查房间类型的 base_env。</summary>
        /// <returns>存在该 room_type ⇒ true;否则 false(不抛 —— 未知类型是调用方的正常分支)。</returns>
        public bool TryGetBaseEnv(string roomType, out Fix baseEnv)
        {
            if (roomType != null && _byRoomType.TryGetValue(roomType, out ClinicContextRow row))
            {
                baseEnv = row.BaseEnv;
                return true;
            }
            baseEnv = default;
            return false;
        }

        /// <summary>查房间类型的 K_speed。</summary>
        public bool TryGetKSpeed(string roomType, out Fix kSpeed)
        {
            if (roomType != null && _byRoomType.TryGetValue(roomType, out ClinicContextRow row))
            {
                kSpeed = row.KSpeed;
                return true;
            }
            kSpeed = default;
            return false;
        }

        /// <summary>查整行(情境 id 等一并取用)。</summary>
        public bool TryGetRow(string roomType, out ClinicContextRow row)
        {
            if (roomType != null && _byRoomType.TryGetValue(roomType, out row)) return true;
            row = default;
            return false;
        }
    }

    /// <summary>ADJ_TABLE 的一条**有向**条目 `w(a,b)`(作者态是有向的;对称性由烘焙门保证)。</summary>
    public readonly struct ClinicAdjPair
    {
        /// <summary>左端家具类型 id。</summary>
        public readonly string A;

        /// <summary>右端家具类型 id。</summary>
        public readonly string B;

        /// <summary>权重(可负)。</summary>
        public readonly Fix W;

        /// <summary>构造一条。</summary>
        public ClinicAdjPair(string a, string b, Fix w) { A = a; B = b; W = w; }
    }

    /// <summary>ADJ_TABLE(24 医馆)的运行期只读视图 —— 以**无序对**提供查询(F-24-1)。
    /// <para>⚠️ 作者态有向、运行期无序:构造期把 `(a,b)` 与 `(b,a)` 归一成同一键并要求两者相等,
    /// 因此 <see cref="TryGetWeight"/> 对调参序给出同一值(F-24-1 的 adj_sum 只按 +x/+y 遍历,
    /// 但镜像 / 旋转布局必须同分 —— 承 AC-24-06)。</para>
    /// </summary>
    public sealed class ClinicAdjTable
    {
        private readonly Dictionary<string, Fix> _byUnorderedKey;

        /// <summary>W 域下界(须 &lt; 0;O-24-5 / OQ-24-6)。</summary>
        public Fix WMin { get; }

        /// <summary>W 域上界(须 &gt; 0)。</summary>
        public Fix WMax { get; }

        /// <summary>单房间格数上界(OQ-24-5 已裁 = 64)。</summary>
        public long NMax { get; }

        /// <summary>有向条目数(产物行数)。</summary>
        public int DirectedCount { get; }

        /// <summary>无序对数(自对 `a == b` 计 1)。</summary>
        public int UnorderedPairCount => _byUnorderedKey.Count;

        /// <summary>构造并校验(含对称完整性)。</summary>
        /// <param name="pairs">有向条目集(构造期拷贝)。</param>
        /// <exception cref="ArgumentException">① 条目为空 ② 域端点非 `w_min &lt; 0 &lt; w_max`
        /// ③ `n_max ≤ 0` ④ 条目 `a` / `b` 为空 ⑤ `w ∉ [w_min, w_max]`
        /// ⑥ 对称性缺失或镜像值不等(AC-24-06)。</exception>
        public ClinicAdjTable(IReadOnlyList<ClinicAdjPair> pairs, long nMax, Fix wMin, Fix wMax)
        {
            if (wMin.Raw >= 0 || wMax.Raw <= 0)
                throw new ArgumentException(
                    $"ADJ_TABLE 域端点非法 [w_min raw={wMin.Raw}, w_max raw={wMax.Raw}] —— " +
                    "须 w_min < 0 < w_max(OQ-24-6 = [-1/4, +1/4])");
            if (nMax <= 0)
                throw new ArgumentException($"ADJ_TABLE n_max={nMax} ≤ 0 —— OQ-24-5 房间格数上界须 > 0");
            if (pairs == null || pairs.Count == 0)
                throw new ArgumentException("ADJ_TABLE 条目为 0 —— 空表让 adj_sum 恒 0 = 整表静默失效");

            WMin = wMin;
            WMax = wMax;
            NMax = nMax;
            DirectedCount = pairs.Count;
            _byUnorderedKey = new Dictionary<string, Fix>(pairs.Count, StringComparer.Ordinal);

            int expectedDirected = 0;
            var directedKeyCount = new Dictionary<string, int>(pairs.Count, StringComparer.Ordinal);
            for (int i = 0; i < pairs.Count; i++)
            {
                ClinicAdjPair p = pairs[i];
                if (string.IsNullOrEmpty(p.A) || string.IsNullOrEmpty(p.B))
                    throw new ArgumentException($"ADJ_TABLE 第 {i} 条端点为空 —— 拒收");
                if (p.W.Raw < wMin.Raw || p.W.Raw > wMax.Raw)
                    throw new ArgumentException(
                        $"ADJ_TABLE w(\"{p.A}\",\"{p.B}\") raw={p.W.Raw} ∉ [{wMin.Raw}, {wMax.Raw}] —— " +
                        "F-24-1 / OQ-24-6");

                // 重复有向键:「取哪一行」成为隐式规则 ⇒ 静默失序(与烘焙侧同一判据)。
                string directedKey = p.A + "\u0001" + p.B;
                if (directedKeyCount.TryGetValue(directedKey, out int times))
                    throw new ArgumentException(
                        $"ADJ_TABLE 重复有向键 (\"{p.A}\",\"{p.B}\")(第 {times + 1} 次)—— 每有向对恰一行");
                directedKeyCount.Add(directedKey, 1);

                bool isSelfPair = string.Equals(p.A, p.B, StringComparison.Ordinal);
                string key = UnorderedKey(p.A, p.B);
                if (_byUnorderedKey.TryGetValue(key, out Fix existing))
                {
                    if (existing.Raw != p.W.Raw)
                        throw new ArgumentException(
                            $"ADJ_TABLE 不对称 w(\"{p.A}\",\"{p.B}\") raw={p.W.Raw} ≠ 镜像 raw={existing.Raw} —— " +
                            "AC-24-06:镜像 / 旋转后的同一布局必须算出同分");
                }
                else
                {
                    _byUnorderedKey.Add(key, p.W);
                    expectedDirected += isSelfPair ? 1 : 2;
                }
            }

            // 对称完整性:每个非自对无序对须恰有 2 条有向条目(自对恰 1 条)。
            if (DirectedCount != expectedDirected)
                throw new ArgumentException(
                    $"ADJ_TABLE 对称性缺失:有向条目 {DirectedCount} ≠ 期望 {expectedDirected} " +
                    $"(无序对 {_byUnorderedKey.Count}) —— " +
                    "AC-24-06:单向缺项会让镜像布局算出不同分");
        }

        /// <summary>查**无序**对权重(对调参序结果相同)。</summary>
        /// <returns>两端类型均在表内 ⇒ true;否则 false(未知类型对 = 权重 0,由调用方决定语义)。</returns>
        public bool TryGetWeight(string a, string b, out Fix w)
        {
            if (a != null && b != null && _byUnorderedKey.TryGetValue(UnorderedKey(a, b), out w))
                return true;
            w = default;
            return false;
        }

        /// <summary>无序键:两端按 Ordinal 排序后以 U+0001 连接(单射前提 = 端点不含控制字符,
        /// 由烘焙侧 `RejectControlChars` 保证)。</summary>
        private static string UnorderedKey(string a, string b)
            => string.CompareOrdinal(a, b) <= 0 ? a + "\u0001" + b : b + "\u0001" + a;
    }

    /// <summary>ROOM_TABLE 的一条判据子句(合取成员)。</summary>
    public readonly struct ClinicRoomClause
    {
        /// <summary>子句类型。</summary>
        public readonly ClinicRoomClauseKind Kind;

        /// <summary>主语家具类型 id。</summary>
        public readonly string Type;

        /// <summary>邻接子句的邻居类型(非邻接子句 = null)。</summary>
        public readonly string NeighborType;

        /// <summary>邻接子句的方向(非邻接子句 = <see cref="ClinicRoomClauseDirection.None"/>)。</summary>
        public readonly ClinicRoomClauseDirection Direction;

        /// <summary>计数阈值(非计数子句 = 0)。</summary>
        public readonly int Min;

        /// <summary>构造一条。</summary>
        public ClinicRoomClause(
            ClinicRoomClauseKind kind, string type, string neighborType,
            ClinicRoomClauseDirection direction, int min)
        {
            Kind = kind; Type = type; NeighborType = neighborType; Direction = direction; Min = min;
        }
    }

    /// <summary>ROOM_TABLE 的一个房间类型定义(谓词 = 子句合取)。</summary>
    public readonly struct ClinicRoomDef
    {
        /// <summary>房间类型 id。</summary>
        public readonly string RoomType;

        /// <summary>子句集。</summary>
        /// <remarks>
        /// <para>⚠️ <b>本结构体的公共构造器<b>不</b>做防强转包装</b>(2026-10-21 评审 D5 登记)——
        /// 它原样保存传入的引用。若调用方传入裸 <c>ClinicRoomClause[]</c>,则该数组仍可被强转回去就地改写。</para>
        /// <para><b>只读保证由 <see cref="ClinicRoomTable"/> 提供</b>:表构造器
        /// (<c>ClinicRoomTable</c> ctor 内的 <c>Array.AsReadOnly(copied)</c>)会**拷贝一份**再包成
        /// <see cref="ReadOnlyCollection{T}"/>,故经表拿到的一律不可强转。
        /// ⇒ 只要 `ClinicRoomDef` 实例来自表(生产路径的唯一来源),只读性成立。</para>
        /// <para><b>为什么不在本构造器里也包一层</b>:那会让每一次拷贝都多分配一次
        /// <see cref="ReadOnlyCollection{T}"/> 而表构造器随后**再包一次** —— 双包装不可观测,
        /// 只是把「谁负责只读」这件事从一处散成两处。今日<b>零生产调用方</b>绕过表构造器
        /// (直接 <c>new ClinicRoomDef</c> 的只有本文件与测试)。</para>
        /// </remarks>
        public readonly IReadOnlyList<ClinicRoomClause> Clauses;

        /// <summary>构造一个。<b>不</b>对 <paramref name="clauses"/> 做只读包装 —— 见 <see cref="Clauses"/> 的备注。</summary>
        public ClinicRoomDef(string roomType, IReadOnlyList<ClinicRoomClause> clauses)
        {
            RoomType = roomType; Clauses = clauses;
        }
    }

    /// <summary>ROOM_TABLE(24 医馆)的运行期只读视图。
    /// <para>⚠️ 本表只承载**定义**;「布局 → 房间类型」的匹配求值不在本件(F-24-2 的求值归后续轮),
    /// 且其可判定前提受 `O-24-1`(家具类型闭集)制约 —— 与 `ClinicRoomExtraction` 同一缺口哨兵纪律。</para>
    /// </summary>
    public sealed class ClinicRoomTable
    {
        private readonly Dictionary<string, ClinicRoomDef> _byRoomType;
        private readonly ClinicRoomDef[] _rooms;
        private readonly ReadOnlyCollection<ClinicRoomDef> _roomView;

        /// <summary>房间类型数。</summary>
        public int Count => _rooms.Length;

        /// <summary>全部房间定义(产物声明序)。
        /// <para>⚠️ 同 <see cref="ClinicContextTable.Rows"/>:必须经 <see cref="ReadOnlyCollection{T}"/>
        /// 包装,裸数组可被强转回 <c>ClinicRoomDef[]</c> 就地改写。</para></summary>
        public IReadOnlyList<ClinicRoomDef> Rooms => _roomView;

        /// <summary>构造并校验。</summary>
        /// <exception cref="ArgumentException">① 表为空 ② `room_type` 空 / 重复
        /// ③ 子句集为空(空合取 = 恒匹配 ⇒ 整馆恒判为该类型)④ 子句字段非法。</exception>
        public ClinicRoomTable(IReadOnlyList<ClinicRoomDef> rooms)
        {
            if (rooms == null || rooms.Count == 0)
                throw new ArgumentException("ROOM_TABLE 房间类型为 0 —— 空表让全部布局恒 ROOM_NONE = 静默失效");

            _rooms = new ClinicRoomDef[rooms.Count];
            _byRoomType = new Dictionary<string, ClinicRoomDef>(rooms.Count, StringComparer.Ordinal);

            for (int i = 0; i < rooms.Count; i++)
            {
                ClinicRoomDef def = rooms[i];
                if (string.IsNullOrEmpty(def.RoomType))
                    throw new ArgumentException($"ROOM_TABLE 第 {i} 个 room_type 为空 —— 拒收");
                if (_byRoomType.ContainsKey(def.RoomType))
                    throw new ArgumentException(
                        $"ROOM_TABLE room_type「{def.RoomType}」重复 —— 每房间类型恰一谓词");
                if (def.Clauses == null || def.Clauses.Count == 0)
                    throw new ArgumentException(
                        $"ROOM_TABLE「{def.RoomType}」子句集为空 —— 空合取 = 恒匹配,整馆恒判为该类型");

                var copied = new ClinicRoomClause[def.Clauses.Count];
                for (int c = 0; c < copied.Length; c++)
                {
                    ClinicRoomClause clause = def.Clauses[c];
                    ValidateClause(def.RoomType, c, clause);
                    copied[c] = clause;
                }

                var materialized = new ClinicRoomDef(def.RoomType, Array.AsReadOnly(copied));
                _rooms[i] = materialized;
                _byRoomType.Add(def.RoomType, materialized);
            }

            _roomView = Array.AsReadOnly(_rooms);
        }

        /// <summary>查房间类型定义。</summary>
        public bool TryGetRoom(string roomType, out ClinicRoomDef room)
        {
            if (roomType != null && _byRoomType.TryGetValue(roomType, out room)) return true;
            room = default;
            return false;
        }

        /// <summary>子句字段级校验(闭集外的序号在强转前已在读方按 byte 取出 ⇒ 此处按枚举定义域判)。</summary>
        private static void ValidateClause(string roomType, int index, ClinicRoomClause clause)
        {
            string where = $"ROOM_TABLE「{roomType}」clauses[{index}]";
            ValidateClauseEnums(where, clause);
            ValidateClausePayload(where, clause);
        }

        /// <summary>公共字段守卫:两个枚举须 ∈ 闭集,<c>type</c> 须非空。
        /// <para>2026-10-21 评审 D7:从 <see cref="ValidateClause"/> 拆出(该件唯一建议拆的方法)。
        /// 拆分是<b>纯提取</b> —— 判据、顺序、报文逐字不变。</para></summary>
        private static void ValidateClauseEnums(string where, ClinicRoomClause clause)
        {
            if (clause.Kind != ClinicRoomClauseKind.MinCount
                && clause.Kind != ClinicRoomClauseKind.HasType
                && clause.Kind != ClinicRoomClauseKind.AdjacencyMinCount)
                throw new ArgumentException($"{where} kind={(byte)clause.Kind} ∉ 闭集 {{0,1,2}} —— 产物损坏");

            if (clause.Direction != ClinicRoomClauseDirection.None
                && clause.Direction != ClinicRoomClauseDirection.PlusX
                && clause.Direction != ClinicRoomClauseDirection.PlusY)
                throw new ArgumentException($"{where} direction={(byte)clause.Direction} ∉ 闭集 {{0,1,2}} —— 产物损坏");

            if (string.IsNullOrEmpty(clause.Type))
                throw new ArgumentException($"{where} type 为空 —— 拒收");
        }

        /// <summary>按 <c>Kind</c> 分支的载荷守卫:邻接类须带方向与正阈值,其余不得带。
        /// <para>2026-10-21 评审 D7:纯提取,判据与报文逐字不变。</para></summary>
        private static void ValidateClausePayload(string where, ClinicRoomClause clause)
        {
            bool isAdjacency = clause.Kind == ClinicRoomClauseKind.AdjacencyMinCount;
            if (isAdjacency)
            {
                if (string.IsNullOrEmpty(clause.NeighborType))
                    throw new ArgumentException($"{where} 邻接子句缺 neighbor_type —— 拒收");
                if (clause.Direction == ClinicRoomClauseDirection.None)
                    throw new ArgumentException($"{where} 邻接子句缺 direction —— 方向不确定 = 判据不确定");
                if (clause.Min < 1)
                    throw new ArgumentException($"{where} 邻接子句 min={clause.Min} < 1 —— 计数阈值须为正");
            }
            else
            {
                if (clause.NeighborType != null)
                    throw new ArgumentException($"{where} 非邻接子句带 neighbor_type —— 拒收");
                if (clause.Direction != ClinicRoomClauseDirection.None)
                    throw new ArgumentException($"{where} 非邻接子句带 direction —— 拒收");

                if (clause.Kind == ClinicRoomClauseKind.MinCount && clause.Min < 1)
                    throw new ArgumentException($"{where} MinCount 子句 min={clause.Min} < 1 —— 计数阈值须为正");
                if (clause.Kind == ClinicRoomClauseKind.HasType && clause.Min != 0)
                    throw new ArgumentException($"{where} HasType 子句 min={clause.Min} ≠ 0 —— 无阈值语义");
            }
        }
    }
}
