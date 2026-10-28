# ADR-033 实现轮 · 证据原件 —— 结构 id 撞号的载入期归一化

**对象**:`docs/architecture/adr-033-structure-id-collision-on-load.md` 的实现四件 + §Validation V-1…V-7
**日期**:2026-10-28
**裁定出处**:用户 2026-10-28 裁定「**两边都留**」(债册 `TD-building-02` 「✅ 裁定已落」行)
**结论**:**四件已落,V-1…V-7 全部有载体且逐条反证转红;本轮另抓到并修掉一处我自己的接线缺陷**

---

## §一 落点(四件)

| # | 件 | 落点 | 备注 |
|---|---|---|---|
| 1 | **`StructureIdRemap` 纯函数** | `unity/Assets/Sim/World/StructureIdRemap.cs` | 住 `Sim`,只 `using System` / `System.Collections.Generic` ⇒ 引用集不变(b2 门 / ADR-025 §①) |
| 2 | **接线** | `unity/Assets/Gameplay.Boot/WorldLoadService.cs:149-171`(`Rebuild()` 内) | 解码之后、`RebuildFromDecoded` 之前;基线件数在**重放之前**读 |
| 3 | **写回事件流的接缝** | `WorldLoadService.cs:74-79`(可选形参对)+ `:186-231`(`RemapStructureIds`)+ `CompositionRoot.cs:976` | 只重编码**三条结构类**事件,事件头 `Tick` / `Patient` / `Seq` / `Kind` 一律不动 |
| 4 | **固化载体** | 存档 v2 载荷段 —— **上一提交 `a03a62c4` 已落** | ADR-033 §Risks R1 的硬约束:载体不同批 ⇒ 平移后的 id 无处持久化 |

**不碰的东西**(ADR-033 §③):`Removed` / `Modified` 的 `Cell` / `ModuleId` 原样保留,只换 `StructureId`。

---

## §二 §Validation V-1…V-7 逐条

夹具文件 = `unity/Assets/Tests/EditMode/ModularBuilding/structure_id_remap_test.cs`(10 条)。

| # | 判据(ADR 原文) | 载体 | 反证(停用什么 ⇒ 哪些转红) | 判定 |
|---|---|---|---|---|
| **V-1** | 判据必要性:后基线存档 + 引用基线号的 `Removed` ⇒ **不触发** | `test_remap_collisionCriterion_onlyPlacedWithinBaseline`(V-1 段)+ `test_load_postBaselineSave_isUntouched`(e2e) | **M1**(删 `Kind == Placed` 合取)⇒ 这 2 条红 | ✅ |
| **V-2** | 判据充分性:前基线存档含 `Placed(1)` ⇒ **触发** | 同上一半 + `test_load_preBaselineSave_shiftsIdsAndCuresStream` | **M8**(基线件数取 0)⇒ 两条红(**M6** 亦红) | ✅ |
| **V-3** | 三方不交:平移后号段与 `1..N`、与旧号段两两不交 | `test_remap_shiftedRange_isDisjointFromBaselineAndInjective` | **M4**(Alt 3)⇒ 该条红 + 2 条;M2(上界开区间)⇒ 1 条红 | ⚠️ **按可判定口径实现,见 §六** |
| **V-4** | 幂等(需固化载体):固化后重跑 ⇒ 恒等 | `test_load_preBaselineSave_shiftsIdsAndCuresStream`(二次 `Rebuild` 段) | **M5**(删固化调用)⇒ 该条红 | ✅ |
| **V-5** | Alt 3 / Alt 5 反例各一条夹具 | Alt 3 = `test_remap_wholeShift_remainsInjectiveWhenMaxIdExceedsBaseline`;Alt 5 = `test_remap_withoutCure_driftsEachReload_alt5Counterexample` | **M4** ⇒ 两条同时红(整体平移一被改坏,两个反例面同时失去前提) | ✅ |
| **V-6** | 全量回归无回退 | 见 §七 | 不适用变异(回归本身即判据) | ✅ |
| **V-7** | 混合存档不变量:写出的存档 id 空间非全旧即全新 | `test_load_preBaselineSave_shiftsIdsAndCuresStream`(`Collides` 复扫段)+ `test_load_collisionWithoutCureCapability_throwsAndLeavesStreamIntact`(未固化路径**拒绝**存在) | **M5** ⇒ 复扫段红;**M6**(fail-closed 退化)⇒ 拒绝段红 | ✅ |

