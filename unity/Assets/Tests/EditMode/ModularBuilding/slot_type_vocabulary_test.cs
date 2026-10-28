// 槽位类型词表(`Editor.Tools.Bake.SlotTypeVocabulary`)的**判别性**夹具。
//
// 立此件缘由(2026-10-28 代码评审 **A-21 · BLOCK**):
//   全仓曾有**四份**手写名字表,两份 Binder 用「名字表下标 + 1」自产序数,而运行期
//   `BitMask.Single(type) => 1 << (int)type` 用的是**枚举值**。两侧唯一的联系是
//   「`SlotType` 数值恰好 = 下标 + 1」—— **一个从未被任何门断言过的巧合**。
//   Sim 侧在枚举中间插一个类型 ⇒ 四份表照旧编译、全绿、掩码整体错位。
//
// ⚠️ **为什么本件的期望值写**字面量**(`("Decor", 5)` / `64`)而非 `(int)SlotType.Decor`**:
//   自指判据的失败模式是**变异时两边一起动**(改被测枚举 ⇒ 期望值跟着改 ⇒ 零红逃逸)。
//   字面量的期望值不随被测侧移动 ⇒ 「序数被改成下标 / 被减一 / 名字被漏掉」都会当场转红。
//   ⚠️ **本件证明不了的那一件事(如实登记,禁借绿)**:在**今天的**枚举
//   (`None=0 … Shell=6`,连续)下,「下标 + 1」与「枚举值」数值相等,
//   因此**没有任何夹具**能区分这两种实现。本件钉住的是「**当前**的映射就是这六对字面量」
//   —— 枚举一旦重排 / 插值,立刻转红,把「要不要改契约」变成人的决定而不是静默错位。
//
// ⚠️ 本件不写盘(`FsWriteBoundaryGate` 无关)。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>槽位类型词表:名字↔序数、闭集边界、与 Binder 位域的逐位对齐。</summary>
    [TestFixture]
    public class slot_type_vocabulary_test
    {
        /// <summary>名字 → 序数的**字面量**钉板(唯一真源 = `Sim.World.SlotType`,此处是回声)。</summary>
        private static readonly (string Name, int Ordinal)[] LiteralPairs =
        {
            ("Bed", 1),
            ("Table", 2),
            ("Cabinet", 3),
            ("Plot", 4),
            ("Decor", 5),
            ("Shell", 6),
        };

        /// <summary>词表 = 这六对,既不缺也不多;每个名字的序数 = 字面量。</summary>
        /// <remarks><b>判别力</b>:① `OrdinalByName.Add(name, v - 1)`(整体减一,模拟「下标从 0 起」
        /// 这类真实漂移)⇒ 每条序数断言红;② 漏 `continue` 掉某个枚举值 ⇒ `Names` 序列不等 ⇒ 红;
        /// ③ 加一条多余名字 ⇒ 序列不等 ⇒ 红。</remarks>
        [Test]
        public void test_slotTypeVocabulary_nameToOrdinal_isLiteralPinned()
        {
            Assert.That(SlotTypeVocabulary.Names,
                Is.EqualTo(LiteralPairs.Select(p => p.Name).ToArray()),
                "闭集须恰为这六个名字(顺序 = 枚举值升序):\n" +
                string.Join(", ", SlotTypeVocabulary.Names));

            foreach ((string name, int ordinal) in LiteralPairs)
            {
                Assert.That(SlotTypeVocabulary.TryGetOrdinal(name, out int got), Is.True,
                    $"\"{name}\" 必须在闭集内");
                Assert.That(got, Is.EqualTo(ordinal),
                    $"\"{name}\" 的序数须 = {ordinal}(枚举值本身,不是表下标 + 1)");
            }

            Assert.That(SlotTypeVocabulary.ClosedSetText(), Is.EqualTo(
                    "Bed, Table, Cabinet, Plot, Decor, Shell"),
                "报文用的闭集文案须与词表同源(否则作者看到的合法值列表与实际校验不一致)");
        }

        /// <summary>`None` / 未知名 / 大小写变体 / `null` 一律不在闭集(且 `null` 不抛)。</summary>
        /// <remarks><b>判别力</b>:删掉静态构造里的 `if (t == SlotType.None) continue;`
        /// ⇒ `None` 那条红(它会被当作合法名,序数 0 —— 正是「哨兵混进合法值域」的形态)。</remarks>
        [Test]
        public void test_slotTypeVocabulary_noneUnknownAndCaseVariant_areRejected()
        {
            Assert.That(SlotTypeVocabulary.TryGetOrdinal("None", out _), Is.False,
                "`None` 是「未分配」哨兵 —— 让所有模块恒不匹配,不得进合法闭集");
            Assert.That(SlotTypeVocabulary.TryGetOrdinal("decor", out _), Is.False,
                "名字比较须大小写敏感(JSON 里的 `decor` 是作者拼错,不是等价写法)");
            Assert.That(SlotTypeVocabulary.TryGetOrdinal("Bedroom", out _), Is.False,
                "前缀相似不得被当合法名");
            Assert.That(SlotTypeVocabulary.TryGetOrdinal(null, out _), Is.False,
                "`null` 须返回 false,**不得抛**(绑定器在缺字段时也要走到自己的报文分支)");
        }

        /// <summary>Binder 产出的位掩码 = `1 &lt;&lt; 枚举值`,用字面量钉住(`Shell ⇒ 64`、`Plot ⇒ 16`)。</summary>
        /// <remarks><b>判别力</b>:把 `SlotRow.AllowedMask` 改成 `1 &lt;&lt; (TypeOrdinal - 1)`
        /// ⇒ 两条断言都红;把 `TryReadSlotType` 改回「下标 + 1」并同时改坏枚举假设 ⇒ 序数断言红。</remarks>
        [Test]
        public void test_worldBuildSlots_binderMask_matchesLiteralBitForEachType()
        {
            WorldBuildSlotsBinder.BindResult shell = BindSingleSlot("Shell");
            Assert.That(shell.Rows.Count, Is.EqualTo(1));
            Assert.That(shell.Rows[0].TypeOrdinal, Is.EqualTo(6), "Shell 的枚举值 = 6");
            Assert.That(shell.Rows[0].AllowedMask, Is.EqualTo(64), "`1 << 6` = 64");
            Assert.That(shell.Rows[0].TypeName, Is.EqualTo("Shell"), "原名须原样保留(报文与对账用)");

            WorldBuildSlotsBinder.BindResult plot = BindSingleSlot("Plot");
            Assert.That(plot.Rows[0].TypeOrdinal, Is.EqualTo(4), "Plot 的枚举值 = 4");
            Assert.That(plot.Rows[0].AllowedMask, Is.EqualTo(16), "`1 << 4` = 16");
        }

        /// <summary>绑定一张只有一格的最小表。</summary>
        private static WorldBuildSlotsBinder.BindResult BindSingleSlot(string typeName)
            => WorldBuildSlotsBinder.Bind(
                "{\"schema_version\":1,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"" + typeName + "\",\"baked_initial\":false}]}");
    }
}
