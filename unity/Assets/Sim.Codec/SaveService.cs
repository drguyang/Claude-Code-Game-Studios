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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档服务。文件 I/O + 校验和 + 原子写 + 双档。</summary>
    public sealed class SaveService : ISaveService
    {
        private readonly string _basePath;
        private readonly ISaveCodec _codec;
        private readonly IFoldedPredicate _folded;
        private readonly INarrativeContextQuery _narrativeContext;
        private readonly Action<IReadOnlyList<SimEvent>> _replaySink;
        private readonly Action<byte[]> _foldedRowSink;
        private readonly Func<SaveSlot, byte[]> _compose;
        private readonly SaveSlotLedger _ledger;

        /// <summary>
        /// 构造期「扫存档目录取最大槽号」的失败原因(<c>null</c> = 未失败 / 未发生)。
        /// <para>非 <c>null</c> ⇒ 高水位不可知 ⇒ <see cref="Checkpoint"/> **拒绝一切写入**
        /// (fail-closed:不可用 &gt; 不安全)。见 <see cref="ResolveInitialNextWritableSeq"/>。</para>
        /// </summary>
        private Exception _slotScanError;
        /// <summary>
        /// 迁移链脚本(ADR-010 §七 · `AC-7a-14`)。**缺省为空** —— 今日零真实历史版本,
        /// 空链下的行为 = 任何 `SaveVersion < 当前` 的档走「断链」并抛
        /// <see cref="InvalidOperationException"/>(与旧行为同为 fail-closed,
        /// 只是报文更准确)。真实脚本随版本升级逐条注册,落点在装配根。
        /// </summary>
        private readonly IReadOnlyList<ISaveMigration> _migrations;

        /// <summary>
        /// blob 池的按号装载面(ADR-034 §⑤)。<see cref="Load"/> 在交出事件 / 折叠段
        /// **之前**用它把载荷段装进池 —— 否则任何解码都撞「查无此 BlobId」。
        /// <para>可空:空载荷段不要求注入;有载荷条目而无注入 ⇒ 抛。</para>
        /// </summary>
        private readonly IBlobRestoreSink _blobRestoreSink;

        /// <summary>
        /// 退出保存的时间预算(ms,`SAVE_EXIT_RETRY_TIMEOUT` 旋钮 · GDD 规则十三)。
        /// <para>缺省 = <see cref="ExitSaveRetrier.DefaultTimeoutMillis"/>(1000 ms)。</para>
        /// </summary>
        private readonly int _exitSaveRetryMillis;

        /// <summary>
        /// 退出保存失败的观察者(装配层 → 日志)。
        /// <para>⚠️ 形参 = <c>(写盘异常, 置位异常)</c>,任一可为 null。本类**不抛**
        /// (退出路径上拋 = 卡退),故这条委托是「记日志」这条 GDD 要求的唯一通道。</para>
        /// </summary>
        private readonly Action<Exception, Exception> _exitSaveFailureSink;

        /// <summary>
        /// 启动后台写盘 worker 的调度器(AC-7a-07 · GDD 规则五)。
        /// <para>生产 = <c>action =&gt; Task.Run(action)</c>;夹具 = 同步内联
        /// <c>action =&gt; action()</c> 或受控线程。</para>
        /// <para>⚠️ <b>未注入 ⇒ fail-closed</b>(用户 2026-10-25 裁定):<see cref="Checkpoint"/> /
        /// <see cref="SaveOnExit"/> 一律抛 <see cref="InvalidOperationException"/>。
        /// <b>禁「缺省同步内联」</b> —— 那会让「生产装配忘了传 <c>Task.Run</c>」静默退化为
        /// 同步写盘,而全部测试仍绿(本项目反复抓过的「静默降级」形态)。</para>
        /// </summary>
        private readonly Action<Action> _writeScheduler;

        /// <summary>后台写盘 worker(one-writer 单飞 + `WRITE_RETRY_DELAY` 退避)。</summary>
        private readonly SaveWriteQueue _writeQueue;

        /// <summary>
        /// 存档路径的精确长度缓冲池(GDD `:123` GC 纪律)。
        /// <para>⚠️ 覆盖范围**有限**,如实登记见 <see cref="SaveBufferPool"/> 文件头 ——
        /// 不含 <c>CodecWriter</c> 的内部增长缓冲。</para>
        /// </summary>
        private readonly SaveBufferPool _bufferPool;

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
            INarrativeContextQuery narrativeContext = null,
            Action<IReadOnlyList<SimEvent>> replaySink = null,
            Action<byte[]> foldedRowSink = null,
            Func<SaveSlot, byte[]> compose = null,
            IReadOnlyList<ISaveMigration> migrations = null,
            int exitSaveRetryMillis = ExitSaveRetrier.DefaultTimeoutMillis,
            Action<Exception, Exception> exitSaveFailureSink = null,
            Action<Action> writeScheduler = null,
            SaveBufferPool bufferPool = null,
            IBlobRestoreSink blobRestoreSink = null,
            uint initialNextWritableSeq = 0)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _folded = folded;
            _narrativeContext = narrativeContext;
            _replaySink = replaySink;
            _foldedRowSink = foldedRowSink;
            _compose = compose;
            // ⚠️ 迁移链**可空**(≠ fail-closed 的那几个驱动源):空链不是「缺驱动源」,
            //    而是「今日没有历史版本可迁」这个事实的正确表达。空链撞上旧档会抛
            //    「断链」—— 那仍是 fail-closed,只是发生在**真的遇到旧档**时才发生。
            _migrations = migrations ?? Array.Empty<ISaveMigration>();
            // ⚠️ 重试预算是**旋钮**,不是硬编码常量 —— 测试用极短预算(如 0 = 不重试)
            //    以保证夹具快且确定;生产用 GDD 默认 1 s。
            _exitSaveRetryMillis = exitSaveRetryMillis;
            _exitSaveFailureSink = exitSaveFailureSink;
            // ⚠️ **不在此处 fail-closed** —— 构造器不抛,由 Checkpoint / SaveOnExit 的
            //    RequireWriteScheduler() 在**使用点**抛。理由:构造器抛会让「只想读档 /
            //    只想查标志位」的装配(与既有 19 处测试构造点)因不相干的理由转红,
            //    而真正需要写盘的那两条路径仍然拒得干净。
            _writeScheduler = writeScheduler;
            _writeQueue = writeScheduler == null ? null : new SaveWriteQueue(writeScheduler);
            _bufferPool = bufferPool ?? new SaveBufferPool();
            // ⚠️ 载荷装载面(ADR-034 §⑤)**可空,但不是静默跳过**:存档里真有载荷条目
            //    而本形参为 null ⇒ Load 在装池那一步抛具名异常(见 Load 的「先装池」段)。
            //    空载荷段(旧档 / 无引用)不要求注入 —— 那时没有任何东西会丢。
            _blobRestoreSink = blobRestoreSink;
            // 台账与 SaveService 共用**同一个**钩子实例 ⇒ 「一个量」在类型层面成立
            // ⚠️ 高水位初值今日**恒为 0**(用默认形参)。`SaveSlotLedger` 的 doc 写「初值由装配层
            //    按『本会话从哪个槽位起步』决定」,而装配层**无任何通道可传**(`SaveService` 构造
            //    不暴露该形参)—— 那句承诺**今日落空**(`/code-review` S19-1 W-4)。
            //    「从槽位 N 续玩」= 读档后高水位应从存档恢复。🔁 2026-10-24:原注记指向
            //    `TD-persist-03`,但该债的裁定面(重放 + 字节布局)不含台账初值 —— **W-4 未随
            //    其兑现,仍 open**(证据件 impl-td-persist-03 §六 如实登记;恢复点须由读档
            //    路径给,ADR-010 §六;落点随读档 UX / 槽位台账轮)。
            // 🔁 2026-11-03(smoke 轮 · `/code-review` S19-1 **W-4** 的装配通道闭合):
            //    高水位初值此前**恒为 0** —— 台账 doc 说「初值由装配层按『本会话从哪个槽位起步』
            //    决定」,而装配层**无通道可传**,那句承诺落空。
            //    实测后果(用户 smoke):`ManualSaveSlots = {1,2,3}` 的屏上,
            //    **第一次手动存/自动 checkpoint 写进槽 0** —— 那号在屏上根本不存在
            //    ⇒ 玩家看到的就是「存了,什么都没有发生」。
            //    ⇒ 装配层现在能传初值;生产传 1(屏上第一个槽),测试缺省仍 0。
            //    🔁 2026-11-03 `TD-persist-15` 订正:上句的「初值」**不再成立** ——
            //       该形参自此是 **floor(下限)**,终值由下面的磁盘扫描决定。
            // ── 高水位初值 = floor ⊕ **磁盘实况**(TD-persist-15)──────────────────────
            // 病因:`initialNextWritableSeq` 曾是**唯一**来源,生产传 1 ⇒ 每个新会话内存里
            //   都从 1 起 ⇒ `EnsureWritable(1)` 判 `1 < 1` = false ⇒ **放行** ⇒ 顶掉磁盘上
            //   上一会话的 `save_1.bin`(实测尸检:`save_1.bak`/`save_5.bak` 保留旧内容
            //   3868B,`save_1.bin`/`save_5.bin` 已是新会话的 1868B)。
            // ⇒ 「槽位只进不退」(GDD 规则十四)语义上**就是跨会话的**,而高水位的唯一合法
            //   来源是**文件名**(不是存档体内容)。故本形参自此是 **floor(下限)**,不是终值。
            // ⚠️ 与 ADR-010 §六 的关系:该条只裁「**锁与安全门**均会话内存、不进存档体」,
            //    **从未**裁过高水位;本类旧注释把「锁与安全门」扩写成「锁与高水位」= 过度引据,
            //    已于 2026-11-03 就地订正(`SaveSlotLedger` 头注 + ADR-010 §六 加注)。
            // ⚠️ 先建目录再扫 —— 目录不存在时 `EnumerateFiles` 会抛,而 ctor 本来就负责建它。
            Directory.CreateDirectory(_basePath);
            _ledger = new SaveSlotLedger(narrativeContext, ResolveInitialNextWritableSeq(initialNextWritableSeq));
        }

        /// <summary>
        /// 注入的折叠谓词(未注入 ⇒ <c>null</c>)。
        /// <para>只读暴露:让装配层与测试可以核对「注入的是哪个实现」,不必反射私有字段。</para>
        /// </summary>
        public IFoldedPredicate FoldedPredicate => _folded;

        /// <summary>
        /// 存档目录。
        /// <para>🔁 2026-11-03(甲方案第一层):7b 的槽位行要知道「哪一槽有档」,
        /// 而 <see cref="SaveSlotProbe"/> 按**同一命名**去读头部。目录必须与
        /// 本服务用的**逐字相同**,否则「屏上说有档、读档说没有」——
        /// 由它对外给出,而不是让装配层各自再拼一次路径(那正是双实现的入口)。</para>
        /// </summary>
        public string BasePath => _basePath;

        /// <summary>槽位台账(锁 / 高水位)。装配层与测试需要驱动叙事锚点时用。</summary>
        public SaveSlotLedger Ledger => _ledger;

        /// <summary>
        /// 叙事事件 checkpoint 已发生 ⇒ **解除全部槽位的读锁**(`AC-7a-19 (d)` 的解除半边)。
        /// <para>锁由 <see cref="Load"/> 成功时置上(`SaveSlotLedger.OnLoaded`);GDD 规则十四:
        /// 「锁持续到下一次叙事事件触发 checkpoint」。本方法即那个解除入口 ——
        /// 生产调用点 = `AnchorCheckpointTrigger.onFired`(`CompositionRoot` 装配),
        /// 白名单 = 脉案落笔 / 病例结案 / 出诊启动。</para>
        /// <para>⚠️ <b>为什么要有这层转发而不是让装配层直接碰 <c>Ledger</c></b>:台账是 7a 的
        /// 内部契约面,装配层只该看见「叙事锚点到了」这个**语义事件**,不该看见
        /// 「把哪个 HashSet 清空」。转发也让「解锁」只有一个可 grep 的生产入口。</para>
        /// </summary>
        public void OnNarrativeAnchor() => _ledger.OnNarrativeAnchor();

        /// <summary>
        /// 存档目录里**最近一份可继续的存档**是哪个槽(无档 / 查询失败 ⇒ <c>false</c>)。
        /// <para>用途:`design/ux/main-menu-42.md §10` 的 `hasContinueSave`(主菜单文案切换)与
        /// §7 的「继续诊治 ⇒ 继续最近档」(2026-11-01 用户裁定 OQ-MM-1 / OQ-MM-2)。</para>
        /// <para><b>「最近」的判据(本类唯一出处,2026-11-03 `TD-persist-17` 起)</b>:
        /// 在**退出保存(槽 1)/ 自动存档(槽 2)/ 全部手动槽(≥ 3)**三者中取**档内 tick 最大**的那一份。
        /// 读不出 / 损坏的候选**不参与**(它们没有可比的时间)。</para>
        /// <para>🔁 原文写「① 退出保存优先 ② 否则取序号最大」—— **该口径已作废**:
        /// 自动存档挪到固定槽 2 之后,「序号最大」会永远选中某个手动槽,
        /// 于是崩溃后「继续诊治」读到的是**更旧**的一份(崩溃损失远超一个间隔)。
        /// 用户 2026-11-03 裁定改为「最新 tick」。</para>
        /// <para>⚠️ 仍刻意**不按文件 mtime** 排序 —— mtime 不是 sim 量,
        /// 且拷贝 / 同步存档会把它改得与语义无关。</para>
        /// <para>⚠️ **只读、不建目录、不抛**:目录不存在 ⇒ <c>false</c>;枚举期任何异常
        /// (权限 / 路径失效)⇒ <b>同样取 <c>false</c></b>(spec §10「查询失败按无存档处理」
        /// —— 保守方向是「开新局」,反过来选「继续」则可能把空档当档读)。失败会 <c>LogWarning</c>,
        /// 不是静默吞掉。</para>
        /// <para>⚠️ 文件名约定**只在本类**:装配层不得自己拼 `save_*.bin`
        /// (否则出现第二份「什么算存档」)。</para>
        /// </summary>
        public bool TryGetMostRecentSave(out SaveSlot slot)
        {
            slot = default;
            LastSaveQueryError = null;
            try
            {
                if (!Directory.Exists(_basePath)) return false;

                bool found = false;
                long bestTick = 0L;
                SaveSlot best = default;

                // 候选 ①/②:两个保留槽(固定身份,各自独立文件名)
                Consider(ExitSaveSlot);
                Consider(AutoSaveSlot);

                // 候选 ③:全部手动槽(≥ 3;1/2 已由上面处理,0 非法)
                foreach (string file in Directory.EnumerateFiles(_basePath, "save_*.bin"))
                {
                    if (!TryParseSlotSeq(Path.GetFileNameWithoutExtension(file), out uint seq)) continue;
                    if (seq < MinManualSlotSeq) continue;
                    Consider(new SaveSlot(seq));
                }

                if (!found) return false;
                slot = best;
                return true;

                void Consider(SaveSlot candidate)
                {
                    // 🔁 2026-11-04(`TD-persist-19` 立、ADR-041 §D4 拆):**占用态**只看
                    //    `ProbeCountsBak`(手动槽 false)—— 覆盖开启后手动槽也有 `.bak`,
                    //    但它不代表「现在有档」;把手动槽的 bak 算进来会重造
                    //    「菜单说有档 ⇒ 按下去说空槽」。
                    string finalPath = GetFinalPath(candidate);
                    string bakPath = SaveSlotLedger.ProbeCountsBak(candidate.SlotSeq)
                        ? GetBakPath(candidate) : null;
                    bool bakExists = bakPath != null && File.Exists(bakPath);
                    bool present = File.Exists(finalPath) || bakExists;
                    if (!present) return;   // 真的没有东西 —— 不参与

                    // final 优先、bak 兜底 —— 与 `LoadBytes` 的次序一致。
                    SaveSlotSummary summary = SaveSlotProbe.ProbeFile(finalPath);
                    if (bakExists && summary.State != SaveSlotState.Occupied)
                        summary = SaveSlotProbe.ProbeFile(bakPath);

                    // ⚠️ **「有档」= 文件存在,不是「能读出 tick」**:
                    //    损坏的档也**要**被报出来 —— 菜单说「开始诊治」会让玩家以为档没了,
                    //    然后安心开新局把它覆盖掉。GDD 规则十一「加载失败硬失败」的口径是
                    //    **报损**,不是**装没有**。
                    //    排序上损坏者取 `long.MinValue` ⇒ 有任何可读的档时它一定排在后面。
                    long tick = summary.State == SaveSlotState.Occupied ? summary.Tick : long.MinValue;

                    // 严格大于 ⇒ 并列时先考虑者胜(退出 > 自动 > 手动的枚举序)
                    if (!found || tick > bestTick)
                    {
                        best = candidate;
                        bestTick = tick;
                        found = true;
                    }
                }
            }
            catch (Exception e)
            {
                // 查询面归本类(唯一出处);失败方向 = 「无存档」(spec §10),但不静默:
                // ⚠️ 本程序集 `noEngineReferences: true`(门 A)⇒ **不能** `Debug.LogWarning`.
                // 失败事实经 <see cref="LastSaveQueryError"/> 交给装配层记日志(它不是静默吞掉,
                // 是把「记哪」放在能记的那一层)。
                LastSaveQueryError = e;
                slot = default;
                return false;
            }
        }

        /// <summary>
        /// 最近一次 <see cref="TryGetMostRecentSave"/> 的**查询失败**原因
        /// (<c>null</c> = 未查询 / 查询成功)。
        /// <para>用途:门 A(`noEngineReferences`)下本程序集无日志面 ⇒ 失败事实只能由装配层记
        /// —— `BootRoot` 读本属性后 `Debug.LogWarning`。⚠️ 它与「查得无档」是**两件事**:
        /// 前者 <c>非 null</c>,后者 <c>null</c> + 返回 <c>false</c>。</para>
        /// </summary>
        public Exception LastSaveQueryError { get; private set; }

        /// <summary>
        /// 从文件名主干解析槽号:`save_{digits}` ⇒ <paramref name="seq"/>;
        /// 其余(含 `save_exit`)⇒ <c>false</c>。**命名约定只在本类**。
        /// </summary>
        private static bool TryParseSlotSeq(string fileNameWithoutExtension, out uint seq)
        {
            seq = 0;
            const string prefix = "save_";
            if (fileNameWithoutExtension == null || !fileNameWithoutExtension.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            int start = prefix.Length;
            if (fileNameWithoutExtension.Length <= start) return false;
            for (int i = start; i < fileNameWithoutExtension.Length; i++)
                if (fileNameWithoutExtension[i] < '0' || fileNameWithoutExtension[i] > '9') return false;
            return uint.TryParse(fileNameWithoutExtension.Substring(start), out seq);
        }

        /// <summary>
        /// 高水位初值的**唯一**求值处(构造期一次)。见 <see cref="TryGetHighestPlayerSlotOnDisk"/>。
        /// </summary>
        /// <param name="floor">装配层给的下限(生产 = 存档位屏的第一个槽号;测试缺省 0)。</param>
        /// <returns>
        /// 磁盘上最大玩家槽号 + 1(与 <paramref name="floor"/> 取大);目录里**没有**玩家槽
        /// ⇒ 原样返回 <paramref name="floor"/>;枚举失败 ⇒ <see cref="uint.MaxValue"/>
        /// (fail-closed:高水位不可知时**拒绝一切写入**,承本仓口径「不可用 &gt; 不安全」)。
        /// </returns>
        private uint ResolveInitialNextWritableSeq(uint floor)
        {
            // 手动槽的 floor 本身不得低于 3(1/2 是保留槽 —— 装配层传错也不许落到那上面)。
            if (floor < MinManualSlotSeq) floor = MinManualSlotSeq;

            if (!TryGetHighestPlayerSlotOnDisk(out uint max))
            {
                // ⚠️ 「枚举失败」与「目录里一张都没有」是**两件事**,处置方向相反:
                //    后者 ⇒ 退回 floor(空目录没有可覆盖的东西);前者 ⇒ fail-closed。
                return _slotScanError == null ? floor : uint.MaxValue;
            }
            // `save_4294967295.bin` 存在时 +1 会回绕成 0 ⇒ 护栏**反向失效**。
            if (max == uint.MaxValue) return uint.MaxValue;
            uint next = max + 1;
            return next > floor ? next : floor;
        }

        /// <summary>
        /// 存档目录里**最大**的玩家槽号(`save_{N}.bin`;退出保存 `save_exit.bin` 因
        /// 「`save_` 后必须全数字」被 <see cref="TryParseSlotSeq"/> 排除)。
        /// <para><b>读的是文件名,不是存档体内容</b> —— 文件存在与否是文件系统自己的事实,
        /// 不是存档体里存的任何值 ⇒ 不触碰 ADR-010 §六 的「不进存档体」。</para>
        /// <para><c>false</c> = 一张玩家槽都没有 **或** 枚举失败;两者用
        /// <see cref="LastSlotScanError"/> 区分。</para>
        /// </summary>
        private bool TryGetHighestPlayerSlotOnDisk(out uint max)
        {
            max = 0;
            _slotScanError = null;
            try
            {
                bool found = false;
                foreach (string file in Directory.EnumerateFiles(_basePath, "save_*.bin"))
                {
                    if (!TryParseSlotSeq(Path.GetFileNameWithoutExtension(file), out uint seq)) continue;
                    // 🔁 2026-11-03(`TD-persist-17`):只认**手动槽**(≥ 3)。
                    //    槽 1 = 退出保存 / 槽 2 = 自动存档,二者是固定槽、不进递增段
                    //    (`save_1.bin` / `save_4.bin` 这类**旧命名**的历史文件就此成为孤儿,
                    //    不再影响高水位 —— 它们也不会被当作手动槽覆盖)。
                    if (seq < MinManualSlotSeq) continue;
                    if (!found || seq > max) { max = seq; found = true; }
                }
                return found;
            }
            catch (Exception e)
            {
                // ⚠️ 本程序集 `noEngineReferences: true`(门 A)⇒ 无日志面。失败事实经
                //    `LastSlotScanError` 交给装配层记(与 `LastSaveQueryError` 同一处置)。
                _slotScanError = e;
                return false;
            }
        }

        /// <summary>
        /// 构造期「扫存档目录取最大槽号」的失败原因(<c>null</c> = 未失败)。
        /// <para>用途:门 A 下本程序集无日志面 ⇒ 失败只能由装配层记(同
        /// <see cref="LastSaveQueryError"/>)。⚠️ 它与「目录里一张玩家槽都没有」是**两件事**:
        /// 后者 <c>null</c> + 高水位 = floor,前者 <c>非 null</c> + 高水位 = `uint.MaxValue`
        /// (拒绝写入)。</para>
        /// </summary>
        public Exception LastSlotScanError => _slotScanError;

        /// <summary>
        /// 上一次**成功**读档的存档头 `ConfigVersion`(`null` = 尚无成功读档 / 最近一次读档失败)。
        /// <para><b>用途</b>:`AC-7a-14` / ADR-010 §七 的「记录并供排查」—— 读档后须把存档头携带的
        /// 配置版本与**当前烘焙产物**的版本比对。比对本身**不在本类**:当前版本由 ADR-014 §五
        /// 的内容哈希派生,那是装配层(`AddressablesDataProvider.LoadGlobalConfigVersion`)的事,
        /// `Sim.Codec` 够不着。⇒ 本类只交出**事实**(一个 u32),比对与标注归
        /// <c>ConfigVersionCheck</c> / 本类的调用方。</para>
        /// <para>⚠️ **语义 = 「成功读档的事实」**:<see cref="Load"/> 入口先清 `null`,
        /// 只有在 `Replay` 阶段完整走完后才回填 —— 读档抛 ⇒ 本属性保持 `null`,
        /// 不会把一份**失败**的档算成「读到过」。</para>
        /// <para>⚠️ **不匹配非致命**(ADR-010 §七):本属性只陈述事实,`Load` 不因两版本
        /// 不同而抛、不重算任何已发生事件、不换后续窗口用的配置(那是 9 / 21a / 52 各自的装载口径)。</para>
        /// </summary>
        public uint? LastLoadedConfigVersion { get; private set; }

        /// <summary>
        /// 最近一次**成功**读档的档头 tick(逻辑时钟)。
        /// <para>⚠️ 2026-11-04 新增(用户报「再进入已有存档,console 很多红色报错」):
        /// 此前档头 tick 读进来**就地丢弃**,于是读档后 `ITickProvider` 还停在 0(或旧值),
        /// 而三流已恢复到最后一条事件的 tick —— 之后每一笔 sim 写入都落在流尾之前,
        /// `EventStream.Append` 以 `BCD-码-3`(Tick 非降)逐条拒绝:console 刷屏,且
        /// **sim 侧一个事件都写不进去**(读进来的世界形同停摆)。</para>
        /// <para>语义与 <see cref="LastLoadedConfigVersion"/> 同款:入口先清 0,只有完整成功的
        /// 读档才回填;失败路径不得沿用上一次的值。</para>
        /// </summary>
        public long LastLoadedTick { get; private set; }

        /// <summary>
        /// 最近一次**成功**读档的槽里记录的时间三参数(ADR-038 §D3 · 存档 v4 起)。
        /// <para><c>null</c> = 本次会话尚未成功读过档(或上一次读档失败)。</para>
        /// <para>⚠️ 语义与 <see cref="LastLoadedConfigVersion"/> 同款:只有完整成功的读档才产生事实,
        /// 失败路径在入口已清空,不得沿用上一次的值。</para>
        /// <para><b>消费者</b>:装配层用它**覆盖**当前构建的数据集值 —— 时间参数属于存档,
        /// 不属于构建(否则旧档会按新值重放出另一个世界)。</para>
        /// </summary>
        public SaveTimeParams? LastLoadedTimeParams { get; private set; }

        /// <summary>
        /// 上一次**成功**读档携带的**逐份**产物 `ConfigVersion`(ADR-037 §④)。
        /// <para><b>空集合的语义 = 「本档不含逐份版本」**(v2 迁上来的旧档,或 v3 但写入时
        /// 未提供逐份数据)⇒ 排查时只能说「无法归因」,**不得**报成「逐份全等」。
        /// 该区分由 <c>ConfigVersionDiff.HasPerArtifactData</c> 承载,不由本属性表达。</para>
        /// <para>⚠️ 与 <see cref="LastLoadedConfigVersion"/> 同款:入口先清空(置 <c>null</c>),
        /// 读档完整成功后才回填。</para>
        /// </summary>
        public IReadOnlyList<ArtifactConfigVersion> LastLoadedArtifactConfigVersions { get; private set; }

        /// <summary>
        /// 后台写盘 worker(AC-7a-07)。**只读暴露**,供夹具与装配收尾等待队列排空。
        /// <para>⚠️ 生产主线程**不得**在 Step 边界调 <see cref="SaveWriteQueue.WaitForIdle()"/>
        /// —— 那等于把异步化撤销。生产唯一的等待点是 <see cref="SaveOnExit"/>。</para>
        /// <para>⚠️ 未注入 <c>writeScheduler</c> 时本属性为 <c>null</c>
        /// (此时写盘路径本就 fail-closed)。</para>
        /// </summary>
        public SaveWriteQueue WriteQueue => _writeQueue;

        /// <summary>
        /// 存档路径的缓冲池(GDD `:123` GC 纪律)。
        /// <para>公开的理由:装配层要把**同一个**池传给 <c>SaveComposer.Compose</c>
        /// (组版在主线程、校验和在 worker,两者共用一份池才有复用意义)。</para>
        /// </summary>
        public SaveBufferPool BufferPool => _bufferPool;

        /// <summary>
        /// 「上一次会话未完整保存」标志位是否已置位(AC-7a-17)。
        /// <para>只读暴露:启动期由装配层读它并**交给 42 / 7b 呈现**(7a 只读位、不出提示 ——
        /// GDD `:586` 逐字「提示承载归 42 / 7b,7a 只断言标志位置位」)。</para>
        /// <para>⚠️ <b>无缓存</b> —— 每次读都落到文件系统。缓存在此会让「上一次会话写了标志、
        /// 这一次读到旧值」成为静默失效通道。</para>
        /// <para>⚠️ 这是**诊断 / 呈现面**,不是第四存档入口 —— 它不写任何东西,故不违反
        /// AC-7a-08 的「恰三处」。</para>
        /// </summary>
        public bool IsSessionIncomplete => SaveIncompleteFlag.IsRaised(_basePath);

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
        /// <para>🔁 <b>2026-11-03(`TD-persist-16` · 用户裁定「甲」)</b>:上句「生产调用点 = 1」
        /// 仍然成立,但**那个调用点的 `bak` 参数是恒不产生文件的** —— 「槽位只进不退」真正成立后
        /// (`TD-persist-15`),`save_{N}.bin` 每个槽号**一生只写一次**,而 <see cref="WriteAtomic"/>
        /// 只在 **`final` 已存在**时才搬 bak ⇒ `save_{N}.bak` **永不出现**。
        /// ⇒ **不要**在这里「补一个 bak」,也不要写「玩家槽回退到 bak」的夹具:
        /// 那不是缺口,是**已裁的口径**(ADR-010 §四 加注 · GDD 规则十一 / `AC-7a-15` 订正)。
        /// 真实存在 bak 的只有 **`save_exit.bin`**(每会话重写)、**`save_auto.bin`**(每次
        /// checkpoint 重写)与「会话未完整」标志位。</para>
        /// <para>🔁 **2026-11-04(`TD-persist-19`)**:上一段的「不要写玩家槽 bak 夹具」当时只管
        /// **写侧**;读侧 `LoadBytes` 仍在无条件回退,⇒ 盘上的旧残留 `.bak` 会被读回来。
        /// 本轮把读侧对齐(`SaveSlotLedger.FallsBackToBak`),并新增
        /// 「手动槽**不**回退」的夹具(`manual_slot_bak_scope_test.cs`)。</para>
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

        /// <summary>读档:校验和验证 + **保留槽**自动回退 bak(手动槽不回退,见下)。</summary>
        /// <remarks>
        /// 🔁 **2026-11-04(`TD-persist-19`)**:回退面按槽型分派 ——
        /// `SaveSlotLedger.FallsBackToBak` 判「该槽的 `.bak` 参不参与回退」。
        /// 手动槽(≥ 3)返回 <c>false</c> ⇒ 校验坏 ⇒ **直接** `SaveCorruptException`;
        /// final 缺失(哪怕盘上有一份旧残留 `.bak`)⇒ **空槽**(`SaveNotFoundException`)。
        /// <para>⚠️ 这两条**不是**「更严格」,是**按已裁口径实现**:`ADR-010 §四` 2026-11-03 加注
        /// 已写「玩家槽无 bak ⇒ 损坏走 Corrupt 报损、不静默兜底」,只是代码当时没跟上。</para>
        /// </remarks>
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
            // 🔁 2026-11-03(用户实测:「继续诊治」每次 Play 都报 SaveNotFoundException,
            //    slot=4294967295):退出保存的哨兵此前**只有写侧**认得,读侧照玩家槽拼路径
            //    ⇒ 去开 `save_4294967295.bin` ⇒ 恒空槽。现在读写共用同一出处。
            // 🔁 2026-11-03(`TD-persist-17`):`GetFinalPath` / `GetBakPath` 现已**内部分派**
            //    保留槽(1 = 退出 / 2 = 自动)⇒ 读侧不再需要自带 if。
            //    此前「哨兵只有写侧认得」曾让「继续诊治」恒 `SaveNotFoundException`。
            string finalPath = GetFinalPath(slot);

            // 🔁 2026-11-04(ADR-041 §D4):**读侧对手动槽也回退 `.bak`** —— 判据 = `ReadFallsBackToBak`。
            //    覆盖开启后 `save_N.bak` 第一次真的由 `WriteAtomic` 产生(它就是这一槽的**上一版**),
            //    回退它是抢救;而「占用态」那一半仍由 `ProbeCountsBak` 独立判,两处**不得混用**。
            string bakPath = SaveSlotLedger.ReadFallsBackToBak(slot.SlotSeq) ? GetBakPath(slot) : null;

            bool finalExists = File.Exists(finalPath);
            if (finalExists)
            {
                byte[] data = File.ReadAllBytes(finalPath);
                if (ValidateChecksum(data)) return data;
            }

            // 回退 bak。
            // ⚠️ **只在「final 缺失但 bak 算占用」或「final 在但坏」时才允许**(ADR-041 §D4):
            //    · 保留槽:bak 算占用(`ProbeCountsBak` true)⇒ final 缺失也回退(旧行为);
            //    · 手动槽:bak **不算占用**(探测说「空」)⇒ final 缺失时**不得**回退 ——
            //      否则「屏上说空槽、读档却读得出来」就是新的分叉(TD-persist-19 的同一形态)。
            bool bakExists = bakPath != null && File.Exists(bakPath);
            bool bakCountsAsOccupancy = !finalExists && SaveSlotLedger.ProbeCountsBak(slot.SlotSeq);
            if (bakExists && (finalExists || bakCountsAsOccupancy))
            {
                byte[] data = File.ReadAllBytes(bakPath);
                if (ValidateChecksum(data)) return data;
            }

            // 🔁 2026-10-31(走查实测后按用户裁定「甲」拆分):**两个态必须分开**。
            //    此前「两级文件都不存在」与「两级都坏」并成一类 ⇒ 玩家点一个从没存过的
            //    空槽,控制台打红字「存档损坏且无法回退」——拒绝载入是对的,话是错的,
            //    而且会让玩家以为自己丢了档。
            //    判据 = **磁盘上有没有东西**:一个都没有 = 空槽(正常态);有但校验不过 = 损坏。
            if (!finalExists && !(bakExists && bakCountsAsOccupancy))
            {
                // ⚠️ 同样**零 I/O** —— 不建空档、不删残档。空槽是正常态,不是要「修」的东西。
                // 🔁 ADR-041 §D4:手动槽只剩 `.bak` 也按**空槽**处理(与探测同向)。
                throw new SaveNotFoundException(slot);
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

        /// <summary>
        /// 剥掉校验和槽 —— <see cref="AddChecksum"/> 的逆操作(不加验证,验证归
        /// <see cref="ValidateChecksum"/>)。
        /// <para>输出形态 = `magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4)
        /// + tick(8) + snapshotOffset(8) + 正文`,即迁移链的工作形态(ADR-010 §七)。</para>
        /// <para>🔁 2026-10-25 `AC-7a-14`(`TD-persist-05` 动作 ③)新增 —— 迁移链须在
        /// **不含校验和**的形态上工作:校验和覆盖它之后的全部字节,正文一变就必须重算;
        /// 链在最后统一算一次 `AddChecksum`,不让每级各自算(那样「某步忘了重算」会成为
        /// 一个静默的坏档来源)。</para>
        /// </summary>
        /// <exception cref="ArgumentException">短于 checksum 槽。</exception>
        public static byte[] StripChecksum(byte[] dataWithChecksum)
        {
            if (dataWithChecksum == null || dataWithChecksum.Length < 36)
                throw new ArgumentException("数据过短");

            byte[] result = new byte[dataWithChecksum.Length - 32];
            Array.Copy(dataWithChecksum, 0, result, 0, 4);                  // magic(4)
            Array.Copy(dataWithChecksum, 36, result, 4, dataWithChecksum.Length - 36); // 其余
            return result;
        }

        /// <summary>计算并写入 checksum。</summary>
        public static byte[] AddChecksum(byte[] dataWithoutChecksum)
            => AddChecksum(dataWithoutChecksum, null);

        /// <summary>
        /// 计算并写入 checksum(可指定缓冲池)。
        /// <para>🔁 <b>2026-10-25 AC-7a-07 新增重载</b>:worker 路径传
        /// <see cref="BufferPool"/>,让产出缓冲入池复用(GDD `:123` GC 纪律)。
        /// 无池版本(<see cref="AddChecksum(byte[])"/>)保持原语义,22 处既有夹具零改动。</para>
        /// </summary>
        /// <param name="dataWithoutChecksum">不含校验和槽的字节。</param>
        /// <param name="pool">缓冲池;null ⇒ 直接分配。</param>
        public static byte[] AddChecksum(byte[] dataWithoutChecksum, SaveBufferPool pool)
        {
            // dataWithoutChecksum = magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4) + tick(8) + snapshotOffset(8) + body
            // 输出 = magic(4) + checksum(32) + saveVersion(4) + ... + body
            if (dataWithoutChecksum == null || dataWithoutChecksum.Length < 32)
                throw new ArgumentException("数据过短");

            byte[] result = pool == null
                ? new byte[dataWithoutChecksum.Length + 32]
                : pool.Rent(dataWithoutChecksum.Length + 32);

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
        /// 存档(唯一写入口)。门 → 组版(**主线程**)→ 入队 → **立即返回**。
        /// <para>🔁 <b>2026-10-25 AC-7a-07 改造</b>:此前是「门 → 组版 → 同步写盘 →
        /// 推高水位」,写盘 IO 与 SHA256 全在主线程。现在:</para>
        /// <list type="number">
        /// <item>门与组版仍在**主线程**(GDD `:509` 逐字「序列化在 Step 边界主线程完成」);</item>
        /// <item>SHA256 回填 + 原子写 + 推高水位闭成**一个工作项**交后台 worker;</item>
        /// <item>本方法返回 = 「已受理」,**不表示已落盘**。</item>
        /// </list>
        /// <para>⚠️ <b>高水位在 worker 写成功后才推</b>(用户 2026-10-25 裁定):保住
        /// <see cref="SaveSlotLedger.OnCheckpointCommitted"/> 的既有不变量「写失败则高水位不动,
        /// 否则一次失败的写会永久吃掉一个槽位」。⇒ 断言 <c>NextWritableSeq</c> 的夹具
        /// 须先 <see cref="SaveWriteQueue.WaitForIdle()"/>。</para>
        /// <para>⚠️ 写失败**不在本方法上报** —— worker 顶层全兜(GDD `:117`),
        /// 失败经 <see cref="SaveWriteQueue.LastFailure"/> 暴露。这是异步化的固有代价:
        /// 调用点已经返回了,没有异常可抛给谁。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 未注入 <c>narrativeContext</c> / <c>compose</c> / <c>writeScheduler</c>(fail-closed);
        /// 或不在叙事上下文内。
        /// </exception>
        /// <exception cref="InvalidDataException">槽位只进不退违例(覆盖既有槽位)。</exception>
        public void Checkpoint(SaveSlot slot)
        {
            // 门 ①:fail-closed —— 三个必需驱动源缺一即拒
            RequireNarrativeContext();
            RequireCompose(nameof(Checkpoint));
            RequireWriteScheduler(nameof(Checkpoint));

            // 门 ①-b(TD-persist-15):构造期扫目录失败 ⇒ 高水位不可知 ⇒ **拒绝一切写入**。
            // ⚠️ 刻意**不**靠 `EnsureWritable` 兜 —— 那里会把 `uint.MaxValue` 报成
            //    「只进不退违例」,归因错(玩家/日志会以为是自己传了个旧槽号)。
            if (_slotScanError != null)
                throw new InvalidOperationException(
                    "存档目录枚举失败,槽位高水位不可知 ⇒ 拒绝写入(fail-closed,不可用 > 不安全):" +
                    _slotScanError.Message);

            // 门 ②:槽位只进不退 + 叙事上下文(AC-7a-19 (b)(c))
            _ledger.EnsureWritable(slot);

            // 组版:折叠与字节布局全在闭包内(本类不认识 FoldRow)。
            // ⚠️ 组版仍在**主线程** —— 它读 sim 状态(事件列表 / 折叠谓词),
            //    后台线程碰不到这些(GDD `:509`;也是 b2 门 A 的实质约束)。
            byte[] bytes = _compose(slot)
                ?? throw new InvalidOperationException(
                    $"compose 委托对 slot={slot.SlotSeq} 返回 null —— 组版失败须抛,不得写空档");

            // 后台工作项:SHA256 回填 + 原子写 + 推高水位(三者同线程、同序)
            _writeQueue.Enqueue(() =>
            {
                byte[] signed = AddChecksum(bytes, _bufferPool);
                try
                {
                    SaveBytes(slot, signed);
                }
                finally
                {
                    // `signed` 是**本次尝试**的产物,失败后不再需要(重试会重新算一份)
                    // ⇒ 逐次归还。
                    _bufferPool.Return(signed);
                }

                // ⚠️ **`bytes` 只在成功后归还** —— 它在重试时必须仍然有效。
                //    若放进上面的 finally,第一次失败就把块还给池,重试时
                //    `AddChecksum(bytes, …)` 读到的可能是**别的调用方已经覆写过的**内容
                //    ⇒ 静默坏档(而且只在「写失败且池恰好复用了同一块」时出现)。
                //    代价:全部重试都失败时 `bytes` 不入池、交给 GC —— 一次永久失败
                //    少回收一块,可接受(该场景已置「会话未完整」标志位)。
                _bufferPool.Return(bytes);

                // 只在写盘**成功返回之后**推高水位 —— 写失败则高水位不动
                _ledger.OnCheckpointCommitted(slot);
            });
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
            // ⚠️ 驱动源缺失 = **编程错误,不可重试** ⇒ 必须在入队**之前**抛。
            //    否则配置错误会伪装成磁盘故障,等满整个预算然后静默变成
            //    「会话未完整」—— 真 bug 被埋掉。
            RequireNarrativeContext();
            RequireCompose(nameof(SaveOnExit));
            RequireWriteScheduler(nameof(SaveOnExit));

            // 🔁 **2026-10-25 W4 修正(代码评审必修)**:原写 `new SaveSlot(0)` ⇒
            // `GetFinalPath` 与玩家手动槽 0 返回**同一** `save_0.bin`,两者互相覆盖;
            // 且 `SaveOnExit` 刻意不调 `OnCheckpointCommitted` ⇒ 台账「只进不退」不保护该文件。
            // ADR-010 §六 只写「退出钩子,**非玩家可选槽位**」,未定义槽位身份 ——
            // 「非玩家可选槽位」的字面读法就要求它**不占玩家槽空间**。
            // ⇒ 退出保存改走独立文件名,与 `save_{slot}.bin` 命名空间不相交。
            byte[] bytes = _compose(ExitSaveSlot)
                ?? throw new InvalidOperationException(
                    "compose 委托返回 null —— 组版失败须抛,不得写空档");

            // ── 入队 + **限时 join**(AC-7a-07 · AC-7a-17 · GDD `:244`)──
            // 🔁 **2026-10-25 语义订正(本轮最重要的口径修正)**:
            //    GDD `:244` 逐字「退出保存:**从 `WritingAsync` 起限时 join**(默认 1 s)」
            //    ⇒ `SAVE_EXIT_RETRY_TIMEOUT` 是**等后台 worker 的时间上限**,
            //      不是「重试循环的总预算」。
            //    TD-persist-06 轮把它当成后者用了 —— 在**同步**写下两者恰好等价,
            //    一旦异步化立刻分叉。现在各归其位:
            //      · 本方法的 join 超时 = `SAVE_EXIT_RETRY_TIMEOUT`(`_exitSaveRetryMillis`);
            //      · `IOException` 的重试间隔 = `WRITE_RETRY_DELAY`(在 `SaveWriteQueue` 内)。
            //
            // ⚠️ 复用 worker 而非另起线程:退出时若有一份 checkpoint 正在写,退出保存
            //    **必须排在它后面**(one-writer 单飞)—— 否则两条 rename 会把 bak 覆盖成
            //    半途产物(GDD `:236`)。
            // 🔁 **2026-11-03(`TD-persist-14`):本次退出保存的结局单独记,不读队列级 `LastFailure`。**
            //
            // **病因(实测)**:成败判据原写 `drained && _writeQueue.LastFailure == null`,
            // 而 `drained = WaitForIdle(...)` 把**两种完全不同的情形合并成同一个 `false`**:
            //   (a) 写盘真的失败 —— `TryRunWithRetry` 用尽次数仍抛;
            //   (b) 写盘只是慢 —— worker **仍在正常跑**,只是超了 `SAVE_EXIT_RETRY_TIMEOUT`。
            // 情形 (b) 也落进失败分支 ⇒ 置「会话未完整」⇒ 下次启动报「上次未完整保存」,
            // **而那次会话正常退出、档最终也写成了**。用户裁定:「这种 bug 情况不该出现」。
            //
            // ⚠️ **为什么不用队列级的 `LastFailure` 兜底**:它是**粘性**的(跨工作项保留),
            //    若本次退出保存还没跑、而**更早的一份 checkpoint 失败过**,读到的会是那份旧异常
            //    ⇒ 置位归因错(把 checkpoint 的失败记成退出保存的失败)。
            //    ⇒ 判据必须锚在**本次工作项自己**的结局上。
            //
            // ⚠️ 跨线程读写:worker 线程写、主线程读。`drained == true` 时有 `WaitForIdle` 的
            //    锁边沿提供可见性;`drained == false` 时**没有** —— 故显式用锁,不靠时序碰运气。
            Exception exitFailure = null;
            bool exitCompleted = false;
            var outcomeGate = new object();

            _writeQueue.Enqueue(() =>
            {
                try
                {
                    byte[] signed = AddChecksum(bytes, _bufferPool);
                    try { WriteExitSave(signed); }
                    finally { _bufferPool.Return(signed); }
                    // 退出保存**不推高水位**(ADR-010 §六:退出钩子不占玩家槽)
                    _bufferPool.Return(bytes);
                    lock (outcomeGate) { exitCompleted = true; exitFailure = null; }
                }
                catch (Exception e)
                {
                    // ⚠️ 记录后**仍要 throw** —— 队列的 `TryRunWithRetry` 靠异常做退避重试;
                    //    吞掉异常会让「第一次失败、第二次成功」被记成一次干净成功。
                    lock (outcomeGate) { exitFailure = e; }
                    throw;
                }
            });

            // 「队列空了」是**时序**事实;「本次工作项成功」是**结果**事实。两者各自独立。
            // ⚠️ `WaitForIdle` 的**返回值不再参与成败判定** —— 超时只说明「还没跑完」,
            //    不说明「失败」。结局一律取本次工作项自己记下的 `exitCompleted` / `exitFailure`。
            _writeQueue.WaitForIdle(_exitSaveRetryMillis);

            Exception writeFailure;
            bool completed;
            lock (outcomeGate) { completed = exitCompleted; writeFailure = exitFailure; }

            if (completed && writeFailure == null)
            {
                // ✅ 世界状态已安全落盘 ⇒ 清「会话未完整」标志位。
                // ⚠️ **清位失败不影响退出**(退出路径的硬要求是「不卡退」)。
                //    清不掉只会让下次启动多提示一次 —— 偏保守方向,可接受。
                try { ClearIncompleteFlag(); }
                catch (Exception e)
                {
                    // 无日志设施(Sim.Codec 是纯 BCL 装配)—— 静默吞。
                    // ⚠️ 这是**刻意的**:退出路径上抛任何东西都违反「不卡退」。
                    _ = e;
                }
                return;
            }

            if (writeFailure != null)
            {
                // ── (a) 写盘**真的失败**:置「会话未完整」标志位,然后**继续退出**(不阻塞)──
                // AC-7a-17 原文:「仍失败 ⇒ **不阻塞退出 + 记日志**;下次启动检测到标志位 ⇒ 提示」。
                //
                // ⚠️ 本类**不抛** —— 调用方(退出钩子)若收到异常就会卡住退出流程。
                //    「不静默丢,不卡退」两条里,后者是硬约束:**日志 + 置位**就是这里的「不静默」。
                try { RaiseIncompleteFlag(); }
                catch (Exception e)
                {
                    // 置位本身失败(目录只读 / 磁盘满)—— 仍不得阻塞退出。
                    // ⚠️ **失败也要让装配层知道**:置位失败 + 写盘失败 = 两条线索都没了。
                    ReportExitSaveFailure(writeFailure, e);
                    return;
                }

                ReportExitSaveFailure(writeFailure, flagFailure: null);
                return;
            }

            // ── (b) join 超时但**未观测到失败**:worker 可能仍在写,结局未知 ──
            // ⚠️ **不置位** —— 这是本债的核心裁定。把「慢」记成「数据不完整」,
            //    玩家会被告知「上次没存好」,而档其实是好的。
            // ⚠️ 但**仍要记日志**(GDD「记日志」那半句):「超预算」是真实的性能事实,
            //    只是它**不是**数据完整性事实。两条线索不合并。
            ReportExitSaveFailure(writeFailure: null, flagFailure: null);
        }

        /// <summary>
        /// 把退出保存的失败交给注入的观察者(装配层 → 日志)。
        /// <para>⚠️ **观察者自身抛异常也不得影响退出** —— 退出路径上任何抛出都违反
        /// 「不卡退」。故此处双重兜住。</para>
        /// </summary>
        private void ReportExitSaveFailure(Exception writeFailure, Exception flagFailure)
        {
            if (_exitSaveFailureSink == null) return;
            try { _exitSaveFailureSink(writeFailure, flagFailure); }
            catch (Exception) { /* 日志失败不得卡退 */ }
        }

        /// <summary>
        /// 退出保存的文件名(独立于 `save_{slot_seq}.bin` 命名空间)。
        /// <para>承 ADR-010 §六「退出钩子,**非玩家可选槽位**」—— 玩家槽位恒为
        /// <see cref="SaveSlot"/> 的 `slot_seq`,退出保存不参与该序列。</para>
        /// </summary>
        public const string ExitSaveFileName = "save_exit.bin";

        /// <summary>自动存档的文件名(独立于 `save_{slot_seq}.bin` 命名空间)。</summary>
        public const string AutoSaveFileName = "save_auto.bin";

        /// <summary>
        /// 手动存档的**第一个**槽号。**槽 1 = 退出保存 · 槽 2 = 自动存档 · 槽 3+ = 手动**。
        /// <para>🔁 <b>2026-11-03(`TD-persist-17` · 用户裁定)</b>:此前退出保存用
        /// `uint.MaxValue` 哨兵、自动存档与手动存档**共用**同一个递增计数器 ⇒ 自动存档会
        /// 一路吃掉手动槽号。现改为**固定槽**:1 / 2 被反复重写(豁免「只进不退」),
        /// 手动槽从 3 起、仍然只进不退。</para>
        /// </summary>
        public const uint MinManualSlotSeq = SaveSlotLedger.MinManualSlotSeq;

        /// <summary>槽 1 = 退出保存。</summary>
        public static readonly SaveSlot ExitSaveSlot = new SaveSlot(SaveSlotLedger.ExitSlotSeq);

        /// <summary>
        /// 槽 2 = **自动存档**(定时兜底 + 叙事事件锚点共用,**原地覆盖**)。
        /// <para>⚠️ 它**不是**玩家的回滚点 —— 每次自动 checkpoint 都把上一份顶掉,
        /// 所以它永远只代表「最近一次自动存档」。这正是反 save-scum 的方向。</para>
        /// </summary>
        public static readonly SaveSlot AutoSaveSlot = new SaveSlot(SaveSlotLedger.AutoSlotSeq);

        /// <summary>该槽位是否是退出保存。</summary>
        public static bool IsExitSaveSlot(SaveSlot slot) => slot.SlotSeq == ExitSaveSlot.SlotSeq;

        /// <summary>该槽位是否是自动存档。</summary>
        public static bool IsAutoSaveSlot(SaveSlot slot) => slot.SlotSeq == AutoSaveSlot.SlotSeq;

        /// <summary>
        /// 保留槽(1 / 2):固定身份、**被反复重写** ⇒ 豁免「只进不退」且**不推进**高水位。
        /// <para>⚠️ 槽 0 **不是**保留槽 —— 它根本不是合法槽号(见
        /// <see cref="SaveSlotLedger.EnsureWritable"/> 的拒收)。</para>
        /// </summary>
        public static bool IsReservedSlot(SaveSlot slot) => SaveSlotLedger.IsReservedSlotSeq(slot.SlotSeq);

        /// <summary>最终文件名(含 `.bin`)。**命名约定只在本类**。</summary>
        private static string FinalNameOf(SaveSlot slot)
            => IsExitSaveSlot(slot) ? ExitSaveFileName
             : IsAutoSaveSlot(slot) ? AutoSaveFileName
             : $"save_{slot.SlotSeq}.bin";

        /// <summary>
        /// 带后缀的旁路文件名(`.tmp` / `.bak`)。
        /// <para>⚠️ 两种形状**必须**并存:保留槽是 `save_exit.bin.bak`(后缀加在 `.bin` 之后),
        /// 手动槽是 `save_3.bak`(`.bin` 被替换)。写错一处 = 读侧找不到自己的备份。</para>
        /// </summary>
        private static string SuffixNameOf(SaveSlot slot, string suffix)
            => IsReservedSlot(slot) ? FinalNameOf(slot) + suffix : $"save_{slot.SlotSeq}{suffix}";

        /// <summary>退出保存的最终文件。</summary>
        private string ExitFinalPath => Path.Combine(_basePath, FinalNameOf(ExitSaveSlot));

        /// <summary>
        /// 退出保存的备份。
        /// <para>⚠️ 是 `save_exit.bin.bak`,**不是** `save_exit.bak` —— 与玩家槽的
        /// `save_{n}.bak` 形状不同。读侧若自己拼一遍就会拼错,故两处共用本属性。</para>
        /// </summary>
        private string ExitBakPath => Path.Combine(_basePath, SuffixNameOf(ExitSaveSlot, ".bak"));

        /// <summary>
        /// 退出保存 —— 与 <see cref="SaveBytes"/> **共用同一原子写原语**
        /// (<see cref="WriteAtomic"/>),只换文件名。
        /// <para>文件名**独立**于玩家槽命名空间 —— 不与任何 `save_{slot_seq}.bin` 相撞。</para>
        /// </summary>
        private void WriteExitSave(byte[] data)
            => WriteAtomic(ExitFinalPath,
                           Path.Combine(_basePath, SuffixNameOf(ExitSaveSlot, ".tmp")),
                           ExitBakPath, data);

        // ══════════════════════════════════════════════════════════════════
        // 「会话未完整」标志位的写面(AC-7a-17 · 规则十三)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 置「会话未完整」标志位 —— **经 <see cref="WriteAtomic"/>**(唯一写盘原语)。
        /// <para>⚠️ <b>为什么写盘必须落在这里</b>:AC-7a-08③ 的 IL 门
        /// (`SaveWritePathGate`)要求 `Sim.Codec` 程序集内**仅 `SaveService::WriteAtomic`
        /// 一处**可达写盘 API。首版把置位直接写成 `File.WriteAllBytes` 撞红了该门
        /// (全量回归 `td06-3` 实测 3 条);用户 2026-10-25 裁定取「走 `WriteAtomic`」
        /// 而非放宽门 —— 门一字不改,既有不变量反而更强。</para>
        /// <para>✅ <b>幂等</b>:重复置位只是重写同一 1 字节内容。退出路径上重试失败
        /// 可能调用多次,抛异常会让「不阻塞退出」这条硬要求失效。</para>
        /// <para>⚠️ 本方法**自身可能失败**(目录只读 / 磁盘满)。调用方(退出路径)
        /// 须自行兜住并记日志 —— 但**不得**因置位失败而阻塞退出(规则十三「不卡退」)。</para>
        /// </summary>
        /// <exception cref="IOException">写标志文件失败(调用方须兜)。</exception>
        private void RaiseIncompleteFlag()
            => WriteAtomic(SaveIncompleteFlag.GetPath(_basePath),
                           SaveIncompleteFlag.GetTmpPath(_basePath),
                           SaveIncompleteFlag.GetBakPath(_basePath),
                           SaveIncompleteFlag.RaisedMarker);

        /// <summary>
        /// 清「会话未完整」标志位 —— 写 **0 字节墓碑**,不走删除。
        /// <para>⚠️ <b>为什么不删</b>:`File.Delete` 也是写盘 API,同样撞 AC-7a-08③ 的门。
        /// 而 <see cref="WriteAtomic"/> 只写不删 ⇒ 清位只能表现为「写空内容」,
        /// 读位判据随之收窄为「存在**且非空**」(见 <see cref="SaveIncompleteFlag.IsRaised"/>)。
        /// 这是为「`Sim.Codec` 内只有一个写盘原语」付的代价,已登记在
        /// `SaveIncompleteFlag` 文件头。</para>
        /// <para>⚠️ 副作用:`WriteAtomic` 的双档逻辑会把上一次的 1 字节内容挪成
        /// `session_incomplete.flag.bak` 并**留在盘上**。该文件不参与任何判据
        /// (读位只读 <see cref="SaveIncompleteFlag.GetPath"/>),属可接受的残留。</para>
        /// </summary>
        /// <exception cref="IOException">写标志文件失败(调用方须兜)。</exception>
        private void ClearIncompleteFlag()
            => WriteAtomic(SaveIncompleteFlag.GetPath(_basePath),
                           SaveIncompleteFlag.GetTmpPath(_basePath),
                           SaveIncompleteFlag.GetBakPath(_basePath),
                           SaveIncompleteFlag.ClearedMarker);

        /// <summary>
        /// 读档。`Gating` → `Verifying` → 头部解析 → **v1 拒收门** → 迁移链三分支 →
        /// `Replaying`(装 blob 池 → 事件列表 → 折叠段原始字节)。
        /// <para>🔁 `TD-persist-03` 修复轮:状态重建 = 装配根把两者喂给
        /// <c>EventStream.Restore(events, rows)</c>(绕 CAP、留去重/单调、Seq 不重发);
        /// 本类只解出与交出。委托次序 = 先事件、后折叠段(见构造器 doc)。</para>
        /// <para>🔁 <b>2026-10-28 ADR-034</b>:新增(一)v1 拒收门 —— `<c>PayloadSegmentMinVersion</c>`
        /// 以下的档**不得**下沉到迁移链(载荷字节从未落盘,迁移在信息论上不可能);
        /// (二)装池步 —— 载荷段解出的 blob 在交出事件列表**之前**装进注入的池。</para>
        /// <para>🔁 <b>2026-10-31 AC-7a-14 执行体</b>:读档成功后把档头的 `ConfigVersion` 记进
        /// <see cref="LastLoadedConfigVersion"/>(此前该字段读进来就丢,「记录并供排查」无执行体)。
        /// 比对与标注**不在本类**(见该属性 doc);本方法**不因**两版本不同而改变任何行为。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 未注入 <c>narrativeContext</c>(fail-closed);不在叙事上下文内;槽位已锁;
        /// 未注入 <c>replaySink</c>;存档含载荷而未注入 <c>blobRestoreSink</c>。
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// 存档损坏且无法回退;magic 不符;版本早于载荷段布局(ADR-034 §⑥)。
        /// </exception>
        public void Load(SaveSlot slot)
        {
            // AC-7a-14:事实出口先清空 —— 本次读档无论是哪一步抛的,都不得沿用上一次的值
            // (「上一份档的 ConfigVersion」冒充「这一份的」= 排查时指向错误的版本对)。
            LastLoadedConfigVersion = null;
            LastLoadedArtifactConfigVersions = null;
            LastLoadedTimeParams = null;
            LastLoadedTick = 0L;
            RequireLoadable(slot);                          // ── Gating ──
            byte[] data = LoadBytes(slot);                  // ── Verifying:校验和 + bak 回退 ──
            SaveHeader header = SaveHeaderCodec.Read(data);
            RequireSupportedVersion(header);                // ── 前向不兼容 / v1 拒收 ──
            bool migrated = header.SaveVersion < SaveHeader.CurrentSaveVersion;
            data = ApplyMigrationsIfNeeded(data, header);   // ── 迁移链 ──
            // ADR-038 §D3:**迁移后重读一次头** —— v3 及更早的档没有时间参数,
            // 三个字段在第一次 `Read` 时是 0;v3→v4 的脚本把它们补进去之后才成立。
            // 不重读 = 拿 0 当「本档的日长」,而 0 会让任何 `FMod` 除零。
            if (migrated) header = SaveHeaderCodec.Read(data);
            RequireTimeParams(header);                      // ── ADR-038:三参数必须可用 ──
            ReplayInto(data, slot);                         // ── Replaying ──
            // AC-7a-14 · ADR-010 §七:回填「已成功读入的档头携带的配置版本」。
            // 位置在 Replay 之后 = 只有完整成功的读档才产生事实(见属性 doc)。
            LastLoadedConfigVersion = header.ConfigVersion;
            // 🔁 2026-11-04:档头 tick 必须交出去 —— 读档后 ITickProvider 要定位到这一刻,
            //    否则后续写入全部早于流尾(`BCD-码-3`)。取**迁移后**的 header(与上面同源)。
            LastLoadedTick = header.Tick;
            // ADR-038 §D3:同理 —— 只有完整成功的读档才交出时间参数。
            LastLoadedTimeParams = new SaveTimeParams(
                header.TicksPerDay, header.NightStart, header.NightSpan,
                header.TicksPerSeason, header.SeasonsPerYear,
                header.WeatherBlockTicks, header.IntensityMax);
        }

        /// <summary>载入前置门:叙事上下文 + 槽位未锁(与写同钩,规则十四 / AC-7a-19 (d))。</summary>
        private void RequireLoadable(SaveSlot slot)
        {
            RequireNarrativeContext();
            if (!_narrativeContext.InNarrativeContext())
                throw new InvalidOperationException(
                    $"不在叙事上下文内,拒绝载入 slot={slot.SlotSeq}" +
                    "(规则十四「载入安全上下文门」—— 读档与写同钩)");
            if (_ledger.IsLocked(slot))
                throw new InvalidOperationException(
                    $"槽位 slot={slot.SlotSeq} 已锁(AC-7a-19 (d) 读档即锁)," +
                    "锁持续到下一次叙事事件 checkpoint");
        }

        /// <summary>版本门:前向不兼容 + v1 及更早拒收(**必须早于**迁移链分支)。</summary>
        /// <remarks>
        /// v1 及更早的布局**没有载荷段**:blob 字节从未写出,信息论上补不出来
        /// ⇒ 不存在、也不可能写出 v1→v2 的迁移脚本(不是「还没写」)。
        /// 若让 v1 下沉进迁移链,空链会抛「版本 1 没有对应迁移脚本」—— 读起来像漏写脚本,
        /// 而真实原因是**信息从未存在**。两句话的处置一样(都拒绝),但归因相反。
        /// <para>⚠️ **与 `SaveMigrationChain` 的第二条不变量同源**:它要求迁移步「末尾等长」
        /// (`TailBody` 取末 N 字节比对)⇒ 连「往末尾补载荷字节」都被结构性地禁掉。</para>
        /// </remarks>
        private static void RequireSupportedVersion(in SaveHeader header)
        {
            if (header.SaveVersion > SaveHeader.CurrentSaveVersion)
                throw new InvalidDataException(
                    $"存档版本 {header.SaveVersion} > 当前 {SaveHeader.CurrentSaveVersion}" +
                    "—— 前向不兼容,拒绝载入");
            if (header.SaveVersion < SaveHeader.PayloadSegmentMinVersion)
                throw new InvalidDataException(
                    $"存档版本 {header.SaveVersion} 早于 v{SaveHeader.PayloadSegmentMinVersion}" +
                    "(首个带载荷段的布局)—— v1 及更早**没有载荷字节**,载荷从未落盘," +
                    "信息不存在 ⇒ 无法迁移(ADR-034 §⑥)。这不是缺迁移脚本");
        }

        /// <summary>
        /// 时间参数可用性门(ADR-038 §D3):v4 起档头必须带一组合法的三参数。
        /// <para>判据与 `Sim.TimeParams.Validate()` 的**关系**同源(这里不做完整校验,
        /// 因为本装配不引用 `Sim`):<c>TPD &gt; 0</c> 且 <c>NIGHT_SPAN ∈ (0, TPD)</c>。</para>
        /// <para>⚠️ 失败 = **拒收该档**,不得回落当前构建的数据集值(那正是静默漂移)。</para>
        /// </summary>
        private static void RequireTimeParams(in SaveHeader header)
        {
            if (header.SaveVersion < 4) return;   // 支持面之外:版本门已在更上方拦下

            if (header.TicksPerDay <= 0)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 TICKS_PER_DAY = {header.TicksPerDay} —— 须 > 0" +
                    "(ADR-038 §D3:日长为 0 会让相位与预算窗口同时失去定义)");

            if (header.NightSpan <= 0 || header.NightSpan >= header.TicksPerDay)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 NIGHT_SPAN = {header.NightSpan} 不在 " +
                    $"(0, {header.TicksPerDay}) 内 —— 拒收(ADR-038 §D3)");

            if (header.SaveVersion < 5) return;   // v4 档没有这四个字段;迁移后重读时才成立

            if (header.TicksPerSeason <= 0)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 TICKS_PER_SEASON = {header.TicksPerSeason} —— 须 > 0" +
                    "(ADR-038 Amendment A:季长为 0 会让季节轴失去定义)");

            if (header.SeasonsPerYear < 2)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 SEASONS_PER_YEAR = {header.SeasonsPerYear} —— 须 ≥ 2" +
                    "(=1 轴退化,与「恒夜」同判据)");

            if (header.WeatherBlockTicks <= 0)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 WEATHER_BLOCK_TICKS = {header.WeatherBlockTicks} —— 须 > 0" +
                    "(块长为 0 = 除零)");

            if (header.IntensityMax <= 0)
                throw new InvalidDataException(
                    $"存档 v{header.SaveVersion} 的 INTENSITY_MAX = {header.IntensityMax} —— 须 > 0");
        }

        /// <summary>
        /// 迁移链接缝:版本低于当前 ⇒ 剥校验和 → 逐级迁 → 补校验和;持平 ⇒ **原样返回同一个数组**。
        /// </summary>
        /// <remarks>
        /// <para>🔁 **2026-10-31 `TD-persist-08` 拆出(原文不删,注释随代码一并搬来)**:此前本段内联在
        /// <see cref="Load"/> 里。拆出的直接动因是 `TD-persist-10` —— 那段接线
        /// (「注册了脚本 ⇒ 链真的被调用、返回值真的被用」)在 `[2, 2)` 空区间下**没有任何可达输入**,
        /// 只能靠读代码守。抽成方法后它**有了可直测的入口**。</para>
        /// <para>SaveVersion == CurrentSaveVersion ⇒ 直通;
        /// SaveVersion &lt; CurrentSaveVersion ⇒ 逐版本升(WorldSeed 逐位保留)。</para>
        /// <para>🔁 **2026-10-25 AC-7a-14(`TD-persist-05` 动作 ③)**:本分支**原为刻意抛
        /// `NotSupportedException` 的 fail-closed 占位**(「不可利用 &gt; 不可用」)。现换成
        /// 真正的迁移链调用 —— 两条不变量(`WorldSeed` / 事件段逐位不变)由
        /// `SaveMigrationChain` 在**每级**断言,不再依赖各脚本自觉。</para>
        /// <para>⚠️ **链为空时的行为**:今日零真实历史版本(`CurrentSaveVersion = 2`,而 `&lt; 2` 的档
        /// 已在 <see cref="RequireSupportedVersion"/> 被拒)⇒ 任何走到这里的档都会有对应脚本;
        /// 缺脚本时 `SaveMigrationChain` 抛「版本 k 没有对应脚本」(fail-closed)。</para>
        /// <para>🔁 **2026-10-28 ADR-034 §⑥**:`CurrentSaveVersion` 已由 1 升到 2,
        /// 所有 `&lt; 2` 的档在更上方被 v1 拒收门拦下(`PayloadSegmentMinVersion = 2`),
        /// ⇒ **本分支今日在 `Load` 路径上无任何可达输入**(`[2, 2)` 是空区间)。
        /// 这不是缺陷,是「v2 是首个带载荷段的布局」的必然;等 `CurrentSaveVersion` 再升 3 时自动复活。
        /// ⚠️ 由此**失去一条接线夹具**:`save_slot_contract_test` 的「注册脚本 ⇒ 链真的被调用」
        /// 此前用「当前版本 − 1」造旧档,现被拒收门截住 ⇒ 该条已就地改写为「v1 档**不得**下沉到链」
        /// 的反向钉子。**该接线缺口 = `TD-persist-10`,2026-10-31 由本条拆出方法后补齐直测夹具。**</para>
        /// </remarks>
        internal byte[] ApplyMigrationsIfNeeded(byte[] data, in SaveHeader header)
        {
            if (header.SaveVersion >= SaveHeader.CurrentSaveVersion) return data;

            // 链工作在「不含校验和」形态(magic + 其余),迁完再补回校验和。
            byte[] withoutChecksum = StripChecksum(data);
            byte[] migrated = SaveMigrationChain.Migrate(
                withoutChecksum, SaveHeader.CurrentSaveVersion, _migrations);
            return AddChecksum(migrated);
        }

        /// <summary>解出事件 / 折叠段 / 载荷,先装池再交出,最后置锁。</summary>
        /// <remarks>
        /// <para>🔁 TD-persist-03 修复轮:契约次序 = 先事件列表、后折叠段字节
        /// (装配根的恢复闭包依赖此次序 —— Restore(events, rows) 要两者齐手)。</para>
        /// <para>⚠️ 装池顺序不可换:`replaySink` / `foldedRowSink` 的装配根实现会立刻开始解码载荷,
        /// 而解码要读池。先解事件后装池 ⇒ 每一个带载荷的事件都会解出「未驻留」。</para>
        /// </remarks>
        private void ReplayInto(byte[] data, SaveSlot slot)
        {
            if (_replaySink == null)
                throw new InvalidOperationException(
                    "未注入 replaySink —— 拒绝静默跳过 Replaying" +
                    "(静默跳过 = 假绿入口)");
            if (_foldedRowSink == null)
                throw new InvalidOperationException(
                    "未注入 foldedRowSink —— 拒绝读档(折叠行是被折叠病人病史的唯一残留," +
                    "静默丢弃 = 不可逆数据丢失;TD-persist-03 动作 ⑤)");

            ParseBody(data, out IReadOnlyList<ArtifactConfigVersion> artifactConfigs,
                      out IReadOnlyList<SimEvent> events,
                      out byte[] foldedSegment, out IReadOnlyList<PayloadEntry> payloads);
            // ADR-037 §④:逐份版本随读档一起交出 —— 「未知(count=0)」与「无差异」的区分
            // 由消费者按 `HasPerArtifactData` 判,本类不做任何降级。
            LastLoadedArtifactConfigVersions = artifactConfigs;

            InstallPayloads(payloads);   // ADR-034 §④:先装池,**再**解事件

            _replaySink(events);
            _foldedRowSink(foldedSegment);

            // ── 锁在 Verifying 通过后置位(用户 2026-10-24 裁定 A) ──
            _ledger.OnLoaded(slot);
        }

        /// <summary>
        /// 解存档体:v2 布局 = <c>事件段(count 前缀 + 逐事件)</c> + <c>折叠段(count 前缀 + 定长行)</c>
        /// + <c>载荷段(count 前缀 + 逐 blob)</c>。
        /// <para>事件段由 <see cref="SimEventListCodec"/> 读尽;其后<b>必跟</b>折叠段
        /// (ADR-010 §一 增补,2026-10-24);折叠段之后<b>必跟</b>载荷段(ADR-034 §②,2026-10-28)。
        /// 三段皆自描述 count 前缀 ⇒ 全程按形状精确读,末尾 <see cref="CodecReader.EnsureFullyConsumed"/>
        /// 兜底(尾随字节 = 布局漂移或损坏)。</para>
        /// <para>⚠️ 折叠段以<b>原始字节</b>交出(本类看不见 <c>FoldRow</c>,解码归装配根);
        /// 载荷段以<b>已解的 <c>(blobId, bytes)</c> 对</b>交出(本类认得布局宽度,见
        /// <see cref="SaveLayout"/>),由调用方装进 <see cref="IBlobRestoreSink"/>。</para>
        /// </summary>
        /// <remarks>
        /// 🔁 <b>2026-10-28 ADR-034 §③ 就地改写(原文不删)</b>:折叠段此前读法是
        /// <c>r.ReadBytes(r.Remaining)</c> —— 「剩下的全给它」。那段代码在 v1 下是**对的**
        /// (折叠段是最后一段),但正是它让「追加载荷段」在结构上不可能:任何追加都会
        /// 被折叠段吞掉,再被 <c>FoldRowCodec.DecodeSegment</c> 的「读尽校验」判成损坏。
        /// ⇒ 改为 <c>count × <see cref="SaveLayout.FoldRowSize"/></c> 精确读。
        /// </remarks>
        private void ParseBody(
            byte[] data,
            out IReadOnlyList<ArtifactConfigVersion> artifactConfigs,
            out IReadOnlyList<SimEvent> events,
            out byte[] foldedSegment,
            out IReadOnlyList<PayloadEntry> payloads)
        {
            // 🔁 2026-11-01(S22-7 · `TD-persist-13`):原 74 代码行按 `── 段 ──` 注释切成
            //    「配置版本段 / 折叠段 / 载荷段」三个私有方法,游标以 `ref CodecReader` 传递。
            //    **分区不变量未变**:校验顺序、拒收报文、游标推进次序逐字保持原样。
            // ADR-038 §D3:v4 起头部多 24 B ⇒ 正文起点**按版本取**,不得硬编码 v3 的 68。
            int offset = SaveHeaderCodec.HeaderSizeFor(SaveHeaderCodec.ReadSaveVersion(data));
            if (data.Length < offset)
                throw new InvalidDataException(
                    $"存档长度 {data.Length} 不足头部 {offset} 字节,无法进入事件段");

            artifactConfigs = ParseConfigVersionSegment(data, ref offset);

            var r = new CodecReader(new ReadOnlySpan<byte>(data, offset, data.Length - offset));
            // 🔁 复合体读:事件段后还有折叠段与载荷段 ⇒ 用无读尽断言的 ReadWithinBody;
            //    存档体的读尽由最末的载荷段闭合(见 ParsePayloadSegment 末尾的 EnsureFullyConsumed)。
            events = SimEventListCodec.ReadWithinBody(ref r, _codec);

            foldedSegment = ParseFoldSegment(data, offset, ref r);
            payloads = ParsePayloadSegment(ref r);
        }

        /// <summary>
        /// 解析配置版本段(ADR-037 §①:紧跟头部、位于事件段之前)。
        /// <para>`count = 0` 是合法语义(「本档不含逐份版本」= v2 迁上来的旧档),由消费侧
        /// <see cref="ConfigVersionDiff.HasPerArtifactData"/> 区分「未知」与「无差异」。</para>
        /// </summary>
        private static IReadOnlyList<ArtifactConfigVersion> ParseConfigVersionSegment(byte[] data, ref int offset)
        {
            IReadOnlyList<ArtifactConfigVersion> configs =
                ConfigVersionSegment.Decode(data, offset, out int configSegmentBytes);
            offset += configSegmentBytes;
            if (data.Length < offset)
                throw new InvalidDataException(
                    $"配置版本段之后无剩余字节(offset={offset}, 长度={data.Length})—— 已损坏");
            return configs;
        }

        /// <summary>
        /// 解析折叠段。段 = `count` 前缀 + 行字节,**整体交出**(行内容归装配根解,本类看不见 `FoldRow`)。
        /// </summary>
        /// <param name="data">整份存档字节(**段字节从原数组直接拷**,不经游标读)。</param>
        /// <param name="segmentBase">事件段在 <paramref name="data"/> 中的起点(游标位置是相对的)。</param>
        /// <param name="r">事件段之后的游标(本方法只推进,不交出)。</param>
        private static byte[] ParseFoldSegment(byte[] data, int segmentBase, ref CodecReader r)
        {
            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"存档事件段后缺折叠段(v2 布局 = 头部 + 事件段 + 折叠段 + 载荷段)," +
                    $"仅剩 {r.Remaining} 字节" +
                    "—— 旧两段式布局按损坏拒收(TD-persist-03 修复轮起折叠段为必备段)");

            int foldStart = r.Position;
            int foldCount = r.ReadInt32LittleEndian();
            if (foldCount < 0)
                throw new InvalidDataException($"折叠段 count={foldCount} 为负 —— 已损坏");
            long foldBytes = (long)foldCount * SaveLayout.FoldRowSize;
            if (foldBytes > r.Remaining)
                throw new InvalidDataException(
                    $"折叠段 count={foldCount} 需 {foldBytes} 字节,仅剩 {r.Remaining} —— 已损坏");

            var foldedSegment = new byte[4 + (int)foldBytes];
            Array.Copy(data, segmentBase + foldStart, foldedSegment, 0, foldedSegment.Length);
            r.ReadBytes((int)foldBytes); // 只推进游标
            return foldedSegment;
        }

        /// <summary>
        /// 解析载荷段(ADR-034 §②)—— 末段,读尽断言在这里闭合。
        /// <para>每条 = 定长头 `{blobId, length}` + 变长字节;`BlobId` 须**严格递增**
        /// (写侧恒升序去重,乱序即布局漂移)。</para>
        /// </summary>
        private static IReadOnlyList<PayloadEntry> ParsePayloadSegment(ref CodecReader r)
        {
            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"存档折叠段后缺载荷段(v2 布局 = 头部 + 事件段 + 折叠段 + 载荷段)," +
                    $"仅剩 {r.Remaining} 字节 —— v1 档须在 Load 的版本门被拒," +
                    "走到这里说明版本号与布局不符");

            int payloadCount = r.ReadInt32LittleEndian();
            if (payloadCount < 0)
                throw new InvalidDataException($"载荷段 count={payloadCount} 为负 —— 已损坏");
            // 每条至少定长头 ⇒ 分配前先用剩余字节复验(防损坏 count 触发巨额分配)
            if ((long)payloadCount * SaveLayout.PayloadEntryHeaderSize > r.Remaining)
                throw new InvalidDataException(
                    $"载荷段 count={payloadCount} 至少需 " +
                    $"{payloadCount * (long)SaveLayout.PayloadEntryHeaderSize} 字节," +
                    $"仅剩 {r.Remaining} —— 已损坏");

            var entries = new PayloadEntry[payloadCount];
            int prevBlobId = int.MinValue;
            for (int i = 0; i < payloadCount; i++)
            {
                entries[i] = ReadPayloadEntry(ref r, i, prevBlobId);
                prevBlobId = entries[i].BlobId;
            }

            // 载荷段是最后一段 ⇒ 尾随字节无处可去。
            r.EnsureFullyConsumed();
            return entries;
        }

        /// <summary>读载荷段第 <paramref name="index"/> 条(含逐条的四条损坏校验)。</summary>
        /// <param name="r">载荷段游标。</param>
        /// <param name="index">条目序号(仅用于报错定位)。</param>
        /// <param name="prevBlobId">前一条的 BlobId(首条传 <see cref="int.MinValue"/>)。</param>
        private static PayloadEntry ReadPayloadEntry(ref CodecReader r, int index, int prevBlobId)
        {
            if (r.Remaining < SaveLayout.PayloadEntryHeaderSize)
                throw new InvalidDataException(
                    $"载荷段第 {index} 条头不足 {SaveLayout.PayloadEntryHeaderSize} 字节" +
                    $"(仅剩 {r.Remaining})—— 已损坏");

            int blobId = r.ReadInt32LittleEndian();
            int length = r.ReadInt32LittleEndian();

            if (blobId < 0)
                throw new InvalidDataException($"载荷段第 {index} 条 BlobId={blobId} 为负 —— 已损坏");
            if (index > 0 && blobId <= prevBlobId)
                throw new InvalidDataException(
                    $"载荷段 BlobId 须严格递增,第 {index} 条 {blobId} ≤ 前一条 {prevBlobId}" +
                    "—— 重复或乱序 = 布局漂移(写侧 ADR-034 §② 恒升序去重)");
            if (length < 0)
                throw new InvalidDataException($"载荷段 BlobId={blobId} 长度 {length} 为负 —— 已损坏");
            if (length > r.Remaining)
                throw new InvalidDataException(
                    $"载荷段 BlobId={blobId} 声明 {length} 字节,仅剩 {r.Remaining} —— 已损坏");

            return new PayloadEntry(blobId, r.ReadBytes(length));
        }

        /// <summary>
        /// 把载荷段解出的 blob 按号装进注入的池(ADR-034 §④ 的第一步)。
        /// <para>空载荷段(无任何存活载荷引用)不要求注入池 —— 那时没有任何东西会丢。
        /// 非空而池缺失 ⇒ <b>抛</b>,不得静默跳过:跳过 = 后续每次解码都解出「未驻留」,
        /// 而那正是这条债原本的症状。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 存档含载荷条目但未注入 <c>blobRestoreSink</c>。
        /// </exception>
        private void InstallPayloads(IReadOnlyList<PayloadEntry> payloads)
        {
            if (payloads.Count == 0) return;

            if (_blobRestoreSink == null)
                throw new InvalidOperationException(
                    $"存档含 {payloads.Count} 个载荷 blob(首个 BlobId={payloads[0].BlobId})," +
                    "但 SaveService 未注入 blobRestoreSink —— 拒绝读档" +
                    "(静默跳过装载 ⇒ 每个带载荷的事件都解出「未驻留」,正是 TD-persist-09 的症状;" +
                    "装配点 = Gameplay.Boot 组合根)");

            for (int i = 0; i < payloads.Count; i++)
                _blobRestoreSink.Restore(payloads[i].BlobId, payloads[i].Bytes);
        }

        /// <summary>载荷段里的一条:<c>(blobId, 原始字节)</c>。仅本类内部传递。</summary>
        private readonly struct PayloadEntry
        {
            public readonly int BlobId;
            public readonly byte[] Bytes;

            public PayloadEntry(int blobId, byte[] bytes)
            {
                BlobId = blobId;
                Bytes = bytes;
            }
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

        /// <summary>
        /// AC-7a-07 的 fail-closed:未注入 <c>writeScheduler</c> ⇒ 拒绝写盘。
        /// <para>⚠️ <b>为什么不用「缺省同步内联」</b>:那会让「生产装配忘了传 <c>Task.Run</c>」
        /// 静默退化为同步写盘,而全部测试仍绿 —— AC-7a-07 在生产路径上假绿。
        /// 本项目已反复抓到这一形态(`SaveSlotLedger` 的 fail-open、44 音频的 Fake 顶替)。</para>
        /// </summary>
        private void RequireWriteScheduler(string caller)
        {
            if (_writeScheduler == null)
                throw new InvalidOperationException(
                    $"{caller}:未注入 writeScheduler 委托(fail-closed,AC-7a-07)—— " +
                    "缺省同步内联会让『生产忘了传 Task.Run』静默退化为同步写盘而测试全绿;" +
                    "生产装配点 = Gameplay.Boot 组合根(传 action => Task.Run(action));" +
                    "夹具传 action => action()(同步内联,可控)。");
        }

        private string GetFinalPath(SaveSlot slot) => Path.Combine(_basePath, FinalNameOf(slot));
        private string GetTmpPath(SaveSlot slot) => Path.Combine(_basePath, SuffixNameOf(slot, ".tmp"));
        private string GetBakPath(SaveSlot slot) => Path.Combine(_basePath, SuffixNameOf(slot, ".bak"));
    }
}
