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
//     🔁 **2026-10-25 订正(原文不删):上句是幽灵引据** —— `TD-persist-03` 的裁定面是
//        「重放 + 折叠段字节布局」,**不含异步化**,且该债已于 2026-10-24 **closed**
//        (`docs/tech-debt-register.md` §`### TD-persist-03` 结案块)。⇒ 「异步化」这一半
//        **今日无任何债承载**。真实归属 = **`AC-7a-07`**(规则五:写盘 IO 与 SHA256 在非主线程
//        + `IOException` 退避重试),该 AC 在 `EPIC.md` §AC 载体总表里判 **PARTIAL**,
//        **新债已立**:`TD-persist-05`(2026-10-25 登记,`docs/tech-debt-register.md` §该条正文本轮已落盘)。
//        **不得**再以 03 充当它的载体。
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
        private readonly Action<byte[]> _foldedRowSink;
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
        /// <para>🔁 `TD-persist-03` 修复轮:委托的实现 = 装配根把列表喂给
        /// `EventStream.Restore`(绕 CAP、留去重/单调、Seq 不重发 —— 用户 2026-10-24
        /// 裁定「三分化」);本类只负责解出与交出,状态重建语义在 `Sim` 侧。</para>
        /// </param>
        /// <param name="foldedRowSink">
        /// 折叠段交出委托(`TD-persist-03` 动作 ⑤)。<see cref="Load"/> 在事件段之后
        /// 把折叠段**原始字节**交给它 —— 本类看不见 <c>FoldRow</c>(住 `Sim`,兄弟装配),
        /// 解码在装配根经 `FoldRowCodec` 完成(与 compose 同一「唯一双见装配」手法)。
        /// <para>⚠️ <b>未注入 ⇒ <see cref="Load"/> 在折叠段解析前抛具名异常</b>(fail-closed):
        /// 折叠行是被折叠病人病史的唯一残留,静默丢弃 = 不可逆数据丢失。</para>
        /// <para>⚠️ 契约次序:<b>先</b> <paramref name="replaySink"/>(事件列表)
        /// <b>后</b>本委托(折叠段字节)—— 装配根的恢复闭包依赖此次序
        /// (折叠行到达时事件列表必须已在手)。</para>
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
            Action<byte[]> foldedRowSink = null,
            Func<SaveSlot, byte[]> compose = null)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _folded = folded;
            _narrativeContext = narrativeContext;
            _replaySink = replaySink;
            _foldedRowSink = foldedRowSink;
            _compose = compose;
            // 台账与 SaveService 共用**同一个**钩子实例 ⇒ 「一个量」在类型层面成立
            // ⚠️ 高水位初值今日**恒为 0**(用默认形参)。`SaveSlotLedger` 的 doc 写「初值由装配层
            //    按『本会话从哪个槽位起步』决定」,而装配层**无任何通道可传**(`SaveService` 构造
            //    不暴露该形参)—— 那句承诺**今日落空**(`/code-review` S19-1 W-4)。
            //    「从槽位 N 续玩」= 读档后高水位应从存档恢复。🔁 2026-10-24:原注记指向
            //    `TD-persist-03`,但该债的裁定面(重放 + 字节布局)不含台账初值 —— **W-4 未随
            //    其兑现,仍 open**(证据件 impl-td-persist-03 §六 如实登记;恢复点须由读档
            //    路径给,ADR-010 §六;落点随读档 UX / 槽位台账轮)。
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
        /// <remarks>
        /// 🔁 <b>2026-10-25 由 <c>public</c> 改 <c>internal</c>(AC-7a-08/09 判据 ① 裁定)</b>:
        /// 本方法是**低层文件 I/O 原语**,不是服务接口的一部分 —— 它**绕过 <see cref="Checkpoint"/>
        /// 的全部门**(叙事上下文 / 槽位只进不退 / 组版),任何拿到 <c>ISaveService</c> 的代码
        /// 都能借此写任意槽。⇒ 从公开面移出,与 GDD 判据「公开可触发写入方法参数集恰等于
        /// <c>{SaveSlot}</c>」及 ADR-010「<c>Checkpoint</c> 是唯一写入口」对齐。
        /// <para><b>测试可达</b>:<c>Sim.Codec/AssemblyInfo.cs</c> 已有
        /// <c>InternalsVisibleTo("Sim.Contracts.Tests")</c> ⇒ 17 处夹具调用**零改动**。</para>
        /// <para>⚠️ <b>生产调用点 = 1</b>(<see cref="Checkpoint"/> 内),故降级无生产影响。</para>
        /// </remarks>
        internal void SaveBytes(SaveSlot slot, byte[] data)
            => WriteAtomic(GetFinalPath(slot), GetTmpPath(slot), GetBakPath(slot), data);

        /// <summary>
        /// 原子写的**唯一实现**(tmp → flush → rename,双档 bak)。
        /// <para>🔁 **2026-10-25 W4 补正**:<see cref="WriteExitSave"/> 首版是逐行复制的第二份
        /// 实现 —— 与 <see cref="SaveBytes"/> 除路径解析外完全相同。那是本项目反复抓到的
        /// 「双实现」形态(两处将来必然漂移,且漂移是**静默**的:退出保存与 checkpoint
        /// 的耐久性语义会分叉)。提取为单一份,两处各自只负责**路径**。</para>
        /// <para>⚠️ **三条路径全部由调用方给,本方法不派生** —— 首版图省事由 finalPath 派生
        /// tmp/bak(`finalPath + ".tmp"`),结果 bak 变成 `save_1.bin.bak`,而
        /// <see cref="LoadBytes"/> 按 <see cref="GetBakPath"/> 读的是 `save_1.bak` ⇒
        /// **回退链静默断裂**(`save_service_test.cs:91` `test_doubleBackup_bakCreatedOnSecondWrite`
        /// 与 `test_checksumFallback_readsBakWhenFinalCorrupted` 两条同时转红)。路径命名是
        /// 读写的**共同契约**,不能由写侧单方面决定。</para>
        /// </summary>
        /// <param name="finalPath">最终文件路径。</param>
        /// <param name="tmpPath">临时文件路径(须与 final 同目录,保证 rename 不跨卷)。</param>
        /// <param name="bakPath">上一好档的备份路径。</param>
        /// <param name="data">待写字节(调用方保证非 null)。</param>
        private static void WriteAtomic(string finalPath, string tmpPath, string bakPath, byte[] data)
        {
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
        /// <remarks>
        /// 🔁 <b>2026-10-25 由 <c>public</c> 改 <c>internal</c></b> —— 与
        /// <see cref="SaveBytes"/> 同批(AC-7a-08/09 判据 ①):低层读原语不在服务接口面内。
        /// <para>⚠️ 与写侧的唯一差别:<c>LoadBytes</c> **没有「绕过门」的风险**(读不改变状态),
        /// 改它是为了让判据「<b>全部</b>公开方法参数集 = <c>{SaveSlot}</c> 或空」在字面上成立 ——
        /// 否则 GDD 字面与实现之间要长期靠脚注解释,而脚注不会被执行。</para>
        /// <para>生产调用点 = 1(<see cref="Load"/> 内);测试 4 处经 IVT 零改动。</para>
        /// </remarks>
        /// <exception cref="SaveCorruptException">
        /// <c>final</c> 与 <c>bak</c> **双双**校验失败(或两者都不存在)⇒ 进入 <c>Corrupt</c> 态。
        /// <para>🔁 **2026-10-25 AC-7a-15(b) 补齐**:原抛裸 <see cref="InvalidDataException"/>,
        /// 与 `Load` 路径上另三处(前向不兼容 / magic 不符 / 布局损坏)**同型不可分** ——
        /// 而 GDD `:257` 的 `Corrupt` 是一个**独立的态**,态的边界须在类型上可表达。
        /// 🔁 **2026-10-25 订正(原文不删)**:上句「子类关系保留…兼容」**曾是错的** ——
        /// 首版按 `InvalidDataException` 写,实测它是 **`sealed`**(`error CS0509`),不可继承;
        /// 且全仓 `catch (InvalidDataException)` **零命中** ⇒ 兼容义务本就不存在。
        /// 现父类 = `Exception`,两条实测理由见 `SaveCorruptException.cs` 文件头。</para>
        /// </exception>
        internal byte[] LoadBytes(SaveSlot slot)
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

            // ── Corrupt 态:两级都坏(GDD :257 / AC-7a-15(b) / 规则十一) ──
            // ⚠️ **本分支零 I/O** —— 不建空档、不删残档、不改任何字节。
            //    「槽目录文件数不变」是一条**禁止**判据,不是「做了清理后数目恰好没变」。
            throw new SaveCorruptException(slot);
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

            // 🔁 **2026-10-25 W4 修正(代码评审必修)**:原写 `new SaveSlot(0)` ⇒
            // `GetFinalPath` 与玩家手动槽 0 返回**同一** `save_0.bin`,两者互相覆盖;
            // 且 `SaveOnExit` 刻意不调 `OnCheckpointCommitted` ⇒ 台账「只进不退」不保护该文件。
            // ADR-010 §六 只写「退出钩子,**非玩家可选槽位**」,未定义槽位身份 ——
            // 「非玩家可选槽位」的字面读法就要求它**不占玩家槽空间**。
            // ⇒ 退出保存改走独立文件名,与 `save_{slot}.bin` 命名空间不相交。
            byte[] bytes = _compose(ExitSaveSlot)
                ?? throw new InvalidOperationException(
                    "compose 委托返回 null —— 组版失败须抛,不得写空档");

            WriteExitSave(bytes);
            // 刻意不调 OnCheckpointCommitted(退出保存不推高水位)
        }

        /// <summary>
        /// 退出保存的文件名(独立于 `save_{slot_seq}.bin` 命名空间)。
        /// <para>承 ADR-010 §六「退出钩子,**非玩家可选槽位**」—— 玩家槽位恒为
        /// <see cref="SaveSlot"/> 的 `slot_seq`,退出保存不参与该序列。</para>
        /// </summary>
        public const string ExitSaveFileName = "save_exit.bin";

        /// <summary>
        /// 传给组版委托的槽位标识。`SlotSeq` 取 `uint.MaxValue` —— 它与任何玩家槽
        /// (`0 .. n`)不相交,且**不会被** <see cref="GetFinalPath"/> 用作文件名
        /// (退出保存走 <see cref="WriteExitSave"/> 的独立路径)。
        /// </summary>
        public static readonly SaveSlot ExitSaveSlot = new SaveSlot(uint.MaxValue);

        /// <summary>
        /// 退出保存 —— 与 <see cref="SaveBytes"/> **共用同一原子写原语**
        /// (<see cref="WriteAtomic"/>),只换文件名。
        /// <para>文件名**独立**于玩家槽命名空间 —— 不与任何 `save_{slot_seq}.bin` 相撞。</para>
        /// </summary>
        private void WriteExitSave(byte[] data)
            => WriteAtomic(Path.Combine(_basePath, ExitSaveFileName),
                           Path.Combine(_basePath, ExitSaveFileName + ".tmp"),
                           Path.Combine(_basePath, ExitSaveFileName + ".bak"),
                           data);

        /// <summary>
        /// 读档。`Gating` → `Verifying` → 头部解析 → 迁移链三分支 → `Replaying`
        /// (事件列表 + 折叠段原始字节,经注入委托交出)。
        /// <para>🔁 `TD-persist-03` 修复轮:状态重建 = 装配根把两者喂给
        /// <c>EventStream.Restore(events, rows)</c>(绕 CAP、留去重/单调、Seq 不重发);
        /// 本类只解出与交出。委托次序 = 先事件、后折叠段(见构造器 doc)。</para>
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

            // ── Replaying:解出事件列表 + 折叠段,经注入委托交出 ──
            // 🔁 TD-persist-03 修复轮:契约次序 = 先事件列表、后折叠段字节
            //    (装配根的恢复闭包依赖此次序 —— Restore(events, rows) 要两者齐手)。
            if (_replaySink == null)
                throw new InvalidOperationException(
                    "未注入 replaySink —— 拒绝静默跳过 Replaying" +
                    "(静默跳过 = 假绿入口)");
            if (_foldedRowSink == null)
                throw new InvalidOperationException(
                    "未注入 foldedRowSink —— 拒绝读档(折叠行是被折叠病人病史的唯一残留," +
                    "静默丢弃 = 不可逆数据丢失;TD-persist-03 动作 ⑤)");

            IReadOnlyList<SimEvent> events;
            byte[] foldedSegment;
            ParseBody(data, out events, out foldedSegment);
            _replaySink(events);
            _foldedRowSink(foldedSegment);

            // ── 锁在 Verifying 通过后置位(用户 2026-10-24 裁定 A) ──
            _ledger.OnLoaded(slot);
        }

        /// <summary>
        /// 解存档体:v1 布局 = <c>事件段(count 前缀 + 逐事件)</c> + <c>折叠段(count 前缀 + 定长行)</c>。
        /// <para>事件段由 <see cref="SimEventListCodec"/> 读尽;其后<b>必跟</b>折叠段
        /// (ADR-010 §一 增补,2026-10-24)—— 不足 4 字节 count 前缀即抛(旧两段式布局
        /// 是本批之前的形态,按损坏拒收)。</para>
        /// <para>⚠️ 折叠段以<b>原始字节</b>交出(本类看不见 <c>FoldRow</c>,解码归装配根)。</para>
        /// </summary>
        private void ParseBody(byte[] data, out IReadOnlyList<SimEvent> events, out byte[] foldedSegment)
        {
            int offset = SaveHeaderCodec.HeaderSize;
            if (data.Length < offset)
                throw new InvalidDataException(
                    $"存档长度 {data.Length} 不足头部 {offset} 字节,无法进入事件段");

            var r = new CodecReader(new ReadOnlySpan<byte>(data, offset, data.Length - offset));
            // 🔁 复合体读:事件段后还有折叠段 ⇒ 用无读尽断言的 ReadWithinBody;
            //    存档体的读尽由折叠段的自描述 count + 其读尽校验闭合(DecodeSegment)。
            events = SimEventListCodec.ReadWithinBody(ref r, _codec);

            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"存档事件段后缺折叠段(v1 布局 = 头部 + 事件段 + 折叠段),仅剩 {r.Remaining} 字节" +
                    "—— 旧两段式布局按损坏拒收(TD-persist-03 修复轮起折叠段为必备段)");

            foldedSegment = r.ReadBytes(r.Remaining);
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
