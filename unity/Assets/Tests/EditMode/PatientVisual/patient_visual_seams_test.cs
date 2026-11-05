// 权威来源:
//   `production/milestones/p0-sweep-2026-10-29.md:308`(「一个单色人形 proxy 灰盒即可,
//     不需要真美术资产」)
//   `TD-prescription-06`(病人占位胶囊品红 rgb(255,0,255) 占屏 13.89%)
//   `3b97a850`(2026-10-20)缺陷 B 修复:URP 下 `Shader.Find("Standard")` 返回 null ⇒ 品红占位
//   ADR-015 §三(`WorldPos` 单一整数格)· ADR-016 §一(锚点纯函数确定性)
//   ADR-013 §9 C3(呈现层只渲染不持状态)
//
// 🔁 **2026-10-30(`TD-prescription-06`「灰盒做好,等真实资产兑换」轮)**:
//   本轮把病人视觉拆成两个接缝(`IPatientVisualFactory` / `IPatientVisualAnchorSource`),
//   并**首次**给「品红」这条判据建机械门。
//
// ⚠️ **为什么此前没有这条门**:`3b97a850` 修了品红,但它的验证只有
//   `PatientVisualWiring 1/1`(验接线闭合),**没验颜色**;
//   全仓 `grep -rn "CreatePlaceholderMaterial\|_BaseColor\|品红" unity/Assets/Tests/`
//   在 2026-10-30 之前**零命中**。⇒ 修复本身是真的,但**没有夹具防它回来**。
//
// 🔁 **2026-11-05(`TD-build-03`)**:占位材质从「运行期 `Shader.Find` 手搓」改为
//   「加载 `Resources/PatientPlaceholderMaterial.mat`」。原先那条源码文本判据
//   (`test_placeholderMaterial_urpLitFirst_andLoudFailOnBothMissing`)守的是**已被推翻的
//   实现形态**,同批改写为 `test_placeholderMaterial_comesFromAsset_notRuntimeShaderFind`
//   —— 新增**行为层**判据(资产能加载 + shader 恰为 URP Lit),它才是防「资产被删」的那条。

