// S20-14 · inventory story-005 —— **无负确认**(BL-30①)的信令面 / registry 面 / 接口面测试
// (Logic · EditMode)
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     规则八 :(BL-30①,上文规则九尾)「**勿向 45 索取 ack 通道**」—— 网络层是纯管道、
//       不做语义判断(ADR-001 §一);「意图被接受 / 被拒绝」是**流的内容**,不是管道回执
//     Edge Cases(:613-621)被拒 ⇒ 零事件、`InventoryOf` 前后逐位相同
//   design/registry/entities.yaml —— Kind 闭集真源(ADR-024 §①)
//   story production/epics/inventory-items/story-005-atomicity-consumption-recovery-no-ack.md
//     AC-20-25:零 `PickupRejected`/`IntentAck`/`ConsumeFailed` 类否定事件;
//               被拒意图后世界流零新增;禁向 45 索 ack 通道
//   ADR-001 §一(纯管道)· ADR-005(唯一真源)· ADR-024(registry 真源)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件为什么**不**与 `drop_event_writer_guards_test.cs` 合并
// ─────────────────────────────────────────────────────────────────────────────
// 那份测的是**行为**(校验返回什么、写了几条);本份测的是**闭集**:
//   ① `entities.yaml` 里没有任何否定信令 Kind(registry 面);
//   ② `EventKind` 枚举里也没有(代码面 —— registry 有而枚举无 = kindgen 漂移);
//   ③ 三支 Drop 载荷无 ack / correlation 字段(载荷面);
//   ④ 45 侧接口面零 ack 符号(接口面 —— AC-20-25 原文点名的「禁向 45 索」)。
// 四者**不可互相冒充**:registry 干净不代表代码干净(枚举可手改),
// 载荷干净不代表接口干净(可以在另一个类型上加 ack 通道)。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(只**读**仓库文件,不写)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary>无负确认(BL-30①)的四面包围:registry / 枚举 / 载荷 / 接口。</summary>
    [TestFixture]
    public sealed class NoNegativeAckTest
    {
        /// <summary>三类否定信令 Kind 的**字面名单**(AC-20-25 原文逐字)。
        /// <para>⚠️ 用字面量而不是从别处派生 —— 派生会让夹具随生产侧一起改名,零判别力
        /// (见 `feedback_self_referential_fixture`)。</para></summary>
        private static readonly string[] ForbiddenKindNames =
        {
            "PickupRejected", "IntentAck", "ConsumeFailed",
        };

        // ══════════════════════════════════════════════════════════════════
        // ① registry 面 —— `entities.yaml` 的 Kind 闭集
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2025_registry_declaresNoNegativeSignalKind()
        {
            string path = EntitiesYamlPath();
            Assert.IsTrue(File.Exists(path),
                "Kind 闭集真源须存在(ADR-024 §①):" + path);

            string text = File.ReadAllText(path);

            foreach (string forbidden in ForbiddenKindNames)
            {
                Assert.IsFalse(text.Contains("SimEvent.Kind." + forbidden),
                    $"`entities.yaml` 不得登记 `SimEvent.Kind.{forbidden}` —— AC-20-25(BL-30①)");
            }

            // 正控(承上条):三支 Drop 必须登记着 —— 否则上一条可能是「文件没读到」的空过。
            string[] required = { "DropSpawned", "DropClaimed", "DropDespawned" };
            foreach (string name in required)
            {
                Assert.IsTrue(text.Contains("SimEvent.Kind." + name),
                    $"`entities.yaml` 须登记 `{name}` —— 正控,证明上一条不是空过");
            }
        }

        [Test]
        public void test_ac2025_registryDropKinds_allDeclareStreamWorld()
        {
            // ⚠️ 这不是 AC-20-25 的原文要求,而是它的**结构性前提**:否定信令最容易
            // 以「一条额外事件」的形式溜进来;三支都是世界流 ⇒ 任何新增的「回执」
            // 若挂在别的流上,本件下面的枚举面断言照样看不见它。
            // 判据 = 三支的 `stream:` 行都在 `name:` 之后最近的若干行内取到 world。
            string text = File.ReadAllText(EntitiesYamlPath());

            foreach (string kind in new[] { "DropSpawned", "DropClaimed", "DropDespawned" })
            {
                string block = BlockOf(text, "SimEvent.Kind." + kind);
                Assert.IsNotEmpty(block, $"未找到 `{kind}` 的条目块 —— 夹具定位失败");
                Assert.IsTrue(block.Contains("stream: world"),
                    $"`{kind}` 须声明 `stream: world`(ADR-009 §二)");
                Assert.IsTrue(block.Contains("author:"),
                    $"`{kind}` 须声明 `author:`(ADR-024 §① 必填三件之一)");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 枚举面 —— 代码里的 `EventKind`(与 registry 是**两件事**)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2025_eventKindEnum_hasNoNegativeSignalMember()
        {
            string[] actual = Enum.GetNames(typeof(EventKind));

            foreach (string forbidden in ForbiddenKindNames)
            {
                Assert.IsFalse(actual.Contains(forbidden),
                    $"`EventKind.{forbidden}` 不得存在 —— AC-20-25。当前成员数 {actual.Length}");
            }

            // 正控:枚举确实读到了。
            Assert.IsTrue(actual.Contains("DropSpawned"), "`EventKind` 须可见且含三支 Drop");
        }

        [Test]
        public void test_ac2025_eventKindEnum_namesContainNoAckOrRejectToken()
        {
            // 换名词表:即使改名(`PickupDenied` / `ConsumeRefused` / `OpAck` …),
            // 「这是回执 / 拒绝」的语义仍会落在某个 token 上。
            // ⚠️ 词表是**登记处** —— 加词 = 改本测试(同 AC-21a-63 的换名扫描纪律)。
            // ⚠️ 判据是**词元级**(见 `WordTokens` / `MatchesToken`),不是子串:
            //    子串法会把将来的 `TrackState`(含 "ack")判成回执,而假阳性会逼出白名单,
            //    白名单一旦存在,真回执也能藏进去。
            string[] bannedTokens = { "ack", "reject", "deny", "refuse", "fail" };

            var hits = new List<string>();
            foreach (string name in Enum.GetNames(typeof(EventKind)))
            {
                foreach (string token in WordTokens(name))
                {
                    foreach (string banned in bannedTokens)
                    {
                        if (MatchesToken(token, banned))
                            hits.Add($"EventKind.{name}(词元 `{token}` 命中 `{banned}`)");
                    }
                }
            }

            Assert.IsEmpty(hits,
                "AC-20-25 的换名词表命中 —— 这些命名把「回执 / 拒绝」做成了流的成员:\n  " +
                string.Join("\n  ", hits) + "\n(词表见本测试;确属误判请加白名单并写明理由)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 载荷面 —— 三支 Drop 载荷无 ack / correlation 字段
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2025_dropPayloads_haveNoAckCorrelationField()
        {
            Type[] payloads =
            {
                typeof(DropSpawnedPayload),
                typeof(DropClaimedPayload),
                typeof(DropDespawnedPayload),
            };

            string[] bannedTokens = { "ack", "correlation", "requestid", "reply" };

            foreach (Type t in payloads)
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);

                // 正控:字段确实读到了(空类型会让下面的循环空过)。
                Assert.IsNotEmpty(fields, $"{t.Name} 须有公开实例字段 —— 正控");

                foreach (FieldInfo f in fields)
                {
                    string lower = f.Name.ToLowerInvariant();
                    foreach (string token in bannedTokens)
                    {
                        Assert.IsFalse(lower.Contains(token),
                            $"{t.Name}.{f.Name} 含 `{token}` —— 45 是纯管道,不做回执(ADR-001 §一)");
                    }
                }
            }
        }

        [Test]
        public void test_ac2025_dropPayloads_arePureValueTypes()
        {
            // ADR-006:载荷 ⊆ 整数域。这条不是 AC-20-25 本身,而是它**能成立**的边界:
            // 若载荷里冒出引用类型/事件句柄,「回执」就可以藏在里面而躲过字段名扫描。
            foreach (Type t in new[]
                     {
                         typeof(DropSpawnedPayload),
                         typeof(DropClaimedPayload),
                         typeof(DropDespawnedPayload),
                     })
            {
                Assert.IsTrue(t.IsValueType, $"{t.Name} 须是值类型(ADR-006 载荷边界)");

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsFalse(f.FieldType.IsClass || f.FieldType.IsInterface,
                        $"{t.Name}.{f.Name} 是引用类型 —— 违 ADR-006 载荷 ⊆ 整数域");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 接口面 —— 禁向 45 索 ack(AC-20-25 原文点名)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2025_contractAbstractions_haveNoAckChannel()
        {
            // AC-20-25 末句:「禁向 45 索 ack 通道」。
            // 判据 = `Sim.Contracts` 里**没有任何接口方法**带 ack / correlation 语义的名字。
            // ⚠️ 扫的是**接口方法名**,不是字段 —— 通道形状只能体现在方法上。
            //
            // ⚠️ **按 PascalCase 词元切分后判等,不做子串匹配**(2026-10-27 实测教训):
            //    子串法会把 `IRollbackableIdAuthority.TryRollbackLastPatientId` 判成回执方法
            //    —— 它含子串 "ack"(`rollb-ack`),但语义是**发号回滚**,与 45 的 ack 通道无关。
            //    假阳性的代价是逼人加白名单,而白名单一旦存在,真回执方法也能藏进去。
            //    ⇒ 判据提高到词元级:`Ack` 必须是独立词元才算命中。
            Assembly contracts = typeof(IEventSink).Assembly;
            var bannedTokens = new HashSet<string> { "ack", "correlation", "await", "confirm" };

            var hits = new List<string>();
            foreach (Type t in contracts.GetTypes().Where(t => t.IsInterface))
            {
                foreach (MethodInfo m in t.GetMethods())
                {
                    foreach (string token in WordTokens(m.Name))
                    {
                        if (bannedTokens.Contains(token))
                        {
                            hits.Add($"{t.Name}.{m.Name}(词元 `{token}`)");
                            break;
                        }
                    }
                }
            }

            Assert.IsEmpty(hits,
                "`Sim.Contracts` 接口面出现回执语义方法 —— 违 AC-20-25「禁向 45 索 ack」/ ADR-001 §一:\n  " +
                string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac2025_eventSinkIsWriteOnly_noReturnValue()
        {
            // 更强的形状判据:`IEventSink.Append` 返回 **void** —— 管道不能回执。
            // 「写入成功与否」在门 A 的接口面上**不可观测**(重发静默去重,ADR-005)。
            MethodInfo append = typeof(IEventSink).GetMethod("Append");
            Assert.IsNotNull(append, "`IEventSink.Append` 须存在 —— 正控");
            Assert.AreEqual(typeof(void), append.ReturnType,
                "`IEventSink.Append` 须返回 void —— 有返回值 = 可承载回执,违 AC-20-25");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `design/registry/entities.yaml` 的绝对路径 —— 经 `Application.dataPath` 上溯仓库根
        /// (与 `Editor.Tools.Gates.DiseaseConstantRegistryGate.EntitiesYamlPath` 同法)。
        /// </summary>
        private static string EntitiesYamlPath()
        {
            string assetsDir = UnityEngine.Application.dataPath;              // …/unity/Assets
            string unityDir = Path.GetDirectoryName(assetsDir);               // …/unity
            string repoRoot = Path.GetDirectoryName(unityDir);                // 仓库根
            return Path.Combine(repoRoot, "design", "registry", "entities.yaml");
        }

        /// <summary>
        /// 词元 × 违禁词的匹配 —— 违禁词是词元的**前缀**(`reject` 命中 `rejected` /
        /// `rejection`),而**不**做任意子串匹配。
        /// <para>前缀匹配同时满足两头:① 抓到屈折形式(`PickupRejected` / `ConsumeFailed`
        /// 这类本项目的实际命名形态);② 不误伤 `rollback`(含子串 `ack` 但**不以** `ack` 开头)
        /// —— 2026-10-27 实测的假阳性正是这一条。</para>
        /// </summary>
        private static bool MatchesToken(string token, string banned)
            => token.StartsWith(banned, StringComparison.Ordinal);

        /// <summary>
        /// 把 PascalCase / camelCase 标识符切成小写词元(`TryRollbackLastPatientId`
        /// → `try` / `rollback` / `last` / `patient` / `id`)。
        /// <para>用于「词元级」命名判据 —— 子串匹配会把 `rollback` 里的 `ack`
        /// 误判成回执词元(2026-10-27 实测)。</para>
        /// </summary>
        private static IEnumerable<string> WordTokens(string identifier)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (!char.IsLetterOrDigit(c))
                {
                    if (sb.Length > 0) { yield return sb.ToString().ToLowerInvariant(); sb.Clear(); }
                    continue;
                }
                // 大写字母 = 词元边界(前一个字符非大写时切)。
                if (char.IsUpper(c) && sb.Length > 0 && !char.IsUpper(identifier[i - 1]))
                {
                    yield return sb.ToString().ToLowerInvariant();
                    sb.Clear();
                }
                sb.Append(c);
            }
            if (sb.Length > 0) yield return sb.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// 取 registry 里某条目的文本块 —— 从 `- name: <kind>` 起到下一个 `- name:` 之前。
        /// <para>⚠️ 刻意只做**块定位**,不做 YAML 解析:本件只问「块里有没有 `stream: world`」,
        /// 引入一个 YAML 解析器会多一条与生产不同的解析路径(判据不许自建第二真源)。</para>
        /// </summary>
        private static string BlockOf(string text, string kindName)
        {
            string marker = "- name: " + kindName;
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            int next = text.IndexOf("- name:", start + marker.Length, StringComparison.Ordinal);
            return next < 0 ? text.Substring(start) : text.Substring(start, next - start);
        }
    }
}