> ⚠️ **V-2 的载体说明**:「触发方向」没有一条**只**测它的夹具(`test_remap_collisionCriterion_*` 的 V-2 段同时含 V-1 段)。
> 这一点如实登记:V-2 的反证只能借 M8 / M6 的**间接**红,不是一条专打 V-2 的变异。**未单独补夹具** —— 若后续有人收紧判据,需先补一条只测触发方向的。

### 新增的第 10 条(不属 V-1…V-7)

`test_load_curePreservesFoldedRows` —— 由 §四 的缺陷驱动,见该节。

---

## §三 证伪轮(9 条变异,一轮一条)

**纪律**:每条变异**单独**应用、**单独**跑、**单独**还原;改 → 跑 → 还原在**同一条命令**内完成;
还原判据 = **SHA-256 逐字节**,不用 `git checkout` / `git stash`。
驱动脚本 = `$CLAUDE_JOB_DIR/tmp/mutate.py` + `run-falsify.sh`(会话临时,不入库)。

**预判先落盘再跑**(`$CLAUDE_JOB_DIR/tmp/adr033-falsify-plan.md`),下表「预判」列逐字取 thereof。

| ID | 属性(一属性一条) | 变异 | 预判 | **实得红** | 判定 |
|---|---|---|---|---|---|
| M1 | 判据只扫 `Placed` | `Collides` 删 `op.Kind == Placed &&` | 2 | **2**(`onlyPlacedWithinBaseline`、`postBaselineSave`) | ✅ 逐条命中 |
| M2 | 上界**闭**区间 | `<= baselineCount` → `< baselineCount` | 1 | **1**(`onlyPlacedWithinBaseline`) | ✅ |
| M3 | 三支共用同一映射 | `Apply` 的 `Removed` / `Modified` 保留原 id | 1 | **1**(`apply_shiftsAllThreeKinds`) | ✅ |
| M4 | 整体平移(非 Alt 3) | 只在 `id <= N` 时加 `N` | 2 | **3**(+`shiftedRange_isDisjointFromBaselineAndInjective`) | ⚠️ **实得 > 预判**,见下 |
| M5 | 固化(写回事件流) | 删 `_cureStream(...)` 一行 | 1 | **1**(`preBaselineSave_shiftsIdsAndCuresStream`) | ✅ |
| M6 | fail-closed | 守卫恒 false + `toReplay = ops` | 1 | **2**(+`preBaselineSave_shiftsIdsAndCuresStream`) | ⚠️ **实得 > 预判**,见下 |
| M7 | 固化**时序**(重放成功后才固化) | 把 `_cureStream(...)` 提到 `RebuildFromDecoded` 之前 | 1 | **1**(`replayFailsAfterCollision_leavesStreamUncured`) | ✅ |
| M8 | 基线件数**读点**(基线未落库时读) | `baselineCount = 0` | **不确定(可能 0)** | **2**(`collisionWithoutCureCapability`、`preBaselineSave`) | ⚠️ 预判过保守,见下 |
| M9 | 折叠行保全(自引用守卫) | 删 `EventStream.Restore` 的 `ReferenceEquals` 守卫 | 1 | **1**(`curePreservesFoldedRows`) | ✅ |

**实得 > 预判的三条 —— 先独立论证「它为什么该红」,再订正(不回头凑理由)**:

- **M4 多红 1**:`test_remap_shiftedRange_...` 用 id `1..4`、`N=3`。Alt 3 下 `1,2,3 → 4,5,6` 而 `4` **原样**⇒ `4` 出现两次 ⇒ **单射确实被破坏**。它本来就该红;我预判时漏看了该夹具的 `M=4 > N=3`。
- **M6 多红 1**:静默不平移 ⇒ 流里 id 仍为 `1`,而 `test_load_preBaselineSave_...` 断言 `N+1` ⇒ 该红。
- **M8**:把基线件数取 0 后 `Collides` 恒 `false` ⇒ 既不抛也不平移 ⇒ 两条都该红。**预判本身写得太保守**(当时把「读点后移」误想成「读到的值可能仍正确」)。

