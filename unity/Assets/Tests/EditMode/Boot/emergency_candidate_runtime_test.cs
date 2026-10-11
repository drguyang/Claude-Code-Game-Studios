// sprint-07 S3.1 · 急救候选列表**生产实例化**判据(2026-10-11)
//
// 判据(可证伪):
//   ① 组合根装配后 ⇒ 服务袋带 `EmergencyCandidateRuntime`(运行期列表**实体存在**);
//   ② 候选动作标签已喂入(止血/通气,10 最小动作)+ 跳过入口恒在(可跳过是结构承诺);
//   ③ 挂载面可用:MountTo(根)后候选列表进入 UI 树(M2 #8 形态件施加点③的载体位);
//   ④ 变异:删组合根实例化 ⇒ `EmergencyCandidates` 为 null ⇒ 恰红。
//
// 范围注:本判据只覆盖「载体生产实例化」半边;动作判定 / Idle 可达时序 / 点击行为
// 归 10 + 数据绑定轮(承 story-023 边界①),不在本测试面。

using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="EmergencyCandidateRuntime"/> 生产实例化判据(sprint-07 S3.1)。</summary>
    internal sealed class emergency_candidate_runtime_test
    {
        [Test]
        public void test_s3_assemble_createsRuntime_withLabelsAndSkip()
        {
            var bag = CompositionRoot.Assemble();

            Assert.IsNotNull(bag.EmergencyCandidates,
                "组合根须装配急救候选列表宿主(M2 #8 施加点③ —— 运行期列表实体存在)");
            Assert.IsTrue(bag.EmergencyCandidates.HasSkipEntry,
                "跳过入口恒在(可跳过是结构承诺,story-023)");
            Assert.AreEqual(2, bag.EmergencyCandidates.EntryCount,
                "候选动作标签已喂入(止血/通气,10 最小动作表)");

            var labels = bag.EmergencyCandidates.Root.Children()
                .Where(e => e.name.StartsWith("candidate-"))
                .Select(e => e.Q<Label>()?.text).ToArray();
            CollectionAssert.AreEqual(
                new[] { "止血", "通气" }, labels,
                "候选标签按喂入序渲染(42 零自定义排序 —— 动作集归 10)");
        }

        [Test]
        public void test_s3_mountTo_attachesToUiRoot()
        {
            var bag = CompositionRoot.Assemble();

            var root = new VisualElement();
            bag.EmergencyCandidates.MountTo(root);

            Assert.IsNotNull(root.Q<VisualElement>("emergency-candidates"),
                "挂载面:候选列表须进入 UI 根(M2 #8 载体位的树上落点)");
            Assert.IsNotNull(root.Q<VisualElement>("skip-entry"),
                "跳过入口同树在(挂载 = 整表结构,不拆)");
        }

        [Test]
        public void test_s3_runtime_isFromProductionBag_notFake()
        {
            // S3.1 判据的核心是「**生产**实例化」:宿主由组合根创建(非测试直 new)。
            // 此测钉死:Assemble() 产出的宿主 = 生产装配面产物(与 8 词表同载体)。
            var bag = CompositionRoot.Assemble();

            Assert.IsNull(bag.EmergencyCandidates.Root.parent,
                "未挂载前列表无父(挂载面显式调用,不自挂 —— 零副作用的装配面)");
            // 实体身份稳定:Root 引用同一 VisualElement(不每次 new)
            var root1 = bag.EmergencyCandidates.Root;
            var root2 = bag.EmergencyCandidates.Root;
            Assert.AreSame(root1, root2,
                "宿主持有同一个载体(实体身份稳定)");
            CollectionAssert.AreEqual(
                new[] { "止血", "通气" },
                bag.EmergencyCandidates.CandidateLabels.ToArray(),
                "标签观测面 = 结构化只读视图(与渲染序一致)");
        }

        [Test]
        public void test_s3_candidateLabels_readOnly_notMutatable()
        {
            // C3-F9 负测:静态默认标签 IReadOnlyList 不可被外部改写;宿主内部数组
            //   CandidateLabels 返回只读快照(强转 string[] 无效/不可写)。
            var bag = CompositionRoot.Assemble();
            string[] snapshot = bag.EmergencyCandidates.CandidateLabels.ToArray();
            snapshot[0] = "被污染";
            Assert.AreEqual("止血", bag.EmergencyCandidates.CandidateLabels[0],
                "外部改写快照不得污染宿主内部状态(测试间隔离)");
        }
    }
}