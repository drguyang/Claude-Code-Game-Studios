// 7b「存盘(落笔)」的 42 侧半 —— 新行在、按了只有一条出口、且不计入槽位行。
//
// ▍为什么一条 EditMode 就够(这一层)
//   本件测的是**呈现契约**:`SaveSlotsScreen` 的新行是否建出来、是否走**同一个**出口、
//   是否污染 `SlotRowIndex` 那一面。它**不**测「按下之后真的写出文件」—— 那要真 `SaveService`
//   + 真写队列,在 `unity/Assets/Tests/PlayMode/save_slot_save_wiring_test.cs`(装配级)。
//   ⚠️ 两条**都要**:只测本件 = 测一个 Fake(本项目 F2 形态);只测 PlayMode = 定位不到
//   「新行算不算槽位行」这种边界。
//
// ▍判据(可证伪)
//   A `unwired_returnsFalse`:未接线 ⇒ `TrySave()` 恒 false,**不抛**(UI 回调里逃异常 = 界面卡住)
//   B `newRow_invokesSink_once`:新行存在,且 `ActivateWritable()` 恰好请求一次落笔
//   C `newRow_notCountedAsSlotRow`:新行**不进** `SlotRowCount` / `RowAt`(`slot_seq` 升序面不变)
//   D `emptySlots_firstFocusableIsNewRow`:空册时开屏落焦落在新行(否则手柄玩家按不到)

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>存档位「落笔」的 42 侧判据。</summary>
    public sealed class save_slot_save_wiring_test
    {
        // ══════════════════════════════════════════════════════════════════
        // A 未接线
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **未接线 ⇒ 返回 false 且不抛**。
        /// <para>为什么「不抛」是判据而不是细节:本方法由 UI 回调驱动(帧内),
        /// 异常逃出会打断当帧输入分发 —— 玩家看到的是「界面卡住」,不是「这次存不了」。</para>
        /// </summary>
        [Test]
        public void test_unwired_trySave_returnsFalseWithoutThrowing()
        {
            var router = new SaveSlotCommandRouter(null, null, null);
            Assert.IsFalse(router.IsSaveWired, "[前置] 未给 SaveService ⇒ 落笔半边未接线");

            bool accepted = false;
            Assert.DoesNotThrow(() => accepted = router.TrySave(), "UI 回调里不得抛");

            Assert.IsFalse(accepted, "未接线 ⇒ 不得声称已受理");
            Assert.IsNull(router.LastSavedSlot, "未受理 ⇒ 不得留下「存过」的痕迹");
        }

        // ══════════════════════════════════════════════════════════════════
        // B / C 新行的存在与边界
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_newRow_exists_andActivating_requestsSaveExactlyOnce()
        {
            var sink = new CountingSink();
            SaveSlotsScreen screen = NewScreen(sink);

            screen.SetSlots(new uint[] { 1u, 2u, 3u });

            Assert.IsNotNull(screen.WritableRow, "槽位行之后必须有「新行」(7b:202 的「末行后 Submit」)");
            Assert.AreEqual(0, sink.SaveCalls, "[前置] 还没按");

            screen.ActivateWritable();
            Assert.AreEqual(1, sink.SaveCalls,
                $"新行确认 = **恰好一次**落笔请求(实际 {sink.SaveCalls})—— 两条输入路径各写一份判定会变 2");

            screen.ActivateWritable();
            Assert.AreEqual(2, sink.SaveCalls, "再按一次 = 再一次请求(不是一次性入口)");
            Assert.AreEqual(0, sink.LoadCalls.Count, "新行不得走载入出口(`TryLoad`)");
        }

        [Test]
        public void test_newRow_isNotCountedAsSlotRow()
        {
            SaveSlotsScreen screen = NewScreen(new CountingSink());
            screen.SetSlots(new uint[] { 1u, 2u, 3u });

            Assert.AreEqual(3, screen.SlotRowCount,
                "新行**不是**槽位行 —— 它没有(也不该有)槽位号,混进去会打乱 `slot_seq` 升序面");
            Assert.IsNull(screen.RowAt(3), "越界仍是 null:第 4 格不是槽位行");
            Assert.AreEqual(3, screen.WritableRow.tabIndex, "但焦点顺序排在槽位行之后");
        }

        // ══════════════════════════════════════════════════════════════════
        // D 空册落焦
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_emptySlots_firstFocusableIsTheNewRow()
        {
            SaveSlotsScreen screen = NewScreen(new CountingSink());
            screen.SetSlots(new uint[0]);

            Assert.AreEqual(0, screen.SlotRowCount, "[前置] 空册");
            Assert.That(screen.FirstFocusable, Is.SameAs(screen.WritableRow),
                "空册时开屏落焦必须落在新行 —— 没有指针的游戏里,焦点悬空 = 一步也走不了");
        }

        // ══════════════════════════════════════════════════════════════════

        private static SaveSlotsScreen NewScreen(ISaveSlotCommandSink sink)
        {
            var screen = new SaveSlotsScreen(new VisualElement(), new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            return screen;
        }

        private sealed class CountingSink : ISaveSlotCommandSink
        {
            public readonly List<uint> LoadCalls = new List<uint>();
            public int SaveCalls;

            public bool TryLoad(uint slotSeq) { LoadCalls.Add(slotSeq); return true; }
            public bool TrySave() { SaveCalls++; return true; }
            public bool TrySaveAt(uint slotSeq) { SaveAtCalls.Add(slotSeq); return true; }
            public readonly List<uint> SaveAtCalls = new List<uint>();
        }
    }
}
