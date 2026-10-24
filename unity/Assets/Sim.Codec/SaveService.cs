// ADR-010 §四/§六 —— SaveService 实现。
//
// 权威来源:
//   ADR-010 §一 —— 全二进制 codec,显式小端
//   ADR-010 §四 —— 校验和 + 自动回退 + 原子写 + 双档
//   ADR-010 §六 —— Checkpoint(SaveSlot) 唯一写入口
//   ADR-010 §三 —— 义务 1:WorldSeed 生成 + 存档头持久化
//
// 实现要点:
//   - ⚠️ **写盘今日是同线程同步 I/O**(`File.WriteAllBytes` + `FileStream.Flush(true)`),
//     **不是**后台线程。原文此处曾写「序列化在主线程,写盘在后台线程」+「后台线程禁调 Unity API」,
//     两句**陈述的实现不存在**(全文零 `Task` / 零 `Thread`)—— `/code-review` S19-1 C-2 实测。
//     后果:存档体变大后主线程 I/O 会造成帧卡顿。**异步化归 `TD-persist-03`**,未做。
//   - 原子写 = tmp + flush + rename
//   - 双档 bak:final 存在则先 final → bak
//   - 校验和 = SHA256,覆盖 checksum 字段之后的全部字节

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档服务。文件 I/O + 校验和 + 原子写 + 双档。</summary>
    public sealed class SaveService : ISaveService
    {
        private readonly string _basePath;
        private readonly ISaveCodec _codec;
        private readonly IFoldedPredicate _folded;
        private readonly Func<bool> _narrativeContext;
        private readonly Action<IReadOnlyList<SimEvent>> _replaySink;
        private readonly Func<SaveSlot, byte[]> _compose;
        private readonly SaveSlotLedger _ledger;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="basePath">存档目录(须可写)。</param>
        /// <param name="codec">存档编解码器。</param>
        /// <param name="folded">
        /// 折叠谓词(7a 义务 G-2 · `IFoldedPredicate` 住 `Sim.Contracts`、实现住 `Sim`)。
        /// <para><b>为何走构造函数注入而不是给 <c>Checkpoint</c> 加形参</b>:ADR-010 <c>:331</c>
        /// 逐字 <c>interface ISaveService { void Checkpoint(SaveSlot slot); }</c>,并带
        /// **反射断言「公开方法参数集恰等于 <c>{SaveSlot}</c>」** ⇒ 加形参会直接打红该断言
        /// (ADR-025 §① 2026-10-21 甲案裁定 ④)。</para>
        /// <para><b>可空</b>:本类**不直接调用**它 —— 折叠在 <paramref name="compose"/>
        /// 委托的闭包内执行(`HistoryFolder.Fold` 的调用点因此在装配根,不在本类:
        /// `Sim` 与 `Sim.Codec` 是兄弟装配,互不可见)。本形参只作**装配层核对**与
        /// <see cref="FoldedPredicate"/> 只读暴露用。</para>
        /// </param>
        /// <param name="narrativeContext">
        /// 叙事上下文观察钩子(`InNarrativeContext(t)`),**一个量**(GDD `:212`/`:219`
        /// 逐字「与手动存共用同一观察钩子」)。
        /// <para>⚠️ <b>未注入 ⇒ fail-closed</b>(用户 2026-10-24 裁定):<see cref="Checkpoint"/> /
        /// <see cref="Load"/> / <see cref="SaveOnExit"/> 一律抛
        /// <see cref="InvalidOperationException"/>。禁 fail-open。</para>
        /// </param>
        /// <param name="replaySink">
        /// 重放交出委托(`PRE-S19-D` 甲案)。<see cref="Load"/> 解出事件列表后交给它。
        /// <para>⚠️ <b>未注入 ⇒ <see cref="Load"/> 在进 `Replaying` 前抛具名异常</b>,
        /// 禁静默跳过(静默跳过 = 假绿入口)。</para>
        /// <para>真重放(列表 → sim 状态)归 `TD-persist-03`,不在本类。</para>
        /// </param>
        /// <param name="compose">
        /// 组版委托:`SaveSlot → 完整存档字节(含校验和槽)`。
        /// <para><b>这是折叠接线形态的落点</b>(S19-1 三家专家合成裁定,甲-a):折叠行选择、
        /// `FoldRow` 编码、病史流内布局**全部在闭包内** —— 本类永不命名 `FoldRow`,
        /// 因而零新增装配边。闭包住在唯一同时看得见 `Sim` 与 `Sim.Codec` 的
        /// `Gameplay.Boot`。</para>
        /// <para>⚠️ <b>未注入 ⇒ <see cref="Checkpoint"/> / <see cref="SaveOnExit"/> 抛具名异常</b>。</para>
        /// </param>
        public SaveService(
            string basePath,
            ISaveCodec codec,
            IFoldedPredicate folded = null,
            Func<bool> narrativeContext = null,
            Action<IReadOnlyList<SimEvent>> replaySink = null,
            Func<SaveSlot, byte[]> compose = null)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _folded = folded;
            _narrativeContext = narrativeContext;
            _replaySink = replaySink;
            _compose = compose;
            // 台账与 SaveService 共用**同一个**钩子实例 ⇒ 「一个量」在类型层面成立
            // ⚠️ 高水位初值今日**恒为 0**(用默认形参)。`SaveSlotLedger` 的 doc 写「初值由装配层
            //    按『本会话从哪个槽位起步』决定」,而装配层**无任何通道可传**(`SaveService` 构造
            //    不暴露该形参)—— 那句承诺**今日落空**(`/code-review` S19-1 W-4)。
            //    「从槽位 N 续玩」= 读档后高水位应从存档恢复,归 `TD-persist-03`(锁与高水位
            //    都是会话内存,ADR-010 §六 ⇒ 恢复点须由读档路径给,不是构造期猜)。
            _ledger = new SaveSlotLedger(narrativeContext);
            Directory.CreateDirectory(_basePath);
        }

        /// <summary>
        /// 注入的折叠谓词(未注入 ⇒ <c>null</c>)。
        /// <para>只读暴露:让装配层与测试可以核对「注入的是哪个实现」,不必反射私有字段。</para>
        /// </summary>
        public IFoldedPredicate FoldedPredicate => _folded;

        /// <summary>槽位台账(锁 / 高水位)。装配层与测试需要驱动叙事锚点时用。</summary>
        public SaveSlotLedger Ledger => _ledger;

        /// <summary>原子写:tmp → flush → rename。双档 bak。</summary>
        public void SaveBytes(SaveSlot slot, byte[] data)
        {
            string finalPath = GetFinalPath(slot);
            string tmpPath = GetTmpPath(slot);
            string bakPath = GetBakPath(slot);

            // 双档:final 存在则先 final → bak
            if (File.Exists(finalPath))
            {
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(finalPath, bakPath);
            }

            // 写 tmp
            File.WriteAllBytes(tmpPath, data);

            // flush + rename(原子)
            using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Flush(true);
            }
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tmpPath, finalPath);
        }

        /// <summary>读档:校验和验证 + 自动回退 bak。</summary>
        public byte[] LoadBytes(SaveSlot slot)
        {
            string finalPath = GetFinalPath(slot);
            string bakPath = GetBakPath(slot);

            if (File.Exists(finalPath))
            {
                byte[] data = File.ReadAllBytes(finalPath);
                if (ValidateChecksum(data)) return data;
            }

            // 回退 bak
            if (File.Exists(bakPath))
            {
                byte[] data = File.ReadAllBytes(bakPath);
                if (ValidateChecksum(data)) return data;
            }

            throw new InvalidDataException($"存档损坏且无法回退:slot={slot.SlotSeq}");
        }

        /// <summary>校验和验证:SHA256 覆盖 checksum 字段之后的全部字节。</summary>
        public static bool ValidateChecksum(byte[] data)
        {
            if (data == null || data.Length < 36) return false;

            // 读取存储的 checksum(字节 4..35)
            byte[] stored = new byte[32];
            Array.Copy(data, 4, stored, 0, 32);

            // 计算实际 checksum(字节 36..end)
            byte[] actual = new byte[32];
            using (var sha = SHA256.Create())
            {
                byte[] covered = new byte[data.Length - 36];
                Array.Copy(data, 36, covered, 0, covered.Length);
                actual = sha.ComputeHash(covered);
            }

            return CryptographicOperations.FixedTimeEquals(stored, actual);
        }

        /// <summary>计算并写入 checksum。</summary>
        public static byte[] AddChecksum(byte[] dataWithoutChecksum)
        {
            // dataWithoutChecksum = magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4) + tick(8) + snapshotOffset(8) + body
            // 输出 = magic(4) + checksum(32) + saveVersion(4) + ... + body
            if (dataWithoutChecksum == null || dataWithoutChecksum.Length < 32)
                throw new ArgumentException("数据过短");

            byte[] result = new byte[dataWithoutChecksum.Length + 32];

            // magic(4)
            Array.Copy(dataWithoutChecksum, 0, result, 0, 4);

            // checksum(32) — 覆盖 saveVersion 之后的全部字节
            byte[] covered = new byte[dataWithoutChecksum.Length - 4];
            Array.Copy(dataWithoutChecksum, 4, covered, 0, covered.Length);
            byte[] checksum;
            using (var sha = SHA256.Create())
            {
                checksum = sha.ComputeHash(covered);
            }
            Array.Copy(checksum, 0, result, 4, 32);

            // saveVersion(4) + worldSeed(8) + ... + body
            Array.Copy(dataWithoutChecksum, 4, result, 36, dataWithoutChecksum.Length - 4);

            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        // 高层接口 —— Checkpoint / SaveOnExit / Load(S19-1)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 存档(唯一写入口)。门 → 组版 → 原子写 → 推高水位。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 未注入 <c>narrativeContext</c> 或 <c>compose</c>(fail-closed);或不在叙事上下文内。
        /// </exception>
        /// <exception cref="InvalidDataException">槽位只进不退违例(覆盖既有槽位)。</exception>
        public void Checkpoint(SaveSlot slot)
        {
            // 门 ①:fail-closed —— 两个必需驱动源缺一即拒
            RequireNarrativeContext();
            RequireCompose(nameof(Checkpoint));

            // 门 ②:槽位只进不退 + 叙事上下文(AC-7a-19 (b)(c))
            _ledger.EnsureWritable(slot);

            // 组版:折叠与字节布局全在闭包内(本类不认识 FoldRow)
            byte[] bytes = _compose(slot)
                ?? throw new InvalidOperationException(
                    $"compose 委托对 slot={slot.SlotSeq} 返回 null —— 组版失败须抛,不得写空档");

            // 原子写(既有实现:tmp → flush → rename + 双档 bak)
            SaveBytes(slot, bytes);

            // 只在写盘**返回之后**推高水位 —— 写失败则高水位不动
            _ledger.OnCheckpointCommitted(slot);
        }

        /// <summary>
        /// 退出保存。与 <see cref="Checkpoint"/> **同组版、不同门**。
        /// <para><b>同</b>:同一个 <c>compose</c> 委托(组版口径唯一)、同一份 fail-closed 纪律
        /// (未注入即抛)、同一套原子写。</para>
        /// <para><b>不同</b>:① <b>不评估叙事上下文钩子的值</b> —— 只要求它已注入
        /// (`RequireNarrativeContext` 是 null 检查,不是值检查)。理由:退出保存**不是回滚点**,
        /// 且「急救动作进行中退出」恰恰是最需要落盘的场景,拿安全上下文门拦它会丢状态。
        /// ② <b>不推进槽位高水位**(退出钩子不占玩家槽,ADR-010 §六)。</para>
        /// <para>⚠️ 原文此处写「与 <see cref="Checkpoint"/> <b>同门</b>同组版」—— 该措辞**为假**
        /// (值门未过),`/code-review` S19-1 W-1 实测。用户 2026-10-24 裁定:
        /// **只修自述,不改行为**。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">未注入驱动源(fail-closed)。</exception>
        public void SaveOnExit()
        {
            RequireNarrativeContext();
            RequireCompose(nameof(SaveOnExit));

            // 退出保存固定写 slot 0 —— 它不是玩家可选槽,不参与「只进不退」排序
            var slot = new SaveSlot(0);

            byte[] bytes = _compose(slot)
                ?? throw new InvalidOperationException(
                    "compose 委托返回 null —— 组版失败须抛,不得写空档");

            SaveBytes(slot, bytes);
            // 刻意不调 OnCheckpointCommitted
        }

        /// <summary>
        /// 读档。`Gating` → `Verifying` → 头部解析 → 迁移链三分支 → `Replaying` 前半段。
        /// <para>⚠️ <b>真重放(列表 → sim 状态)不在本方法内</b> —— 归 `TD-persist-03`。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 未注入 <c>narrativeContext</c>(fail-closed);不在叙事上下文内;槽位已锁;
        /// 未注入 <c>replaySink</c>。
        /// </exception>
        /// <exception cref="InvalidDataException">存档损坏且无法回退;magic 不符。</exception>
        public void Load(SaveSlot slot)
        {
            // ── Gating:安全上下文门(与写同钩,规则十四) ──
            RequireNarrativeContext();
            if (!_narrativeContext())
                throw new InvalidOperationException(
                    $"不在叙事上下文内,拒绝载入 slot={slot.SlotSeq}" +
                    "(规则十四「载入安全上下文门」—— 读档与写同钩)");
            if (_ledger.IsLocked(slot))
                throw new InvalidOperationException(
                    $"槽位 slot={slot.SlotSeq} 已锁(AC-7a-19 (d) 读档即锁)," +
                    "锁持续到下一次叙事事件 checkpoint");

            // ── Verifying:校验和 + bak 回退(既有实现) ──
            byte[] data = LoadBytes(slot);

            // ── 头部解析 ──
            SaveHeader header = SaveHeaderCodec.Read(data);

            // ── 迁移链三分支 ──
            if (header.SaveVersion > SaveHeader.CurrentSaveVersion)
                throw new InvalidDataException(
                    $"存档版本 {header.SaveVersion} > 当前 {SaveHeader.CurrentSaveVersion}" +
                    "—— 前向不兼容,拒绝载入");
            // SaveVersion == CurrentSaveVersion ⇒ 直通;
            // SaveVersion < CurrentSaveVersion ⇒ 逐版本升(WorldSeed 逐位保留)。
            //
            // ⚠️ **升链实现今日不存在,且此处刻意 fail-closed 而非放行**:
            //    今日 CurrentSaveVersion = 1 且全库零 v0 写入者 ⇒ 升链路径零触发面
            //    (判据 ⑦b 的事实基础)。若在此**放行**,旧档会被当作当前版本直接解析 ——
            //    而 `ReadEventSegment` 按当前版本的布局读,**布局一旦变过就是静默错读**。
            //    ⇒ 拒绝并抛具名异常(不可利用 > 不可用,承 `sprint-19.md` §S19-1 ⑧b 同源口径)。
            //    **真迁移(逐版本升 + WorldSeed 逐位保留)= `NOT-RUN`**,归 `TD-persist-03`;
            //    届时把本分支换成真正的升级实现,并把 ⑦b 的升级夹具改为断言「升完的 WorldSeed 逐位相等」。
            if (header.SaveVersion < SaveHeader.CurrentSaveVersion)
                throw new NotSupportedException(
                    $"存档版本 {header.SaveVersion} < 当前 {SaveHeader.CurrentSaveVersion}" +
                    "—— 升链(逐版本迁移)尚未实现,拒绝按当前版本布局静默解析旧档" +
                    "(布局变更下的静默错读不可接受)。真迁移归 TD-persist-03。");

            // ── Replaying 前半段:字节 → 列表,经注入委托交出 ──
            if (_replaySink == null)
                throw new InvalidOperationException(
                    "未注入 replaySink —— 拒绝静默跳过 Replaying" +
                    "(静默跳过 = 假绿入口;真重放归 TD-persist-03)");

            IReadOnlyList<SimEvent> events = ReadEventSegment(data);
            _replaySink(events);

            // ── 锁在 Verifying 通过后置位(用户 2026-10-24 裁定 A) ──
            _ledger.OnLoaded(slot);
        }

        /// <summary>
        /// 从完整存档字节解出事件列表。
        /// <para>布局 = 头部(<see cref="SaveHeaderCodec.HeaderSize"/>)+ 事件列表段。
        /// ⚠️ <b>快照段 / 分册态段 / 折叠段的布局尚未定义</b>(`TD-persist-03` 动作 ③/⑤)
        /// ⇒ 今日只解「头部之后就是事件列表」这一形态。</para>
        /// </summary>
        private IReadOnlyList<SimEvent> ReadEventSegment(byte[] data)
        {
            int offset = SaveHeaderCodec.HeaderSize;
            if (data.Length < offset)
                throw new InvalidDataException(
                    $"存档长度 {data.Length} 不足头部 {offset} 字节,无法进入事件段");

            byte[] segment = new byte[data.Length - offset];
            Array.Copy(data, offset, segment, 0, segment.Length);
            return SimEventListCodec.Decode(segment, _codec);
        }

        private void RequireNarrativeContext()
        {
            if (_narrativeContext == null)
                throw new InvalidOperationException(
                    "SaveService 未注入 narrativeContext(fail-closed)—— " +
                    "恒真会让 save-scum 通道在生产路径默开而测试全绿;" +
                    "真实驱动源归 8 / 37 / 52 的实现轮(TD-persist-04)。");
        }

        private void RequireCompose(string caller)
        {
            if (_compose == null)
                throw new InvalidOperationException(
                    $"{caller}:未注入 compose 委托(fail-closed)—— " +
                    "组版字节源缺失,拒绝静默写空档;" +
                    "装配点 = Gameplay.Boot 组合根(唯一同时看得见 Sim 与 Sim.Codec 的装配)。");
        }

        private string GetFinalPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.bin");
        private string GetTmpPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.tmp");
        private string GetBakPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.bak");
    }
}
