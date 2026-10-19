// 权威来源:ADR-019 §三(51 住边界层 · **只读消费者**,不是第四个 `IEventSink` 写入者)·
//   `design/gdd/telemetry-analytics.md` R1(唯一输入面 = `ITelemetrySource` 的三个 `Read*`)·
//   R4(接口层不存在 `Upload`/`Send`/`Post`)· `AC-51-A1`(BLOCKING 反射断言)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ 本文件是 TD-epic-01 的还债件(2026-10-19 第二片):`readonly_boundary_test.cs` 原先
//    `Assert.Ignore("ITelemetrySource 未实现…")` —— 契约面无主语,AC-19-01 的语义层无承载。
//
// ⚠️ **三个 Read* 一一对应 ADR-009 的三条逻辑流**(病史 / 病例 / 世界):
//    51 只读订阅这三条,**不新增流、不写流**。返回 `IReadOnlyList<SimEvent>`(GDD AC-51-A1 逐字)。
// ⚠️ **写面刻意不存在**:本程序集无任何 `Append`/`Write`/`Emit` 成员 ⇒
//    「51 不得写三流」是**编译期不可表达**,不靠纪律守(与 ADR-031 ⑤ 的 freehand 路径同型设计)。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>51 遥测的**唯一输入面** —— 三条逻辑流的只读订阅(GDD R1 · ADR-019 §三)。
    /// <para>⚠️ **恰好三个 `Read*`**(`AC-51-A1` 的断言主语):对应 ADR-009 的病史 / 病例 / 世界三流。</para>
    /// <para>⚠️ **零写面**:没有 `Append` / `Write` / `Emit`,也没有 `IEventSink` 形参
    /// ⇒「51 不写三流」在类型上不可表达。</para>
    /// <para>⚠️ **只读语义由返回类型承载**:`IReadOnlyList&lt;SimEvent&gt;` 调用方拿不到写权。</para></summary>
    public interface ITelemetrySource
    {
        /// <summary>病史流(病人 / 病程 / 处置)—— 只读副本。</summary>
        IReadOnlyList<SimEvent> ReadHistoryStream();

        /// <summary>病例流(立案 / 结案 / 判断 / 读数为案)—— 只读副本。</summary>
        IReadOnlyList<SimEvent> ReadCaseStream();

        /// <summary>世界流(玩家跨格 / POI 状态 / 掉落身份 / 建造)—— 只读副本。</summary>
        IReadOnlyList<SimEvent> ReadWorldStream();
    }

    /// <summary>51 的**输出面** —— 本地落盘 / 开发者控制台(GDD R5 · ADR-019 §五)。
    /// <para>⚠️ **刻意没有 `Upload` / `Send` / `Post`**(AC-19-02 / AC-51-A4 · GDD R4「51 零出厂」)
    /// ⇒「无出厂数据」同样是**编译期不可表达**,不靠纪律守。</para>
    /// <para>⚠️ 方法名用 `Write`(本地文件)/`Log`(控制台)而非任何网络动词。</para></summary>
    public interface ITelemetrySink
    {
        /// <summary>把汇总写入**本地文件**(供开发者离线分析;ADR-019 §五 的「导出」)。</summary>
        void WriteLocalReport(string fileName, string content);

        /// <summary>往**开发者控制台**写一行汇总(仅 Development Build 可见;GDD R5)。</summary>
        void LogDeveloperSummary(string summary);
    }
}
