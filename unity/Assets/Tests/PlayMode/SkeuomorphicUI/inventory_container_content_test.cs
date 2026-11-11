// 20 容器屏的**内容面**判据(2026-11-02 · `S23-25`)
//
// ▍为什么要有这个文件
//   该屏此前是**空壳**:只建了两个纸面容器,零内容、零交互、零 DTO 源
//   (`grep -c "Label\|focusable\|Bind" InventoryContainerScreen.cs` ⇒ 0)。
//   本轮补内容面(物品格 / 翻页 / 选中 / 载重四档),使「料位选中态」有挂载点 ——
//   那是炮制屏品级集的来源。
//
// ▍判据(逐条可证伪)
//   ① **落位序**:格按 `instance_id` 升序、每页 ≤12(§5.2 的 `COLUMNS=4` / `ROWS_PER_PAGE=3`)
//   ② **翻页**:第 2 页起序号对、首/末页铜扣**卸为平整铜片**(非置灰非隐藏 —— §5.2 ④)
//   ③ **选中**:点格 ⇒ 选中集变、出口收到 id、**格上 `selected` 类跟着变**;**再点 ⇒ 取消**
//   ④ **四档**:满档 = **20 的谓词结果**(不在本屏重算);余下三档单调不增
//   ⑤ **空箱**:零格 + `FirstFocusable == null` + 1 页(不假装有内容)
//
// ▍本件的边界(先声明,防借绿)
//   · **不测视觉** —— `-nographics` 下无像素。满溢感(AC-20-10)/ 色盲(AC-20-23)/
//     听测(AC-20-11)三条 `[L]` **仍 NOT-RUN**,本件只覆盖结构半边。
//   · **不测手柄焦点导航** —— 那要真 `EventSystem` + 虚拟 Gamepad。本件测「格可获焦」位。
//   · **不测「箱内 vs 身上」的区分** —— 投影今日没有「在哪个容器里」这一维(登记为残余)。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>20 容器屏的内容面判据。</summary>
    internal sealed class inventory_container_content_test
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private InventoryContainerScreen _screen;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null; _screen = null;
        }

        private IEnumerator SetUp()
        {
            _go = new GameObject("InventoryContainer_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f)
                {
                    Assert.Fail("[夹具红] rootVisualElement / panel 3s 未就绪");
                    yield break;
                }
                yield return null;
            }
            yield return null;

            _screen = new InventoryContainerScreen(_doc.rootVisualElement, new SkeuoElementLibrary());
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 落位序 + 每页 ≤12
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac2020_gridOrder_matchesInstanceIdAscending_andPageCap()
        {
            yield return SetUp();

            // 15 件(> 12)⇒ 第 1 页 12 格,第 2 页 3 格
            var items = new ContainerItemDto[15];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ContainerItemDto(instanceId: 100L + i, itemOrdinal: i, quality: 1, qty: 1);

            _screen.Bind(new InventoryContainerDto(containerInstanceId: 42L, items, 0L, 0, false));
            yield return null;

            Assert.That(_screen.PageCount, Is.EqualTo(2),
                "① 15 件 ⇒ 2 页(12 + 3)");
            Assert.That(_screen.CellCount, Is.EqualTo(12),
                "① 第 1 页须恰 12 格(`COLUMNS × ROWS_PER_PAGE = 4 × 3`,§5.2)");

            for (int i = 0; i < 12; i++)
            {
                Assert.That(_screen.InstanceIdAt(i), Is.EqualTo(100L + i),
                    $"① 第 {i} 格须绑 instance_id = {100 + i}(§5.2 的升序落位)");
                Assert.That(_screen.CellAt(i).focusable, Is.True,
                    $"① 第 {i} 格须可获焦(§5.3 器物「是(焦点 + 行级动作)」)");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 翻页 + 铜扣卸形
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac2020_pageTurn_secondPageAndDetentUnload()
        {
            yield return SetUp();

            var items = new ContainerItemDto[15];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ContainerItemDto(100L + i, i, 1, 1);
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            Assert.That(_screen.Page, Is.Zero, "② 前置:开屏在第 1 页");

            _screen.TurnPage(+1);
            yield return null;

            Assert.That(_screen.Page, Is.EqualTo(1), "② 翻到第 2 页");
            Assert.That(_screen.CellCount, Is.EqualTo(3), "② 第 2 页剩 3 件");
            Assert.That(_screen.InstanceIdAt(0), Is.EqualTo(112L),
                "② 第 2 页首格 = 第 13 件(instance_id 112)");

            // §5.2 ④ 末页铜扣**卸为平整铜片**(非置灰非隐藏):`brass-detent-flat` 类
            var next = _doc.rootVisualElement.Q("page-next");
            Assert.That(next, Is.Not.Null, "② 前置:下一页铜扣须存在(不是隐藏)");
            Assert.That(next.ClassListContains("brass-detent-flat"), Is.True,
                "② 末页 ⇒ `page-next` 须卸为平整铜片(§5.2 ④:非置灰、非隐藏)");
            Assert.That(next.enabledSelf, Is.False, "② 卸形后不可交互");

            // 回第 1 页 ⇒ 铜扣恢复
            _screen.TurnPage(-1);
            yield return null;
            Assert.That(next.ClassListContains("brass-detent-flat"), Is.False,
                "② 回第 1 页 ⇒ 下一页铜扣须恢复可交互形");
        }

        /// <summary>
        /// ②b 翻页后**焦点跟到新页**(2026-11-11 T22 批十二 真包走查缺陷)。
        /// <para>真包实测:第 2 页有 2 件器物,按下一页铜扣后焦点掉成「无焦点」⇒
        /// 方向键无处可走。`AC-20-20` 要「稳定且可达」;焦点悬空即不可达。</para>
        /// <para>判别力:删 `TurnPage` 里的推迟落焦 ⇒ 本条红(既有 ② 仍绿 —— 它只查页码/类名)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_ac2020_pageTurn_movesFocusToNewPageFirstCell()
        {
            yield return SetUp();

            var items = new ContainerItemDto[15];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ContainerItemDto(100L + i, i, 1, 1);
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            Assert.That(_screen.CellCount, Is.EqualTo(12), "前置:第 1 页 12 格");
            _screen.FirstFocusable.Focus();
            yield return null;

            _screen.TurnPage(+1);
            yield return null;      // 推迟一帧落焦

            var focused = _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused, Is.Not.Null,
                "翻页后焦点须落在新页 —— 悬空 ⇒ 手柄 / 键盘玩家在药箱里走不动(AC-20-20)");
            Assert.That(focused.name, Is.EqualTo("container-item-112"),
                "焦点须落新页**首格**(第 13 件,instance_id 112)");
        }

        /// <summary>
        /// ②c **沿口 / 半盖必须留在树上**(2026-11-11 T22 批十二 真包走查)。
        /// <para>真包实测:近满 / 满档只有格间隙变化,`box-rim` 与 `box-lid` 从未出现。
        /// 根因 = `RebuildPage` 的清树循环只豁免 `item-grid-paper`,把构造期加进去的
        /// 这两个非格子件一并摘掉 ⇒ `ApplyBandGeometry` 设的 `display:flex` 画在**树外**元素上。</para>
        /// <para>判别力:删掉 `RebuildPage` 对 `box-rim` / `box-lid` 的豁免 ⇒ 本条红
        /// (既有 5′ / 5″ 仍绿 —— 它们只查 `style.display` 属性,查不到「仍在树上」)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_ac2010_bandGeometry_rimAndLidStayAttachedAcrossRebuild()
        {
            yield return SetUp();

            var items = new ContainerItemDto[12];
            for (int i = 0; i < items.Length; i++)
                items[i] = new ContainerItemDto(200L + i, i, 1, 1);

            // 近满:55 / 60,谓词 false ⇒ NearFull
            _screen.Bind(new InventoryContainerDto(42L, items, 55L, 60, false));
            yield return null;

            var rim = _doc.rootVisualElement.Q<VisualElement>("box-rim");
            Assert.That(rim, Is.Not.Null, "沿口元素须存在");
            Assert.That(rim.panel, Is.Not.Null,
                "近满档的沿口必须**真在树上** —— 属性 true 而 panel null 就是画不出来");
            Assert.That(_screen.IsRimVisible, Is.True, "近满档沿口须为可见");

            // 满:60 / 60 + 谓词 true ⇒ Full
            _screen.Bind(new InventoryContainerDto(42L, items, 60L, 60, true));
            yield return null;

            var lid = _doc.rootVisualElement.Q<VisualElement>("box-lid");
            Assert.That(lid, Is.Not.Null, "半盖元素须存在");
            Assert.That(lid.panel, Is.Not.Null,
                "满档的半盖必须**真在树上** —— 属性 true 而 panel null 就是画不出来");
            Assert.That(_screen.IsLidVisible, Is.True, "满档半盖须为可见");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 选中:点 ⇒ 出口收到;再点 ⇒ 取消
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_selection_togglesAndReportsToSink()
        {
            yield return SetUp();

            var items = new[]
            {
                new ContainerItemDto(201L, 1, 3, 1),
                new ContainerItemDto(202L, 2, 1, 1),
            };
            var sink = new SpySink();
            _screen.BindSelectionSink(sink);
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            Assert.That(_screen.SelectedInstanceIds, Is.Empty, "③ 前置:初始无选中");
            Assert.That(_screen.CellAt(0).ClassListContains("selected"), Is.False,
                "③ 前置:未选中的格不带 `selected` 类");

            _screen.ToggleSelection(0);   // 选中 201
            Assert.That(_screen.SelectedInstanceIds, Is.EqualTo(new[] { 201L }),
                "③ 点第 0 格 ⇒ 选中 201");
            Assert.That(sink.LastReported, Is.EqualTo(new[] { 201L }),
                "③ 出口须收到选中集(BL-23 ④「消耗意图直接点名实例集」)");
            // ⚠️ **本行是证伪轮 INV20-M3 补的**:首版只断言选中集与出口,而「格上的
            //    `selected` 类」是**唯一**告诉玩家「这一栈被点了」的东西(§11 品级通道走
            //    质地/药签,选中走类)。只加不删的变异当时**假存活** —— 因为没有任何断言
            //    看类。类是可断言的**结构**面(`-nographics` 下也能读),不属于视觉。
            Assert.That(_screen.CellAt(0).ClassListContains("selected"), Is.True,
                "③ 选中后该格须带 `selected` 类(玩家唯一可见的选中标记)");

            _screen.ToggleSelection(1);   // 再选 202
            Assert.That(_screen.SelectedInstanceIds, Is.EqualTo(new[] { 201L, 202L }),
                "③ 两格都选 ⇒ 选中集按 id 升序(稳定序)");
            Assert.That(_screen.CellAt(1).ClassListContains("selected"), Is.True,
                "③ 第 1 格也须带 `selected` 类");

            _screen.ToggleSelection(0);   // 取消 201
            Assert.That(_screen.SelectedInstanceIds, Is.EqualTo(new[] { 202L }),
                "③ 再点同一格 ⇒ 取消该格(不是重选)");
            Assert.That(sink.LastReported, Is.EqualTo(new[] { 202L }), "③ 出口须同步");
            Assert.That(_screen.CellAt(0).ClassListContains("selected"), Is.False,
                "③ **取消**后该格须摘掉 `selected` 类(只加不删 ⇒ 界面永远显示「已选」)");
            Assert.That(_screen.CellAt(1).ClassListContains("selected"), Is.True,
                "③ 取消第 0 格不得连带清掉第 1 格");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 载重四档:单调不增 + 满档具名谓词
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_carryBand_fullComesFromNamedPredicate_monotonicAbove()
        {
            // ⚠️ 本测**刻意不建 panel**(纯静态函数 + 真谓词),少一层夹具失败面。

            // §6 / AC-20-18 / BL-27:满档 ⇔ **20 的**具名谓词 `carry_load > cap − W_MIN` 为真。
            // 本屏(`BandOf`)**不重算**它 —— 谓词由装配层调 `CarryLoadSolver` 本体求值,
            // 经 `InventoryContainerDto.IsFull` 传入。本测钉两件事:
            //   ⓐ 本屏确实**只消费**结果(不自己写一份式子 —— F8「行为等价掩盖结构分叉」);
            //   ⓑ 余下三档对 slack 单调不增、小箱不退化。

            // ── ⓐ 委派:本屏不得自己求满档 ─────────────────────────────────────
            // ⚠️ 这里喂的 `isFull = false` + `load == cap` 是**生产不可达**的组合
            //    (最小件重 ≥ 1 ⇒ 谓词为假时必有 `load < cap`),它**只**为可判别性而存在:
            //    若本屏哪天自己写成 `load >= cap`,这组会返回 Full ⇒ 本行红。
            //    换句话说 —— 这是「委派」这一属性**唯一**能从外部观测到的表达式。
            // ⚠️ 2026-11-03:原还声明了一条 `const int wMin = 3;` —— **从未被使用**
            //    (编译警告 CS0219),已删;上文括注里对它的指名改为「最小件重」。
            const int cap = 10;

            Assert.That(InventoryContainerScreen.BandOf(cap, cap, isFull: false),
                Is.Not.EqualTo(CarryBand.Full),
                "④ 满档**只认** 20 的谓词结果:谓词说不满 ⇒ 即使 load == cap 也不是 Full。" +
                "**若本屏自己写 `load >= cap`,此处返回 Full ⇒ 红**");

            // ── ⓑ 与真谓词联动:每一档的 Full 与否都必须与 `CarryLoadSolver` 一致 ──
            // 这里接的是**生产谓词本体**(不是复制品):两处口径一旦分叉,本扫必红。
            int[][] caps = { new[] { 10, 3 }, new[] { 3, 1 }, new[] { 60, 1 }, new[] { 7, 2 } };
            for (int c = 0; c < caps.Length; c++)
            {
                int capX = caps[c][0];
                int wMinX = caps[c][1];
                var capacity = new DaYiJingCheng.Sim.CarryCapacity(capX);

                CarryBand prev = CarryBand.Empty;
                for (int load = 0; load <= capX; load++)
                {
                    bool isFull = DaYiJingCheng.Sim.CarryLoadSolver.IsFullForAnyAddition(
                        load, capacity, wMinX);
                    CarryBand band = InventoryContainerScreen.BandOf(load, capX, isFull);

                    Assert.That(band == CarryBand.Full, Is.EqualTo(isFull),
                        $"④ cap={capX}, wMin={wMinX}, load={load}:本屏判 Full={band == CarryBand.Full}," +
                        $"而 `CarryLoadSolver.IsFullForAnyAddition` = {isFull} —— 两处口径须逐点一致" +
                        "(BL-27:20 出谓词、42 出分档)");

                    Assert.That((int)band, Is.GreaterThanOrEqualTo((int)prev),
                        $"④ cap={capX}, wMin={wMinX}:load = {load} 的档位 {band} 不得比 " +
                        $"load = {load - 1} 的 {prev} 更空(§6 的单调不增)");
                    prev = band;
                }
            }

            // ── 小箱不退化 ────────────────────────────────────────────────────
            // cap=3 / wMin=1:满档谓词 `load > 2` ⇒ load=2 非满。
            // 🔁 2026-11-10(T22 批二):切点改为 spec §6 的整数式 `2×load ≤ cap` ⇒ 半满。
            //    cap=3 / load=2:`2×2 = 4 > 3` ⇒ NearFull。任何用「cap/4 整除」的写法
            //    (`3/4 = 0`)会在这里得出 Half ⇒ 小箱档位退化。
            Assert.That(InventoryContainerScreen.BandOf(2, 3, isFull: false),
                Is.EqualTo(CarryBand.NearFull),
                "④ 小箱 cap=3:`load=2` ⇒ 非 Full · `2×2 > 3` ⇒ NearFull。" +
                "**若切点写成 cap 的比例(如 `cap/4` 整除),此处掉到 Half ⇒ 本测红**");

            Assert.That(InventoryContainerScreen.BandOf(0, cap, isFull: false),
                Is.EqualTo(CarryBand.Empty), "④ load = 0 ⇒ Empty");

            // ── 域守卫 ────────────────────────────────────────────────────────
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => InventoryContainerScreen.BandOf(-1L, cap, isFull: false),
                "④ 负载重 ⇒ 须抛(与 `CarryLoadSolver` 同一守卫:负值 = 求和出了错)");
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => InventoryContainerScreen.BandOf(5L, -1, isFull: false),
                "④ 负容量 ⇒ 须抛(`CARRY_CAP` ∈ int>0;0 = 未装配,不是非法)");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 空箱:零格 + 无焦点落点 + 1 页
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_emptyContainer_zeroCells_noFocusTarget_onePage()
        {
            yield return SetUp();

            _screen.Bind(new InventoryContainerDto(42L, System.Array.Empty<ContainerItemDto>(), 0L, 0, false));
            yield return null;

            Assert.That(_screen.CellCount, Is.Zero, "⑤ 空箱 ⇒ 零格");
            Assert.That(_screen.PageCount, Is.EqualTo(1), "⑤ 空箱 ⇒ 1 页(不是 0 页)");
            Assert.That(_screen.FirstFocusable, Is.Null,
                "⑤ 零格 ⇒ 无可落焦点(空屏不得假装有落点)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ `TD-skeuo-20`:开屏(首次 Bind)焦点落进第一格
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_tdSkeuo20_bind_focusesFirstCell()
        {
            yield return SetUp();

            var items = new[]
            {
                new ContainerItemDto(instanceId: 100L, itemOrdinal: 0, quality: 1, qty: 1),
                new ContainerItemDto(instanceId: 101L, itemOrdinal: 1, quality: 1, qty: 1),
            };
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            // 前置:屏须已挂到 panel 上(否则 Focus() 是空操作,断言会「因为没测到」而变红)
            Assert.That(_screen.FirstFocusable, Is.Not.Null, "⑥ 前置:有格 ⇒ 有首格");
            Assert.That(_screen.FirstFocusable.panel, Is.Not.Null, "⑥ 前置:首格须已挂 panel");

            VisualElement focused =
                _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused, Is.SameAs(_screen.FirstFocusable),
                "⑥ 开屏后焦点须落在第一格 —— 否则键盘/手柄的第一次 Return 落空(TD-skeuo-20)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦ 负控:换箱重绑后,焦点须回到**同一件物品**的格(重建会换元素,故按 id 还原)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_tdSkeuo20_rebind_keepsFocusOnSameInstance()
        {
            yield return SetUp();

            var items = new[]
            {
                new ContainerItemDto(100L, 0, 1, 1),
                new ContainerItemDto(101L, 1, 1, 1),
                new ContainerItemDto(102L, 2, 1, 1),
            };
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            // 玩家把焦点移到第 2 格(前置:焦点确实动了)
            VisualElement second = _screen.CellAt(1);
            second.Focus();
            yield return null;
            Assert.That(_doc.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(second), "⑦ 前置:焦点已到第 2 格(instance 101)");

            // 重绑(库存变动 / 换箱)—— 注意 `RebuildPage` 会**重造格**,旧元素已脱离 panel
            _screen.Bind(new InventoryContainerDto(42L, items, 0L, 0, false));
            yield return null;

            VisualElement focused =
                _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused, Is.SameAs(_screen.CellAt(1)),
                "⑦ 负控:重绑后焦点须回到 instance 101 的**新**格(不得被拽回第一格,也不得停在已脱离的旧元素上)");
            Assert.That(_screen.InstanceIdAt(1), Is.EqualTo(101L),
                "⑦ 前置:第 2 格确为 instance 101");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        private sealed class SpySink : IContainerSelectionSink
        {
            public long[] LastReported = System.Array.Empty<long>();

            public void OnSelectionChanged(IReadOnlyList<long> selectedInstanceIds)
            {
                var list = new List<long>(selectedInstanceIds);
                LastReported = list.ToArray();
            }
        }
    }
}
