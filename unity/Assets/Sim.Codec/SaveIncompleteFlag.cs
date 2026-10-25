// ADR-010 §六 / GDD `persistence-service.md` 规则十三 —— 「会话未完整」标志位(**命名与判据**)。
//
// 权威来源:
//   GDD `persistence-service.md:195-198` —— 规则十三(退出保存失败 = 限时重试 + 下次启动提示)
//   GDD `persistence-service.md:581-586` —— AC-7a-17(BLOCKING)
//   GDD `persistence-service.md:610` —— 标志位两种形态并列:「头字段 / 标志文件」
//   ADR-010 §六 —— 退出保存;崩溃 / 强杀不触发退出钩子 ⇒ 由下次启动的标志检测兜底
//   `traceability-index.md:1030` —— `TR-persist-010`
//
// ─────────────────────────────────────────────────────────────────────────────
// 形态裁定:独立标志文件(用户 2026-10-25 裁定)
// ─────────────────────────────────────────────────────────────────────────────
// GDD `:610` 原文把两种形态并列:「**会话未完整标志位(头字段 / 标志文件)**」。取**标志文件**,
// 不取头字段,理由不是偏好,是三条连带的硬代价:
//
//   ① **头字段 = 改存档布局**。`SaveHeader` 七字段被 `SaveHeaderCodec` 的定长编码与
//      ADR-010 §一 的黄金夹具**逐字节钉死**(`HeaderSize = FixedSize + 32`)。
//      加一个字段 ⇒ `FixedSize` 变 ⇒ 黄金夹具全红 ⇒ 按 ADR-010 §七 应评估 bump
//      `SaveVersion`(现为 1)⇒ 触发迁移链义务。
//   ② **而 v1 从未出货** —— ADR-010 §一 已明写「零生产消费者、零存档文件」,
//      `TD-persist-05` 也是据此拒绝造「假 v0 迁移脚本」(那测的是夹具的想象力)。
//      ⇒ 为标志位 bump 版本号,会逼出一条**只为标志位存在**的迁移脚本,正是同一形态。
//   ③ **语义上它也不该进存档体**。标志位的含义是「**上一次会话没走完**」——
//      它是**跨会话的元信息**,不是世界状态。写进存档头会让它随存档被复制 / 回退
//      (`bak` 回退后标志位跟着回退 = 标志位本身失真)。
//
// ⇒ 独立文件:与存档体解耦,`bak` 回退不影响它,`SaveVersion` 保持 1,迁移链零影响。
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-10-25 二次裁定(用户):本类**不碰写盘**,写/清归 `SaveService.WriteAtomic`
// ─────────────────────────────────────────────────────────────────────────────
// 首版把置位 / 清位直接写成 `File.WriteAllBytes` / `File.Delete`,结果撞上
// **AC-7a-08③ IL 门**(`SaveWritePathGate`):该门要求 `Sim.Codec` 程序集内
// **仅 `SaveService::WriteAtomic` 一处**可达写盘 API —— 全量回归 `td06-3` 实测 3 条红。
//
// 三条出路里用户裁定取**「走 WriteAtomic」**:门一字不改,且既有不变量从
// 「存档体只有一个写入口」变成**字面成立**的「`Sim.Codec` 内只有一个写盘原语」——
// 比放宽门更强。代价是标志位语义由「文件存在」变为「文件存在**且非空**」
// (写 0 字节墓碑 = 清位),`IsRaised` 的判据随之收窄,见下。
//
// ⇒ 本类退化为**纯命名 + 纯判据**:零 I/O、零目录创建、零异常路径。
//    真正的写盘在 `SaveService.RaiseIncompleteFlag` / `ClearIncompleteFlag`。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本类**只命名 / 只读位**,不出任何提示,也不置位
// ─────────────────────────────────────────────────────────────────────────────
// AC-7a-17 原文括注逐字:「**提示承载归 42 / 7b,7a 只断言标志位置位**」(BLOCKING)。
// ⇒ 本类**没有任何 UI / 日志 / 弹窗**路径。消费方读 `IsRaised` 后自己决定怎么呈现。
//
// ⚠️ **清位时机 = 下一次成功保存**,不是「启动时清」。
//   若启动就清,那么「启动 → 玩家没做任何事 → 崩溃」会让标志位永久丢失,
//   真出问题的那次会话反而无声。清位必须发生在「世界状态已经被安全落盘之后」——
//   见 `SaveService.SaveOnExit` 成功分支。

