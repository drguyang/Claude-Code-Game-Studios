// ADR-010 §四 —— 损坏恢复的终态异常(AC-7a-15 (b))。
//
// 权威来源:
//   design/gdd/persistence-service.md:569-573 —— AC-7a-15(b):final 与 bak 都坏 ⇒ 进入
//     `Corrupt` 态、抛**指定异常**、槽目录文件数不变(不静默新建空档、不 null 解引用)。
//   design/gdd/persistence-service.md:257    —— 加载状态机 `Corrupt` 行「显式报损坏」。
//   design/gdd/persistence-service.md:187-189 —— 规则十一:加载失败硬失败,绝不静默兜底。
//   ADR-010 §四 —— 回退逻辑「两级都坏 ⇒ 提示损坏」。
//
// ⚠️ **为什么必须是一个具名子类,而不是裸 `InvalidDataException`**:
//   `Load` 路径上另有三处也抛 `InvalidDataException` —— 前向不兼容(存档版本 > 当前)、
//   头部 magic 不符、事件段/折叠段布局损坏。若终态与之同型,**调用方无法区分**
//   「这份档彻底没了」与「这份档版本不对,换一版程序可能能读」—— 而这两件事对玩家的
//   呈现完全相反(前者 = 报损坏,后者 = 提示升级)。GDD 的 `Corrupt` 是一个**独立的态**,
//   态的边界必须在类型上可表达(承 ADR-013 §9 C3「呈现层不猜」的同源纪律)。
//
// ⚠️ **父类 = `Exception`,不是 `InvalidDataException`,也不是 `IOException`** ——
//    两条都是**实测**得出的,不是取舍:
//    ① `System.IO.InvalidDataException` 在 BCL 里是 **`sealed`** ⇒ 不可继承
//       (首版按它写,Unity 编译直接红:`error CS0509: cannot derive from sealed type`)。
//    ② **刻意不选 `IOException`** —— 它会被下游 `catch (IOException)`(本仓 `BindingsStore`
//       五处就是这种形)当成「I/O 暂时失败,可重试」**静默吞掉**,而 `Corrupt` 是**不可重试**
//       的终态。让它在类型树上**落单**,任何兜底都必须显式点名,才谈得上「态的边界可表达」。
//    ⚠️ 反过来说:**兼容面本来就不存在** —— 全仓 `catch (InvalidDataException)` 零命中
//       (`grep -rn "catch (InvalidDataException" unity/Assets/` ⇒ 0),故无向后兼容义务。
//
// ⚠️ **本类不携带「是否已提示」状态** —— 提示承载归 42 / 7b(承 AC-7a-17 同款口径:
//   7a 只出信号,呈现归下游)。异常本身就是那个信号。
//
// ⚠️ **本类不建任何文件、不删任何文件** —— AC-7a-15(b) 的「槽目录文件数不变」是
//   一条**禁止**判据:进入 `Corrupt` 态的动作面 = 抛这一个异常,零 I/O。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 存档进入 <c>Corrupt</c> 态:目标槽的 <c>final</c> 与 <c>bak</c> **双双**校验失败,
    /// 无任何可回退的上一好档。
    /// <para>承载 AC-7a-15(b)(BLOCKING):「显式报损坏;**不**静默新建空档」。
    /// 抛出本异常即表示加载流程终止在 <c>Corrupt</c> 分支,且未触碰磁盘。</para>
    /// <para>⚠️ 调用方**不得**在捕获后尝试重建空档 —— 静默重建 = 抹掉真相
    /// (GDD <c>:344</c>)。</para>
    /// </summary>
    public sealed class SaveCorruptException : Exception
    {
        /// <summary>进入 <c>Corrupt</c> 态的那个槽位序号。</summary>
        /// <para>供上游在提示里点名「哪个槽坏了」(呈现归 42 / 7b)。</para>
        public uint SlotSeq { get; }

        /// <summary>构造。</summary>
        /// <param name="slot">两级均坏的槽位。</param>
        public SaveCorruptException(SaveSlot slot)
            : base($"存档损坏且无法回退:final 与 bak 均校验失败 —— 进入 Corrupt 态" +
                   $"(slot={slot.SlotSeq});不重建空档(规则十一 / AC-7a-15(b))")
        {
            SlotSeq = slot.SlotSeq;
        }
    }
}
