// AC-7a-16 —— authority-agnostic:同步点与形状正交。
//
// 权威来源:
//   GDD `persistence-service.md:575-579` —— AC-7a-16 全文
//   GDD `:191-193` 规则十二(「现在免费,P1b 重构是三个月级灾难」)
//   systems-index §8 C6
//   ADR-010 §七(存档格式与 authority 无关)
//
// ─────────────────────────────────────────────────────────────────────────────
// 这条 AC 要守的**不是**「两种布局下输出一致」这一个观察,而是它的**成因**
// ─────────────────────────────────────────────────────────────────────────────
// 若只断言「两种布局跑出来一样」,那在「Compose 根本不认识 authority」的实现下
// 是**恒真**的 —— 恒真的断言对变异零判别力(本项目的假绿形态之一)。
// 真正会被将来某次改动打破的,是下面四条**互相独立**的性质:
//
//   A. 组版函数的参数集**逐字**不含任何 authority 类型
//      ⇒ 打破形态:有人给 `Compose` 加 `IIdAuthority` 形参(「顺便把权威传进来」)
//   B. 组版路径上**没有可变静态态**
//      ⇒ 打破形态:有人加一个 static 缓存 / static 计数器,让组版读到进程级状态
//   C. 两种 authority 布局在场时,产出字节**逐位一致**
//      ⇒ 打破形态:组版读取了 ambient authority(见 B 的静态态)
//   D. authority **可注入**(可存在两个互不干扰的实例,非全局单例)
//      ⇒ 打破形态:有人把 `IdAuthority` 的计数器改成 `static`
//
// A / B / D 是**结构判据**,它们直接对应上面的打破形态;C 是行为对照。
// 四条各自可被一次变异单独打红 —— 这是本文件按此拆分的原因。
//
// 写盘边界:本文件零写盘(纯内存组版)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveAuthorityOrthogonalityTest
    {
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
        }

        /// <summary>恒假谓词 —— 本文件只关心 authority 正交,不关心折叠。</summary>
        private sealed class NeverFoldedPredicate : IFoldedPredicate
        {
            public bool Folded(int patientId) => false;
        }

        /// <summary>三流各一条最小事件(载荷用 default —— 本文件不解析载荷内容)。</summary>
        private static IReadOnlyList<SimEvent> ThreeStreams() => new[]
        {
            new SimEvent(10L, PatientId.None, 0L, EventKind.StructurePlaced, default),  // 世界流
            new SimEvent(11L, new PatientId(7), 0L, EventKind.CaseOpened, default),     // 病例流
            new SimEvent(12L, new PatientId(7), 0L, EventKind.CareApplied, default),    // 病史流
        };

        private byte[] Compose(IReadOnlyList<SimEvent> events)
            => SaveComposer.Compose(events, new NeverFoldedPredicate(), _decoder,
                                    new SaveCodec(), 0x5EEDUL, 3U, 12L);

        // ══════════════════════════════════════════════════════════════════
        // A. 组版函数的参数集逐字不含 authority 类型
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_signature_containsNoAuthorityParameter()
        {
            // 判据用**具名类型清单**(字面量),不用 IsAssignableFrom —— 后者对
            // 「新加一个从未见过的 authority 接口」无判别力。
            string[] forbidden =
            {
                "IIdAuthority", "IRollbackableIdAuthority",
                "IEventAuthority", "IAuthority", "IdAuthority",
            };

            var offenders = typeof(SaveComposer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Compose")
                .SelectMany(m => m.GetParameters())
                .Where(p => forbidden.Contains(p.ParameterType.Name))
                .Select(p => $"{p.ParameterType.Name} {p.Name}")
                .ToArray();

            Assert.IsEmpty(offenders,
                "组版函数的参数集不得出现 authority 类型 —— AC-7a-16 要求「同步点抽象与 authority 正交」。" +
                "authority 一旦成为组版的输入,字节形状就会随「谁是权威」而变,P1b 联机即须重排版面。" +
                "违规形参:\n" + string.Join("\n", offenders));
        }

        // ══════════════════════════════════════════════════════════════════
        // B. 组版路径上没有可变静态态
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveComposer_hasNoMutableStaticState()
        {
            // const / static readonly 是**不可变**的,允许(它们不承载「当前谁是权威」)。
            // 只抓 `static` 非 readonly 的非字面量字段 —— 那是唯一的「进程级可变槽」。
            var mutable = typeof(SaveComposer)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name)
                .ToArray();

            Assert.IsEmpty(mutable,
                "SaveComposer 不得有可变静态字段 —— 它是 authority-agnostic 的物理前提:" +
                "组版若读进程级状态,「同样输入 ⇒ 同样字节」就不再成立," +
                "而 AC-7a-16 的两种布局对照会退化为恒真断言。违规字段:\n" +
                string.Join("\n", mutable));
        }

        // ══════════════════════════════════════════════════════════════════
        // C. 两种 authority 布局在场时,字节逐位一致
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_compose_twoAuthorityLayouts_produceIdenticalBytes()
        {
            var events = ThreeStreams();

            // 布局①:单机权威 —— 计数器从 0 起,不参与组版。
            var singlePlayer = new IdAuthority(nextItem: 0L, nextPatient: 0);
            byte[] solo = Compose(events);

            // 布局②:模拟的主机权威 —— 计数器已被推进到高位(联机迁移后高水位重构的形态),
            // 且具回滚能力(真实主机才实现 IRollbackableIdAuthority)。
            var host = new IdAuthority(nextItem: 1000L, nextPatient: 500);
            host.NextPatientId();
            host.NextItemInstanceId();
            Assert.IsTrue(host is IRollbackableIdAuthority, "主机布局须具回滚能力 —— 与单机布局**不同型**");

            byte[] hosted = Compose(events);

            // 两个布局对象在本进程内都**活着且状态不同** —— 若组版读了 ambient authority,
            // 两次产出必然不同。这是本条能证伪的唯一形态。
            CollectionAssert.AreEqual(solo, hosted,
                "两种 authority 布局下同输入须产出**逐位一致**的字节(AC-7a-16)。" +
                "不等 ⇒ 组版读到了 authority 状态,同步点抽象与形状未正交");

            // 顺带确认两个替身确实是**不同状态**,不是同一个对象被复用
            Assert.AreNotEqual(singlePlayer.NextPatientId().Value, host.NextPatientId().Value,
                "两个替身须各自独立推进 —— 同一静态计数器会让本对照失去意义");
        }

        [Test]
        public void test_compose_repeatedUnderDifferentAmbientAuthority_isStable()
        {
            var events = ThreeStreams();
            byte[] baseline = Compose(events);

            // 反复在不同数量的 authority 实例在场时组版 ⇒ 字节不得漂移
            for (int i = 0; i < 5; i++)
            {
                var noise = new IdAuthority(nextItem: i * 100L, nextPatient: i * 10);
                for (int k = 0; k <= i; k++) noise.NextPatientId();

                CollectionAssert.AreEqual(baseline, Compose(events),
                    $"第 {i} 轮(进程内有 {i + 1} 个 authority 实例在场)产出的字节须与基线相同");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // D. authority 可注入 —— 两个实例互不干扰(非全局单例)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_idAuthority_twoInstances_doNotShareCounter()
        {
            // AC-7a-16 的括注逐字:「authority 布局 = **可注入测试替身,非全局单例**」。
            // 这句的可证伪形态 = 计数器是实例字段而非静态字段。
            var a = new IdAuthority(nextPatient: 0);
            var b = new IdAuthority(nextPatient: 100);

            for (int i = 0; i < 5; i++) a.NextPatientId();

            Assert.AreEqual(5, a.NextPatientId().Value, "a 推进 5 次后须发 5");
            Assert.AreEqual(100, b.NextPatientId().Value,
                "b 的计数器不得受 a 影响 —— 若受影响,authority 是全局单例,AC-7a-16 的「布局可切换」即不成立");
        }

        [Test]
        public void test_idAuthority_countersAreInstanceFields()
        {
            // 结构面:计数器字段必须是**实例**非静态。
            // 与上一条互补 —— 上条是行为,本条直接钉住字段位置(行为测试依赖构造顺序,
            // 字段断言不受构造顺序影响)。
            var staticCounters = typeof(IdAuthority)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name)
                .ToArray();

            Assert.IsEmpty(staticCounters,
                "IdAuthority 不得有可变静态字段(计数器须是实例字段) —— " +
                "否则「可注入替身」退化为「全局单例」,AC-7a-16 括注即被违反。违规字段:\n" +
                string.Join("\n", staticCounters));
        }

        // ══════════════════════════════════════════════════════════════════
        // 反向对照:接口真的可替换(不是为了断言而断言)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_idAuthority_interfaceIsSubstitutable_twoImplsCoexist()
        {
            // 「可注入」的另一半 = 消费方**只认接口**。这里证明两种不同实现可并存且可替换,
            // 这是 P1b 换 authority 时不必改存档形状的类型前提。
            IIdAuthority real = new IdAuthority(nextPatient: 0);
            IIdAuthority stub = new ClampingStub();

            Assert.AreEqual(0, real.NextPatientId().Value);
            Assert.AreEqual(999, stub.NextPatientId().Value,
                "替身实现可返回任意值 ⇒ 消费方不得假设真实实现的内部结构");
        }

        /// <summary>一个形态完全不同的 <see cref="IIdAuthority"/> 实现(高水位恒定)。</summary>
        private sealed class ClampingStub : IIdAuthority
        {
            private PatientId _fixed = new PatientId(999);
            public PatientId NextPatientId() => _fixed;
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(999L);
        }
    }
}