using System;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Patients;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.PatientVisual
{
    [TestFixture]
    internal sealed class PatientVisualSeamsTest
    {
        // ══════════════ ① 品红回归门(此前不存在)══════════════

        /// <summary>`TD-prescription-06` 的**部分**回归门:占位视觉的材质与基色正确。
        /// <para><b>判据</b>:走**真实生产路径** —— `CapsulePatientVisualFactory.Create(1)` 出来的
        /// GameObject,其 `Renderer.sharedMaterial` 非 null、shader 非 null、基色恰为中性灰
        /// `(0.7, 0.7, 0.7)` 且**不是**品红 `(1,0,1)`。</para>
        /// <para>✅ <b>2026-11-05(`TD-build-03`)起本条的判别力变强了</b>:材质改为从
        /// `Resources/PatientPlaceholderMaterial.mat` 加载 ⇒ 「资产被删 / 路径改名」会**当场抛**,
        /// 本条转红。此前运行期现找着色器,资产不存在这种形态**物理上无法发生**,
        /// 故那时只覆盖「挂上了 + 色对 + 非品红」。</para>
        /// <para>⚠️ <b>为什么不测内部 helper</b>:`LoadPlaceholderMaterial` 是 `internal`
        /// (测试装配看不到,也没有 `InternalsVisibleTo`)—— 而且**走生产路径更强**:
        /// 它连带证明「材质真的被挂到渲染器上」,而不只是「helper 返回值对」。</para>
        ///
        /// <para>🔴 <b>本条守不住「品红」这件事,必须如实说清</b>(2026-10-30 实测):
        /// 我曾以为「shader == null ⇒ 品红」可在 EditMode 判定,<b>实测证伪</b> ——
        /// 探针(`probe_whatDoesShaderFindReturnInEditMode`,已删)实测输出:</para>
        /// <code>
        /// [PROBE-URP]     Universal Render Pipeline/Lit
        /// [PROBE-STD]     Standard          ← 非 null!
        /// [PROBE-UNLIT]   Unlit/Color
        /// [PROBE-DIFFUSE] Legacy Shaders/Diffuse
        /// </code>
        /// <para>⇒ **编辑器里 `Standard` 永远找得到**(内置 shader 恒在),所以那条探针的结论是:
        /// 「运行期 `Shader.Find` 是否失败」在 EditMode 下**不可判定** —— 失败形态只在打包版出现。
        /// 那正是 `TD-build-03`(打包版 2489 条异常 / 病人画不出来)能活一个月的机制。</para>
        /// <para>⇒ 本条的**真实判别力**覆盖「材质挂上了 + 基色对 + 不是品红」这三项,
        /// 外加(2026-11-05 起)「资产存在且能加载」—— 因为材质现在**来自资产**,
        /// 资产缺失会在 <see cref="CapsulePatientVisualFactory.Create"/> 里当场抛。
        /// 「生产码不得用 `Shader.Find`」那条**负判据**由
        /// <see cref="test_placeholderMaterial_comesFromAsset_notRuntimeShaderFind"/> 守。</para>
        /// </summary>
        [Test]
        public void test_placeholderVisual_materialIsNeutralGrey_notMagentaColor()
        {
            var factory = new CapsulePatientVisualFactory();
            GameObject visual = factory.Create(patientId: 1);
            Assert.That(visual, Is.Not.Null, "占位 factory 必须能建出视觉根");

            try
            {
                var renderer = visual.GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null, "视觉根必须带 Renderer(否则不可见 = 退回「看不到病人」)");

                Material mat = renderer.sharedMaterial;
                Assert.That(mat, Is.Not.Null, "视觉根的渲染器必须挂了材质");

                // Assert ①:shader 非 null —— 在 EditMode 下**恒真**(见上注),保留为形状守卫
                Assert.That(mat.shader, Is.Not.Null,
                    "占位材质的 shader 为 null —— Unity 会走错误着色器(品红 rgb(255,0,255))。" +
                    "⚠️ 该断言在 EditMode 下判别力为零(编辑器内置 Standard 恒在),见本测 doc。");

                // Assert ②:基色确实被写成中性灰(而非留在默认白/默认色)
                bool hasBaseColor = mat.HasProperty("_BaseColor");
                Color actual = hasBaseColor ? mat.GetColor("_BaseColor") : mat.color;
                Assert.That(actual.r, Is.EqualTo(0.7f).Within(1e-4f), "中性灰 R 通道");
                Assert.That(actual.g, Is.EqualTo(0.7f).Within(1e-4f), "中性灰 G 通道");
                Assert.That(actual.b, Is.EqualTo(0.7f).Within(1e-4f), "中性灰 B 通道");
                Assert.That(actual.a, Is.EqualTo(1f).Within(1e-4f), "不透明");

                // Assert ③:**明确排除品红**(255,0,255)—— 即便将来有人改色也要避开这一组
                Assert.That(actual.r == 1f && actual.g == 0f && actual.b == 1f, Is.False,
                    "基色恰为品红 (1,0,1) —— 这是错误着色器的观测色,不得作为占位色");
            }
            finally
            {
                factory.Destroy(visual);   // 顺带验 Destroy 的 null 容忍面(接口契约)
            }
        }

        /// <summary>`TD-build-03` 的回归门:材质必须**来自资产**,不得在运行期 `Shader.Find`。
        /// <para><b>病根</b>:运行期 `Shader.Find` 找的着色器若没有任何资产引用它,player 构建
        /// 会把它当未使用**剥离**;而**编辑器与 EditMode 下 `Shader.Find` 恒成功**(内置 shader 恒在)
        /// ⇒ 该缺陷在开发环境**完全隐身**,只在打包版发作(实测 2489 条异常,病人画不出来)。</para>
        /// <para><b>判据分两层</b>:</para>
        /// <list type="number">
        /// <item><b>行为层(主判据)</b>:`Resources.Load&lt;Material&gt;(\"PatientPlaceholderMaterial\")`
        /// 拿到非 null 材质,且其 shader 名**恰为** `Universal Render Pipeline/Lit`。
        /// 资产被删 / 改名 / 换成 Standard ⇒ 本条当场红。</item>
        /// <item><b>源码层</b>:生产码里 `<c>Resources.Load&lt;Material&gt;</c>` 在场、
        /// `<c>Shader.Find</c>` **不在场**(负判据 —— 加回它就是让本缺陷复活)、
        /// 加载不到时 `<c>throw new System.InvalidOperationException</c>`(不得静默 null)。</item>
        /// </list>
        /// <para>⚠️ 源码层扫的是**去注释后的代码** —— 源文件头注里也写着这些名字,
        /// 直接用 IndexOf 会命中文档而非实现(2026-10-30 实跑踩过:红在注释上)。</para>
        /// </summary>
        [Test]
        public void test_placeholderMaterial_comesFromAsset_notRuntimeShaderFind()
        {
            // ── 行为层:资产真的能加载,且 shader 就是 URP Lit ──
            var asset = Resources.Load<Material>("PatientPlaceholderMaterial");
            Assert.That(asset, Is.Not.Null,
                "Resources/PatientPlaceholderMaterial.mat 加载不到 —— 资产被删 / 移出 Resources / 路径改名。" +
                "这一条在打包版上表现为「病人完全不可见」(TD-build-03)。");
            Assert.That(asset.shader, Is.Not.Null, "资产存在但其 shader 引用断了(着色器包缺失?)");
            Assert.That(asset.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"),
                "资产的 shader 不是 URP Lit —— 换成 Standard 在 URP player 构建下会被剥离," +
                "那正是 TD-build-03 的病根路径");

            // ── 源码层:走资产加载,不走运行期查找 ──
            string code = StripComments(ReadOwnSource());

            Assert.That(code, Does.Contain("Resources.Load<Material>"),
                "占位材质必须经 Resources 加载资产(资产引用 = 构建期的可见依赖,着色器才会进包)");
            Assert.That(code, Does.Contain("\"PatientPlaceholderMaterial\""),
                "资源名必须是 PatientPlaceholderMaterial(与本测行为层同名,否则两处会静默分叉)");
            Assert.That(code, Does.Not.Contain("Shader.Find"),
                "生产码里**不得**再出现 Shader.Find —— 它在编辑器恒成功、只在打包版失败," +
                "会把 TD-build-03 这类缺陷重新变成隐身形态");
            Assert.That(code, Does.Contain("throw new System.InvalidOperationException"),
                "加载不到时必须 throw —— 静默返回 null 会让 Unity 走错误着色器(品红)");
        }

        /// <summary>去掉 `//` 行注释与 `/* */` 块注释(判据须锚在**实现**上,不锚在文档上)。
        /// <para>⚠️ 不做完整词法分析 —— 只处理本类会出现的形态(行注释 + 块注释);
        /// 字符串字面量里的 `//` 不处理(本文件的字符串里不含 `//`)。</para>
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                // 块注释
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int close = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = close < 0 ? src.Length : close + 2;
                    continue;
                }
                // 行注释
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    i = nl < 0 ? src.Length : nl;
                    continue;
                }
                sb.Append(src[i]);
                i++;
            }
            return sb.ToString();
        }

        // ══════════════ ② 两接缝的契约面(兑换前提)══════════════

        /// <summary>**兑换前提**:两个接缝都存在,且占位实现都实现了它们。
        /// <para>这是「灰盒做好,等真实资产兑换」的**结构性判据** —— 若接缝缺失,
        /// 真资产来了就只能改 spawner 本体(回到重构前的三处改动)。</para>
        /// </summary>
        [Test]
        public void test_seams_bothExist_andPlaceholdersImplementThem()
        {
            // 位置接缝住 Sim.Contracts(只用 BCL 类型 —— 真位置实现可由 sim 侧提供)
            Assert.That(typeof(IPatientVisualAnchorSource).Namespace,
                Is.EqualTo("DaYiJingCheng.Sim.Contracts"),
                "位置接缝须住 Sim.Contracts(真位置归 6 世界生态区 + 13 空间侧,它们够得着契约面)");
            Assert.That(typeof(IPatientVisualAnchorSource).IsInterface, Is.True);

            // 视觉接缝住表现层(签名必须出现 GameObject ⇒ 物理上进不了 Sim.Contracts)
            Assert.That(typeof(IPatientVisualFactory).Namespace,
                Is.EqualTo("DaYiJingCheng.Gameplay.Presentation.Patients"),
                "视觉接缝须住表现层(签名含 GameObject,进不了 Sim.Contracts)");
            Assert.That(typeof(IPatientVisualFactory).IsInterface, Is.True);

            // 占位实现确实实现了接缝
            Assert.That(typeof(IPatientVisualAnchorSource).IsAssignableFrom(typeof(RingPatientAnchorSource)),
                Is.True, "RingPatientAnchorSource 必须实现 IPatientVisualAnchorSource");
            Assert.That(typeof(IPatientVisualFactory).IsAssignableFrom(typeof(CapsulePatientVisualFactory)),
                Is.True, "CapsulePatientVisualFactory 必须实现 IPatientVisualFactory");
        }

        /// <summary>`IPatientVisualAnchorSource` 返回的是**整数格**,不是连续坐标
        /// —— 承 ADR-015 §三(空间真值一律整数格;换算归表现层)。
        /// <para>判别力:若有人把它改成返回 `Vector3`,本条红(那会让契约面反向依赖 UnityEngine,
        /// 真位置实现(sim 侧)就再也提供不了它)。</para>
        /// </summary>
        [Test]
        public void test_anchorSource_returnsIntegerCell_notVector3()
        {
            MethodInfo m = typeof(IPatientVisualAnchorSource).GetMethod("TryGetAnchorCell");
            Assert.That(m, Is.Not.Null, "接缝方法名须为 TryGetAnchorCell(具名契约面)");

            ParameterInfo[] ps = m.GetParameters();
            Assert.That(ps.Length, Is.EqualTo(2), "签名 = (int patientId, out WorldPos cell)");
            Assert.That(ps[0].ParameterType, Is.EqualTo(typeof(int)), "第一参数 = 病人 id(int)");
            // ⚠️ `out` 参数在反射里呈现为 `WorldPos&`(ByRef)⇒ 须取 `GetElementType()`
            Assert.That(ps[1].ParameterType.IsByRef, Is.True, "第二参数须是 out(不是按值)");
            Assert.That(ps[1].ParameterType.GetElementType(), Is.EqualTo(typeof(WorldPos)),
                "第二参数 = WorldPos(整数格)—— 不得改成 Vector3(那会破 ADR-015 §三)");

            Assert.That(m.ReturnType, Is.EqualTo(typeof(bool)),
                "返回 bool(有/无锚点),不得返回 WorldPos? —— WorldPos 零值 (0,0,0) 是合法格," +
                "拿它当「无锚点」哨兵会静默把病人摆到原点");
        }

        // ══════════════ ③ 占位锚点的确定性(ADR-016 §一)══════════════

        /// <summary>占位锚点**同 id 恒同格**(纯函数确定性,禁 PRNG —— ADR-016 §一)。
        /// <para>判别力:把 `SplitMix64.Hash(patientId, 0)` 换成 `UnityEngine.Random` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_ringAnchor_sameIdIsStable_acrossCalls()
        {
            var anchors = new RingPatientAnchorSource();

            Assert.That(anchors.TryGetAnchorCell(7, out WorldPos first), Is.True);
            Assert.That(anchors.TryGetAnchorCell(7, out WorldPos second), Is.True);

            Assert.That(second.X, Is.EqualTo(first.X), "同 id 两次调用须同格(X)");
            Assert.That(second.Y, Is.EqualTo(first.Y), "同 id 两次调用须同格(Y)");
            Assert.That(second.Z, Is.EqualTo(first.Z), "同 id 两次调用须同格(Z)");
        }

        /// <summary>占位锚点**不是全挤在一格**(否则交互的「最近病人」判定退化)。
        /// <para>⚠️ <b>这不是「必然不碰撞」的强断言</b> —— 环占位的格位是**有限**的:
        /// 半径 2.5 m ⇒ 周长 ≈ 15.7 m ⇒ 最多约 **15 个整数格**。id 多到一定程度**必撞**,
        /// 这是占位算法本身的上限,不是缺陷(真位置(6 + 13 的整数格)没有这个上限)。</para>
        /// <para>本测只钉**诚实下限**:前 4 个 id 至少产生 **≥ 2 个不同格** ——
        /// 保证「最近病人」在最小可用规模下仍有区分度。</para>
        /// </summary>
        [Test]
        public void test_ringAnchor_smallIdSet_isNotAllOneCell()
        {
            var anchors = new RingPatientAnchorSource();
            var distinct = new System.Collections.Generic.HashSet<(int, int, int)>();

            for (int id = 1; id <= 4; id++)
            {
                Assert.That(anchors.TryGetAnchorCell(id, out WorldPos c), Is.True);
                distinct.Add((c.X, c.Y, c.Z));
            }

            Assert.That(distinct.Count, Is.GreaterThanOrEqualTo(2),
                "前 4 个 id 全落在同一格 —— 「最近病人」判定会退化(环占位失效)。" +
                "⚠️ 环占位格位上限约 15(周长 15.7 m),故不主张「永不碰撞」");
        }

        /// <summary>`Destroy` 须**幂等 + null 容忍**(接口契约):teardown 在异常路径上可能重复调用,
        /// 抛异常会把「清理」变成「二次故障」。
        /// <para>判别力:把 `Destroy` 改成 `Object.Destroy(visual)` 无 null 检查 ⇒ Unity 的
        /// `Destroy(null)` 本身是 no-op(不抛),故本条**不会**因它变红 —— 如实登记:
        /// 本条守的是「**未来某个实现**在 null 上抛异常」(如 `throw ArgumentNullException`)的形态。</para>
        /// </summary>
        [Test]
        public void test_factory_destroy_isIdempotentAndNullTolerant()
        {
            var factory = new CapsulePatientVisualFactory();

            // ① null ⇒ no-op(不得抛)
            Assert.DoesNotThrow(() => factory.Destroy(null), "Destroy(null) 必须 no-op");

            // ② 已销毁对象再传 ⇒ no-op(不得抛)
            GameObject visual = factory.Create(patientId: 2);
            Assert.DoesNotThrow(() => factory.Destroy(visual), "首次 Destroy 不得抛");
            Assert.DoesNotThrow(() => factory.Destroy(visual), "重复 Destroy 不得抛(幂等)");
        }

        // ══════════════ 辅助 ══════════════

        /// <summary>读本测试自身的源码(`CallerFilePath` 定位)—— 用于「分支存在性」断言。</summary>
        private static string ReadOwnSource([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            // 本文件在 unity/Assets/Tests/EditMode/PatientVisual/
            // ⇒ PatientVisual → EditMode → Tests → Assets → unity(上溯 4 层)
            string root = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile) ?? ".",
                    "..", "..", "..", ".."));
            string target = System.IO.Path.Combine(
                root, "Assets", "Gameplay.Presentation", "Patients", "CapsulePatientVisualFactory.cs");
            Assert.That(System.IO.File.Exists(target), Is.True,
                $"占位 factory 源文件不存在 —— {target}(路径变了须同批订正本测)");
            return System.IO.File.ReadAllText(target);
        }
    }
}