using System;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 「会话未完整保存」标志位 —— 一份**独立于存档体**的哨兵文件的**命名与判据**。
    /// <para>置位 / 清位 = <see cref="SaveService"/> 经 <c>WriteAtomic</c>(唯一写盘原语);
    /// 读位 = 本类 <see cref="IsRaised"/>,由下次启动检测(AC-7a-17)。</para>
    /// <para><b>零 I/O 写、无状态、无缓存</b> —— 任何进程级缓存都会让
    /// 「上一次会话写了标志、这一次会话读到的是缓存里的旧值」成为静默失效通道。</para>
    /// </summary>
    public static class SaveIncompleteFlag
    {
        /// <summary>
        /// 标志文件名(与 `save_{slot}.bin` / `save_exit.bin` 命名空间不相交)。
        /// <para>刻意不叫 `save_*` 前缀:它**不是存档**,任何按 `save_*.bin` 通配扫目录的
        /// 逻辑(备份 / 清理 / 迁移)都不该把它当成存档。</para>
        /// </summary>
        public const string FlagFileName = "session_incomplete.flag";

        /// <summary>临时文件后缀(与 `SaveService` 的原子写同款三路径手法)。</summary>
        public const string TmpSuffix = ".tmp";

        /// <summary>备份后缀(与 `SaveService` 的原子写同款三路径手法)。</summary>
        public const string BakSuffix = ".bak";

        /// <summary>
        /// 置位时写入的字节 —— **单个 0x01**。
        /// <para>⚠️ 内容本身无契约(没人读它),但**长度有契约**:<see cref="IsRaised"/> 判
        /// 「存在**且非空**」,故置位须写非空内容、清位须写**空**内容。
        /// 取 1 字节而非 `Array.Empty` 的理由:让「置位」与「清位」在文件长度上可分,
        /// 从而清位也能走 `WriteAtomic`(它只写不删),不必给 `Sim.Codec` 开第二条写盘面。</para>
        /// </summary>
        public static readonly byte[] RaisedMarker = { 0x01 };

        /// <summary>清位时写入的字节 —— **空**(0 字节墓碑)。</summary>
        public static readonly byte[] ClearedMarker = Array.Empty<byte>();

        /// <summary>标志文件的完整路径。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="basePath"/> 为 null。</exception>
        public static string GetPath(string basePath)
        {
            if (basePath == null) throw new ArgumentNullException(nameof(basePath));
            return Path.Combine(basePath, FlagFileName);
        }

        /// <summary>临时文件路径(<see cref="SaveService"/> 原子写用)。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="basePath"/> 为 null。</exception>
        public static string GetTmpPath(string basePath) => GetPath(basePath) + TmpSuffix;

        /// <summary>备份文件路径(<see cref="SaveService"/> 原子写用)。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="basePath"/> 为 null。</exception>
        public static string GetBakPath(string basePath) => GetPath(basePath) + BakSuffix;

        /// <summary>
        /// 读取标志位是否已置位 = **文件存在且非空**。
        /// <para>⚠️ <b>只判存在与非空,不解析内容</b> —— 内容无契约(置位写 1 字节 0x01)。
        /// 将来若要携带「哪一次会话」之类的信息,须**另开契约**,不得让本方法
        /// 静默开始解析内容(那会让早已写下的标志文件变成「损坏」)。</para>
        /// <para>⚠️ <b>非空</b>这一半不是装饰:清位 = 写 0 字节墓碑(不走删除,因为
        /// `Sim.Codec` 内只允许一处写盘原语,而 `WriteAtomic` 只写不删)。只判
        /// `File.Exists` 会让清位后的墓碑被读成「已置位」⇒ 提示永不消失。</para>
        /// </summary>
        /// <returns>标志文件存在且长度 &gt; 0 ⇒ true。</returns>
        public static bool IsRaised(string basePath)
        {
            string path = GetPath(basePath);
            if (!File.Exists(path)) return false;

            // 读长度而非读内容 —— 内容无契约。
            // ⚠️ 竞态:Exists 与 Length 之间文件可能被删 ⇒ 吞掉并返回 false
            //    (「读不到」与「未置位」在此处同义:都没有可靠的置位证据)。
            try { return new FileInfo(path).Length > 0; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