**还原校验**(每条后均跑,末次输出):
```
72e47d3418bac18de1fdfd50f6b634fd7b90d7b38bfaaa9c7e611c212a1c3bf5  StructureIdRemap.cs
0cf49fb4198df1006f3054ee903890fdf213cae2621b504220669f38367e4fc2  WorldLoadService.cs
285be2bc453984a35f5d2229c5f5709b13376f47f4faa577871899a9ed7ab87f  EventStream.cs
```
复算:`sha256sum unity/Assets/Sim/World/StructureIdRemap.cs unity/Assets/Gameplay.Boot/WorldLoadService.cs unity/Assets/Sim/EventStream.cs`

---

## §四 本轮实跑抓到的缺陷 —— 折叠行自引用被静默抹除(**我自己的接线缺陷**)

**发现方式**:读 `EventStream.Clear()` 时发现,不是任何夹具抓到的。当时 9 条夹具全绿。

**机制**:
1. `EventStream.FoldedRows => _foldedRows` —— 返回的是**内部列表实例**;
2. `EventStream.Restore(events, foldedRows)` 先 `Clear()`(`:294` 会 `_foldedRows.Clear()`),末尾才 `_foldedRows.AddRange(foldedRows)`;
3. 我在装配根写的是 `cureStream: cured => stream.Restore(cured, stream.FoldedRows)` ⇒ 传进去的就是那个即将被清空的列表 ⇒ `AddRange` 把空列表加回自己 ⇒ **折叠行全丢,零异常、零日志**。

**后果(不只是少一条记录)**:`GetNextPatientId` 高水位依赖折叠行(ADR-006 Amendment B)⇒ 有 **`patient_id` 重用**风险;存档折叠段亦会变空。

**为什么夹具当时全绿**:`test_load_preBaselineSave_...` 的流**没有折叠行** —— 「没丢」与「本来就空」不可分。这正是「夹具无判别力」的形态。

**修法**:守卫放在**拥有该列表的类**里(`EventStream.Restore` 自引用即先物化快照),而不是各调用点 ——
只修装配根的话,下个调用者会再踩一次,而且**无法被夹具钉住**(夹具不走装配根)。
生产调用点全仓仅此一处(`grep -rn "Restore(.*FoldedRows" unity/Assets --include=*.cs | grep -v /Tests/`)。

**夹具**:第 10 条 `test_load_curePreservesFoldedRows`(前置断言「固化前折叠行非空」+「固化确实发生」两条都要满足,否则无判别力);
**反证 = M9**(删守卫 ⇒ 恰该条红,实得 1 红)。

**同批订正**:`WorldLoadService` 的 `<param name="cureStream">` 原文把错误用法写成了生产口径
(「生产 = 调 `EventStream.Restore(events, stream.FoldedRows)`」)—— 已就地改写并注明守卫位置。

---

## §五 本轮**就地裁定**、未经用户逐条确认的事项

⚠️ 与 ADR-034 同款披露:下面这条是**实现期必须定的形状**,ADR-033 未写,我按既有模式定了。**实现轮之前用户可改**。

1. **固化接缝的形状** = 可选形参**对** `(IPayloadEncoder encoder, Action<IReadOnlyList<SimEvent>> cureStream)`,
   两者皆缺而命中撞号判据 ⇒ `Rebuild()` **抛**(fail-closed)。
   - 依据:ADR-033 §⑥ 明写「固化改写已发生事件的 id」,不等于「新增一条状态」⇒ 不走 `IEventSink.Append`;
     选「整表替换」而非「逐条改写」是 `EventStream.Restore` 的既有替换语义(先 `Clear`)。
   - **fail-closed 而非静默跳过**的理由:跳过 = 允许写出**混合存档**(§④之补的硬不变量)。
   - **固化放在重放成功之后**:重放抛时事件流保持原样 ⇒ 下次载入按同一判据重试。反证 = **M7**。
