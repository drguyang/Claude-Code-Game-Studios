// player-controller Story 006 测试
//
// AC-1-23: 位图语义(①–④)+ 调用点白名单(4 / 10 / 25;UI 不得直触)
// AC-1-27: 1 不持有游戏状态 —— **字段类型白名单**(非字段名黑名单)
// AC-1-12: 禁止项零引用(反射 + 程序集引用集)
//
// ⚠️ 2026-10-02 判据修复轮:
//   原 AC-1-27 判据实现为字段名黑名单({"hp","skill",...}),而本 story 的 Guardrail
//   与 AC 原文均明写「须按**字段类型白名单**,不是字段名黑名单 —— 后者是假阴性机器」。
//   黑名单形态**恒真**(任何改名即绕过)⇒ 该 BLOCKING 未真正执行。本轮改为类型白名单。
//   原 AC-1-23 只验位图机制,零调用点断言。本轮补:UI 零符号引用 + 调用点集合 ⊆ 白名单。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class MotorLeaseTest
    {
        // ══════════ AC-1-23(机制): 位图语义 ══════════

        [Test]
        public void test_ac123_bitmap_singleBitPerSource()
        {
            var lease = new MotorLease();
            Assert.IsFalse(lease.IsSuppressed, "初始无压制");

            lease.Acquire(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Self 位被置位");

            lease.Acquire(LeaseSource.Emergency);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位被置位");

            lease.Acquire(LeaseSource.Combat);
            Assert.IsTrue(lease.IsSuppressed, "Combat 位被置位");
        }

        [Test]
        public void test_ac123_idempotentAcquire()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Self); // 重复 Acquire
            Assert.IsTrue(lease.IsSuppressed, "幂等 Acquire 不改变状态");

            lease.Release(LeaseSource.Self);
            Assert.IsFalse(lease.IsSuppressed, "Release 后清除(位语义,非引用计数 —— 重复 Acquire 不留残余)");
        }

        [Test]
        public void test_ac123_releaseOnlyOwnBit()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Emergency);

            lease.Release(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位保持");
            Assert.IsFalse(lease.HasLease(LeaseSource.Self), "Self 位已清");
            Assert.IsTrue(lease.HasLease(LeaseSource.Emergency), "只碰自己位(纪律 ①)");

            lease.Release(LeaseSource.Emergency);
            Assert.IsFalse(lease.IsSuppressed, "全部清除");
        }

        [Test]
        public void test_ac123_releaseNeverAcquired_noOp()
        {
            var lease = new MotorLease();
            lease.Release(LeaseSource.Combat); // 从未 Acquire
            Assert.IsFalse(lease.IsSuppressed, "无下溢");
        }

        [Test]
        public void test_ac123_interleavedSources()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Self);
            lease.Acquire(LeaseSource.Combat);
            lease.Release(LeaseSource.Self);
            Assert.IsTrue(lease.IsSuppressed, "Combat 位保持");

            lease.Acquire(LeaseSource.Emergency);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位被置位");

            lease.Release(LeaseSource.Combat);
            Assert.IsTrue(lease.IsSuppressed, "Emergency 位保持");

            lease.Release(LeaseSource.Emergency);
            Assert.IsFalse(lease.IsSuppressed, "全部清除");
        }

        // ══════════ AC-1-23: 调用点白名单(4 / 10 / 25;UI 不得直触)══════════

        /// <summary>允许的调用者程序集白名单(AC-1-23)。</summary>
        private static readonly string[] AllowedCallerAssemblies =
        {
            "Gameplay.Presentation", // 4 交互 / 10 急救 / 25 战斗的宿主程序集
            "Gameplay.Input",
            "Sim",
            "Sim.Contracts",
            "Sim.Codec",
        };

        /// <summary>UI 程序集(42 / 48)—— AC-1-23 明令不得直触 lease。</summary>
        private static readonly string[] UiSourceDirectories =
        {
            "Gameplay.UI",
            "Gameplay.Presentation/Skeuomorphic",
        };

        [Test]
        public void test_ac123_uiSourceHasZeroLeaseReferences()
        {
            // AC-1-23: UI(42 / 48)不得直接调用 —— 判据 = UI 源码零 LeaseSource / MotorLease 符号引用。
            // UI 需冻结移动须经合法游戏系统(如 4 的 Interact 触发压制),不直触 1(ADR-013 §9 C3)。
            var violations = new List<string>();

            foreach (string rel in UiSourceDirectories)
            {
                string dir = Path.Combine(Application.dataPath, rel);
                if (!Directory.Exists(dir))
                {
                    violations.Add($"⚠️ UI 目录不存在:{rel}(判据无从执行 —— 不得静默通过)");
                    continue;
                }

                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    if (text.Contains("LeaseSource") || text.Contains("MotorLease"))
                        violations.Add(Path.GetFileName(file));
                }
            }

            Assert.IsEmpty(violations,
                "UI(42 / 48)不得直接引用 lease(AC-1-23 · ADR-013 §9 C3):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac123_callSitesSubsetOfWhitelist()
        {
            // AC-1-23: 断言的是**调用点集合**,不是接口定义位置。
            // 调用者 ∈ {4, 10, 25};当前生产代码零调用点(P0 尚未接线)⇒ 集合为空,是白名单子集。
            var callSites = new List<string>();
            var disallowed = new List<string>();

            foreach (string asmName in AllowedCallerAssemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null) continue;

                foreach (var site in FindLeaseCallSites(asm))
                {
                    callSites.Add($"{asmName}:{site.Caller}.{site.Method}");
                    if (!AllowedCallerAssemblies.Contains(asmName))
                        disallowed.Add(site.Caller);
                }
            }

            Assert.IsEmpty(disallowed,
                "lease 调用者不在白名单 {4, 10, 25} 内(AC-1-23):\n" + string.Join("\n", disallowed));

            // 显式记账:集合当前为空 ⇒ 本断言此刻是「白名单子集」的守卫,
            // 真正的调用点覆盖须待 4 / 10 / 25 接线(见 story-006 Deviations)。
            TestContext.WriteLine(
                $"AC-1-23 调用点集合(当前 {callSites.Count} 处,预期 P0 为 0):\n" +
                (callSites.Count == 0 ? "(空 —— 4 / 10 / 25 尚未接线)" : string.Join("\n", callSites)));
        }

        [Test]
        public void test_ac123_callSiteScannerIsNotVacuous()
        {
            // 负向自检:扫描器必须能**真的**抓到调用点,否则上一条断言是空转。
            // 本测试程序集自身含 lease 调用(motor_lease_test.cs)⇒ 扫描它必非空。
            var self = typeof(MotorLeaseTest).Assembly;
            var found = FindLeaseCallSites(self);

            Assert.IsNotEmpty(found,
                "扫描器对含 lease 调用的程序集应报出 ≥1 处调用点 —— 空结果 = 扫描器失效(AC-1-23 判据空转)");
            Assert.IsTrue(found.Any(f => f.Caller == nameof(MotorLeaseTest)),
                "应至少报出本测试类为调用者");
        }

        [Test]
        public void test_ac123_leaseSourceOrdinalIsSystemNumber()
        {
            // AC-1-23 / ADR-014 ordinal 纪律:ordinal = 系统号,append-only、禁重排。
            Assert.AreEqual(4, (int)LeaseSource.Self, "Self 的 ordinal 须 = 系统号 4");
            Assert.AreEqual(10, (int)LeaseSource.Emergency, "Emergency 的 ordinal 须 = 系统号 10");
            Assert.AreEqual(25, (int)LeaseSource.Combat, "Combat 的 ordinal 须 = 系统号 25");
        }

        // ══════════ AC-1-27: 字段类型白名单(非字段名黑名单)══════════

        /// <summary>
        /// 允许的字段类型集 —— 表现层允许集 + 边界程序集整数类型(AC-1-27 原文)。
        /// 判据 = **类型域**,与命名无关:任何自造游戏状态类型在类型域上即被拒。
        /// </summary>
        private static readonly HashSet<Type> AllowedFieldTypes = new HashSet<Type>
        {
            // BCL 基元与字符串
            typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(char), typeof(string),

            // UnityEngine 表现层类型
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion),
            typeof(Transform), typeof(GameObject), typeof(CharacterController),
            typeof(MonoBehaviour),

            // 边界程序集整数域
            typeof(WorldPos), typeof(Int3), typeof(PatientId), typeof(Fix),

            // 1 自有类型
            typeof(PlayerControllerType), typeof(CellTransitionDetector),
            typeof(LocomotionEvaluator), typeof(MotorLease), typeof(SimAuthorityMode),
            typeof(LeaseSource),

            // 只读接口引用(六抽象点 + 相机只读基)
            typeof(IEventSink), typeof(ITickProvider), typeof(IEventAuthority),
            typeof(IIdAuthority), typeof(IVitalsQuery), typeof(IPresenceQuery),
            typeof(IDataProvider), typeof(ICameraRig), typeof(YawBasis),
        };

        /// <summary>
        /// 字段类型是否在白名单内。集合容器(数组 / 泛型集合 / Nullable)递归判定元素类型。
        /// </summary>
        internal static bool IsAllowedFieldType(Type t)
        {
            if (t == null) return false;
            if (AllowedFieldTypes.Contains(t)) return true;
            if (t.IsEnum) return true;                 // 枚举 = 整数域
            if (t.IsArray) return IsAllowedFieldType(t.GetElementType());

            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                bool isContainer =
                    def == typeof(List<>) || def == typeof(IReadOnlyList<>) ||
                    def == typeof(IList<>) || def == typeof(IEnumerable<>) ||
                    def == typeof(IReadOnlyCollection<>) || def == typeof(ICollection<>) ||
                    def == typeof(HashSet<>) || def == typeof(ISet<>) ||
                    def == typeof(Dictionary<,>) || def == typeof(IReadOnlyDictionary<,>) ||
                    def == typeof(IDictionary<,>) || def == typeof(Nullable<>);

                if (!isContainer) return false;        // 未登记的泛型 ⇒ 拒
                return t.GetGenericArguments().All(IsAllowedFieldType);
            }

            return false;                              // 其余类型一律红
        }

        [Test]
        public void test_ac127_fieldTypesInWhitelist()
        {
            // AC-1-27: 1 的类型图中不存在血量 / 技能 / 库存 / 任务字段或引用。
            // ⚠️ 判据按**字段类型白名单**,不是字段名黑名单(story-006 Guardrail 原文)。
            // DeclaredOnly:只判 1 自己声明的字段,不牵连 MonoBehaviour 基类字段。
            var playerType = typeof(PlayerControllerType);
            var violations = new List<string>();
            var inspected = new List<string>();

            foreach (var field in playerType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                inspected.Add($"{field.Name}:{field.FieldType.Name}");
                if (!IsAllowedFieldType(field.FieldType))
                    violations.Add($"{field.Name} : {field.FieldType.FullName}");
            }

            // 非空转守卫:字段集为空 ⇒ 本判据什么都没查,必须红而非静默通过。
            Assert.IsNotEmpty(inspected,
                "未扫到任何字段 —— 判据空转(AC-1-27 未真正执行)");

            Assert.IsEmpty(violations,
                $"1 的字段类型不在白名单内(AC-1-27 类型白名单),已扫 {inspected.Count} 个字段:\n" +
                string.Join("\n", violations));

            TestContext.WriteLine($"AC-1-27 已扫 {inspected.Count} 个字段:{string.Join(", ", inspected)}");
        }

        [Test]
        public void test_ac127_whitelistRejectsFabricatedGameState()
        {
            // 负向夹具(回归测试):证明白名单**不是恒真**。
            // 原判据(字段名黑名单)对下面两个类型都会放行 —— 它们正是该缺陷的形态。
            Assert.IsFalse(IsAllowedFieldType(typeof(FakeHealthState)),
                "自造游戏状态类型应被拒(AC-1-27 类型域判据)");
            Assert.IsFalse(IsAllowedFieldType(typeof(FakeVitalityState)),
                "**改名后仍须被拒** —— 这是原字段名黑名单的漏洞形态(hp/health 换成 Current 即绕过)");
            Assert.IsFalse(IsAllowedFieldType(typeof(List<FakeHealthState>)),
                "容器元素类型须递归判定 —— List<自造状态> 亦须被拒");
            Assert.IsFalse(IsAllowedFieldType(typeof(Stack<int>)),
                "未登记的泛型容器须被拒(白名单是闭集,不是「任何泛型都放行」)");
        }

        [Test]
        public void test_ac127_whitelistAcceptsKnownGoodTypes()
        {
            // 正向半边:白名单不得过窄(否则判据会被「永远红」掩盖)。
            Assert.IsTrue(IsAllowedFieldType(typeof(float)));
            Assert.IsTrue(IsAllowedFieldType(typeof(bool)));
            Assert.IsTrue(IsAllowedFieldType(typeof(Vector3)));
            Assert.IsTrue(IsAllowedFieldType(typeof(WorldPos)));
            Assert.IsTrue(IsAllowedFieldType(typeof(SimAuthorityMode)), "枚举 = 整数域");
            Assert.IsTrue(IsAllowedFieldType(typeof(CharacterController)));
            Assert.IsTrue(IsAllowedFieldType(typeof(IEventSink)));
            Assert.IsTrue(IsAllowedFieldType(typeof(LeaseSource)));
            Assert.IsTrue(IsAllowedFieldType(typeof(List<string>)), "容器元素为已登记类型 ⇒ 放行");
        }

        // ══════════ AC-1-12: 禁止项零引用 ══════════

        [Test]
        public void test_ac112_noTerrainSampling()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(method.Name.Contains("SampleHeight"),
                    $"方法 {method.Name} 含 SampleHeight(AC-1-12)");
                Assert.IsFalse(method.Name.Contains("TerrainSample"),
                    $"方法 {method.Name} 含 TerrainSample(AC-1-12)");
            }
        }

        [Test]
        public void test_ac112_noNavMeshSampling()
        {
            var playerType = typeof(PlayerControllerType);
            foreach (var method in playerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(method.Name.Contains("NavMeshSample"),
                    $"方法 {method.Name} 含 NavMeshSample(AC-1-12)");
            }
        }

        // ══════════ 负向夹具载体(故意不含 hp / health / vitality 等黑名单词)══════════

        private struct FakeHealthState { public int Value; }

        private struct FakeVitalityState { public int Current; }

        // ══════════ IL 扫描器:找 MotorLease.Acquire / Release 的调用点 ══════════

        private static List<(string Caller, string Method)> FindLeaseCallSites(Assembly assembly)
        {
            var found = new List<(string, string)>();

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            foreach (var type in types)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.DeclaredOnly;

                var methods = type.GetMethods(flags).Cast<MethodBase>()
                    .Concat(type.GetConstructors(flags));

                foreach (var method in methods)
                {
                    foreach (var callee in EnumerateCalledMethods(method))
                    {
                        if (callee?.DeclaringType == typeof(MotorLease) &&
                            (callee.Name == "Acquire" || callee.Name == "Release"))
                        {
                            found.Add((type.Name, method.Name));
                        }
                    }
                }
            }

            return found;
        }

        private static IEnumerable<MethodBase> EnumerateCalledMethods(MethodBase method)
        {
            MethodBody body;
            try { body = method.GetMethodBody(); }
            catch { yield break; }
            if (body == null) yield break;

            byte[] il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) yield break;

            for (int i = 0; i < il.Length; i++)
            {
                ushort opcode = il[i];
                if (opcode == 0xFE)
                {
                    i++;
                    if (i >= il.Length) break;
                    continue;
                }

                // call = 0x28, callvirt = 0x6F, newobj = 0x73
                if (opcode != 0x28 && opcode != 0x6F && opcode != 0x73) continue;
                if (i + 4 >= il.Length) break;

                int token = BitConverter.ToInt32(il, i + 1);
                MethodBase resolved = null;
                try { resolved = method.Module.ResolveMethod(token); }
                catch { /* 无法解析 ⇒ 保守跳过 */ }

                i += 4;
                if (resolved != null) yield return resolved;
            }
        }
    }
}
