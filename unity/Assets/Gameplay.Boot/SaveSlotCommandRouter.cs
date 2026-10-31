// 7b 存档位 → 7a 载入的**唯一接通点**(TD-persist-04 裁定② 的「通电待信」到此为止)。
//
// 权威来源:
//   · design/gdd/save-slot-ui.md §规则四(存/载钩子)· 规则三(读档即锁)
//   · design/ux/save-slots-7b.md §交互表:「载入 = 无 SimEvent · `ISaveService.Load(SaveSlot)`」
//   · ADR-010 §七(载入自检门全在 `SaveService.Load` 内,本类**不复制**任何一道)
//   · ADR-013 §9 C3(42 只渲染)—— 可执行判定留在这里,不在屏里
//   · 同型先例:`CasebookCommandRouter`(42 事件 → 装配层写者)
//
// ── 本类只做三件事 ───────────────────────────────────────────────────
// ① 把 `uint slotSeq` 包成 `SaveSlot`;② 调 `LoadSequencer.Load`(**不**绕过它去直调
// `SaveService.Load` —— 那样会漏掉读档后的缝合:锚点游标复位 / 兜底复位 / 派生态重建);
// ③ 失败时把异常交给注入的观察者并返回 `false`。
//
// ⚠️ **为什么 catch 而不是让它抛**:本方法由 UI 回调驱动(帧内)。UI 回调里逃出的异常
//    会打断当帧的输入分发,玩家看到的是「界面卡住」而不是「这个槽读不了」。
//    ⇒ 与 `SaveService` 退出保存的处置同款(**不抛给调用方**,但**必须留下记录**,
//    经 `failureSink`)。「吞掉不记」才是禁止形态,「接住并记」不是。
//
// ⚠️ **未接线 ⇒ 返回 false**(不崩、不造动作):与 42 侧「缺写通道 = 留空」同纪律。

using System;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;   // ISaveSlotCommandSink(42 侧契约)
using DaYiJingCheng.Sim.Codec;                  // SaveService / SaveSlot

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>存档位载入路由:42 的载入意图 → <see cref="LoadSequencer"/>。</summary>
    public sealed class SaveSlotCommandRouter : ISaveSlotCommandSink
    {
        private readonly LoadSequencer _sequencer;
        private readonly Action<uint, Exception> _failureSink;

        /// <summary>构造。</summary>
        /// <param name="sequencer">读档缝合序列器(**唯一**载入入口;可 null = 未接线形态,
        /// 此时 <see cref="TryLoad"/> 恒返回 <c>false</c>)。</param>
        /// <param name="failureSink">载入失败的观察者(<c>(槽位, 异常)</c>)。可 null ——
        /// 但 null 意味着失败**不留记录**,仅建议在测试的部分装配里用。</param>
        public SaveSlotCommandRouter(LoadSequencer sequencer,
                                     Action<uint, Exception> failureSink = null)
        {
            _sequencer = sequencer;
            _failureSink = failureSink;
        }

        /// <summary>是否已接线(未接线 = 生产装配缺存档目录的合法形态)。</summary>
        public bool IsWired => _sequencer != null;

        /// <summary>请求载入槽位。异常被接住并交观察者,不向 UI 回调逃逸。</summary>
        /// <inheritdoc cref="ISaveSlotCommandSink.TryLoad"/>
        public bool TryLoad(uint slotSeq)
        {
            if (_sequencer == null) return false;

            try
            {
                _sequencer.Load(new SaveSlot(slotSeq));
                return true;
            }
            catch (Exception ex)
            {
                // ⚠️ 这里**不是**静默吞:记录面是构造注入的观察者(生产 = 引擎日志)。
                //    受检的失败形态 = 槽位不存在 / 校验和与 bak 均坏 / 载入安全上下文门 /
                //    版本门拒收 / 派生态重建失败 —— 逐条都在 `SaveService.Load` / 序列器里。
                _failureSink?.Invoke(slotSeq, ex);
                return false;
            }
        }
    }
}