2. **`Sim` 侧不引 codec**:`StructureIdRemap` 只吃 `StructureRestoreOp`;重编码在 `Gameplay.Boot` 做
   (`WorldLoadService.RemapStructureIds`,看得见 `Sim.Codec` 的一侧)⇒ ADR-025 §① / b2 门不变。

---

## §六 **V-3 的措辞缺陷**(如实登记,不改 ADR 原文)

**ADR-033 §Validation V-3 原文**:「平移后号段与 `1..N`、**与旧号段**两两不交」。

**该措辞字面上不可满足**,两条独立反例:
- **纯前基线存档**(`M ≤ N`,全部 id 都落在 `1..N` 内)⇒ 「旧号段」⊆ `1..N` ⇒ 若「平移后与旧号段不交」成立,则平移后与 `1..N` 不交也必然成立,反之亦然 —— 此时该条件与第一条**重复**,不构成独立判据;
- **`M > N`**(如 `1..6`, `N=3`)⇒ 旧号段是 `1..6`,新号段是 `4..9` ⇒ **必然相交**(`4,5,6`)⇒ 该条**恒假**。

**我实现的可判定对**(两条,均单调可证):
① `min(平移后 id) > N` ⇒ 与基线号段 `1..N` **不交**;
② 映射**单射**(`HashSet` 去重后条数不变)。

**为什么不改 ADR**:本件按「原文不删,就地加注」纪律,把措辞问题登记在此,**由用户裁定是否修订 ADR-033 §Validation V-3**;我不自裁改判据文本。

---

## §七 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `total=5724 passed=5689 failed=0 inconclusive=0 skipped=35` | 2026-10-28 23:44 CST | ADR-033 实现轮 | `a03a62c4` + 工作树 |
| PlayMode | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | 2026-10-28 23:52 CST | ADR-033 实现轮 | `a03a62c4` + 工作树 |

**与基线(ADR-034 轮 @ 2026-10-28 23:16)的等式**:
- EditMode `5724 − 5714 = 10` = **新增测试文件的 `[Test]` 计数 10**(`structure_id_remap_test.cs`)。
  已跟踪文件的 `git diff -U0 -- unity/Assets | grep -c "^+.*\[Test\]"` = **0**,删除 = **0**
  (新文件尚未跟踪 ⇒ 不出现在 `git diff` 里,故等式取自文件内计数,不取 diff)。
- PlayMode `238/234/0/2/2` 与基线**逐项相同**;2 条 `Inconclusive` 是 `U1Assump6GamepadFocusSpikeTest`
  的 `Assert.Inconclusive` 历史登记,2 条 `Skipped` 同理,该文件本轮**未改动**。

⚠️ **`EXIT=2` 的口径**:两轮 PlayMode 均 `EXIT=2` 而 XML `failed=0`、`<failure>` 节点 0 个
⇒ UTF 把 `Inconclusive` 计成非通过。**如实说明**:我**没有**保留 ADR-034 轮 PlayMode 的退出码记录,
因此只能断言「计数与未通过项身份未变」,**不能**断言「退出码未变」。

**v1 门不受影响**:本批未再改 `CurrentSaveVersion`(仍 = 2,`a03a62c4` 已立)。

---

## §八 残余 / 未覆盖(如实登记,不借绿)

1. **V-2 无专打变异**(见 §二末注)。当前靠 M8 / M6 间接覆盖。
2. **V-3 的 ADR 措辞待用户裁定是否修订**(§六)。
3. **固化会在池里留下孤儿 blob**:每次固化把三条结构事件的载荷重新编码 ⇒ 旧 `BlobId` 变孤儿。
   存档侧无影响(载荷段**引用驱动**,只写存活事件引用到的 `BlobId`,ADR-034 §②),
   但内存池会缓慢增长。今日不可达(零真实存档);**登记为观察项,不另立债**。
4. **`TD-ci-01`(CI 常驻矩阵从未实跑)与本件无关**,仍在册。
