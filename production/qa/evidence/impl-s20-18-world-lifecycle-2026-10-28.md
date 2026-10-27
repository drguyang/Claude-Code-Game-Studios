# S20-18 · World 生命周期轮 —— 实施与验证原件

**日期**:2026-10-28
**卡面**:`production/sprints/sprint-20.md` §十 · S20-18
**判定**:AC-1…AC-10 全部**已落 + 已证**;证伪轮 **18 条变异,18 条都让其目标断言转红**(见 §三 ——
主轮 17 条:15 exact + 2 超集;两条超集订正预期后复验 exact;另有 1 条 M12 为主轮后补的分支覆盖)。
**挂标不记绿**:6 条(见 §五),逐条照抄卡面,一条不勾。

---

## 一、为什么做这一轮

S20-17 卡内「挂标不记绿」第 4 条逐字:

> **`World` 的生命周期后果** —— 世界挂在 `BootRoot`(常驻)之上 ⇒ 换关 / 读档后
> 「旧世界还在」这件事**本卡不处理**,归 ADR-023 拆序轮的实现期义务。

2026-10-28 用户裁定「做 World 生命周期那一项」,路线取**甲 · 补全重建链**,范围取**全批照做**。
本卡即该义务的兑现。

### 病因三层(逐条可复算,基线 = `c784d88`)

| # | 缺陷 | 复算命令(基线 = 提交 `c784d88` 的 blob) | 实得 |
|---|---|---|---|
| ① | `World` **无重置入口** | `git show HEAD:unity/Assets/Sim/World/World.cs \| grep -c "public void Reset"` | `0` |
| ① | 且唯一近似物 `Clear()` **只清实例表** | `git show HEAD:unity/Assets/Sim/World/StructureKinds.cs \| sed -n '/public void Clear()/,/^        }/p'` | 体内仅 `_instances.Clear();`(不清 `World._slotOccupied`) |
| ① | 注册表**无 `Reset()`**(不复位发号) | `git show HEAD:unity/Assets/Sim/World/StructureKinds.cs \| grep -c "public void Reset()"` | `0` |
| ② | **无世界流重放器** | `git ls-tree -r --name-only HEAD unity/Assets/Sim/World/ \| grep -i restore` | 零命中 |
| ③ | `StructureWriter.Modify` **漏写占用** | `git show HEAD:unity/Assets/Sim/World/StructureKinds.cs \| sed -n '/public bool Modify(/,/^        }$/p' \| grep -c "FreeCells\|OccupyCells"` | `0` |

> `HEAD` 在收口时仍 = `c784d88`(本轮改动**未提交**),故上表命令在任何时刻重跑都复现同一结果。

**三层为什么必须一起做**:① 单独做 ⇒ 读档后世界**变空**(房子的记忆全没了,比残留更糟);
② 单独做 ⇒ 恒空跑(没有清零,重放只是叠加);③ 单独做 ⇒ 流是唯一真源、现场不还原真足迹,
**重放与现场分叉** —— 重放器再正确也会还原出一个与存档**不同**的世界。

### 边界

- **只做 sim 侧 `World` 对象**,不碰 ADR-023 §⑤ 的场景 / Addressables 拆序
  (`Scenes/World.unity` 存在但不在 BuildSettings、零加载点)。
- **世界几何不动**:`WorldGeometry` / `BuildSlotCatalog` 来自烘焙静态层(ADR-015),
  读档不改变 ⇒ `Reset()` 只清**派生态**两样(实例表 + 占用表)。
- 形态照 `PoiStateMachine.RebuildFromDecoded`:**`Sim` 侧不自行解码**(够不着 `Sim.Codec`,
  ADR-029 / ADR-031 甲案),由 Boot 层 `WorldLoadService` 解码后喂入 `StructureRestorer`。

---

## 二、AC 结账表(Landed / Proven 分列)

| # | 判据 | Landed(已落) | Proven(已证) | Proven-by(可复算) |
|---|---|---|---|---|
| **AC-1** | `World.Reset()` 后 `OccupiedCount == 0` **且** `Structures.Count == 0`,旧足迹格恢复可走 | ✅ `Sim/World/World.cs:51-55`(`:54` `Array.Clear(_slotOccupied…)`)+ `StructureKinds.cs:188-192` | ✅ | `structure_restore_test.test_ac1_reset_clearsOccupancyAndRegistry`;反证 **M1** |
| **AC-2** | 注册表 `Reset()` **复位发号** ⇒ 其后首次 `Register` 返回 `1` | ✅ `StructureKinds.cs:191` `_nextStructureId = 1;` | ✅ | `test_ac2_reset_resetsIdCounter`;反证 **M2** |
| **AC-3** | `Placed` 重放后实例明细 / 占用表 / `IsEffectivelyWalkable` 与现场放置**逐格一致**(含非锚点足迹格) | ✅ `Sim/World/StructureRestore.cs:188` `OccupyCells` | ✅ | `test_ac3_replayPlaced_restoresFootprintAndWalkability`;反证 **M3** |
| **AC-4** | `Placed → Removed` 重放 ⇒ 净空 | ✅ `StructureRestore.cs:200` `FreeCells` | ✅ | `test_ac4_replayPlaceThenRemove_leavesWorldEmpty`;反证 **M4** |
| **AC-5** | `Placed → Modified(旋转)` 重放 ⇒ 旧足迹释放 / 新足迹占用 / 与 `ComputeOccupiedCells(新朝向)` 逐格相等 | ✅ `StructureRestore.cs:213` `FootprintOf(… op.Orientation …)` + `:215/:216` | ✅ | `test_ac5_replayRotate_movesFootprint`;反证 **M5** |
| **AC-6** | 重放**按事件原号保号**,高水位 = `max(id)+1` ⇒ 重放后 `Place` 不撞号 | ✅ `StructureKinds.cs:226` `_nextStructureId = structureId + 1;` | ✅ | `test_ac6_replayKeepsIdsAndBumpsHighWater`(`Assert.AreEqual(6L, next …)`);反证 **M6** |
| **AC-7** | fail-closed:目录未注入 / `moduleId` 不在目录 / `Removed`·`Modified` 指向不存在 id / `ops == null` ⇒ 各抛,**且抛后世界 = 重置后的空态**(原子性) | ✅ `StructureRestore.cs:133`(目录)· `:157`(ops)· `:174`(catch 内 `_world.Reset()` 回滚) | ✅ | `test_ac7a/b/c/d/e`;反证 **M7**(原子性)· **G4/G5**(两守卫) |
| **AC-8** | 装配接线:给 `world` ⇒ `WorldLoad != null`;**不给 `world` ⇒ `null`** | ✅ `Gameplay.Boot/CompositionRoot.cs:889` | ✅ | `world_load_service_test.test_ac8` / `test_ac8b`;反证 **M8** |
| **AC-9** | 端到端:含 `StructurePlaced` 的流 ⇒ `Rebuild()` 后世界恢复到流所述形态;**空流 ⇒ 清空**;**二次重建从零开始** | ✅ `Gameplay.Boot/WorldLoadService.cs:114` + `StructureRestore.cs:159` `_world.Reset();` | ✅ | `test_ac9` / `ac9b` / `ac9c` / `ac9d` / `ac9e` / `ac9f`;反证 **M9** + **M11** |
| **AC-10** | `StructureWriter.Modify` 旋转后 ⇒ 旧足迹**可走**、新足迹**不可走**、注册表记**新足迹** | ✅ `StructureKinds.cs:477` `FreeCells` + `:478` `OccupyCells` + `:481` `Update(…, newCells)` | ✅ | `test_ac10_modifyAppliesFootprintChange`;反证 **M10**(释放半边)+ **M12**(占用半边) |

> **Branch(分支覆盖)** —— 按 `review-workflow` Phase 8b 的口径,多分支判定**每个分支各要一条反证**。
> 本件的三处多分支:
> - **AC-7**(四个独立守卫):M7(回滚)· G4(目录守卫,两入口)· G5(ops 守卫)· `test_ac7d` —— 无遗漏;
> - **AC-8**(给 world / 不给 world):`test_ac8` / `test_ac8b` 各一例,M8 同时钉住两侧;
> - **AC-10**(合取:旧格可走 **∧** 新格不可走):**主轮只有 M10,覆盖「释放旧足迹」半边** ⇒
>   按同一纪律**补 M12**(「占用新足迹」半边),单跑复验(见 §3.4)。
>   ⚠️ 这一补正是 sprint-18 S18-2 的教训:当时折叠谓词 4/4 全红却**没有一条**打产出侧分支,
>   「产出面已验证」是事后才被推翻的。

### 夹具设计上的两处自纠(实现轮,记录在此以免被当成「一次就写对」)

1. **`test_ac9f` 首版是假绿。** 初版 `test_ac9f_rebuildIsIdempotent` 断言「连跑两次结果相同」——
   这对**任何**流的确定性函数都恒成立,**零判别力**:没有任何变异能弄红它。
   已改写为 `test_ac9f_secondRebuild_startsFromCleanSlate`(重建 → 弄脏世界 → 再重建 → 脏结构必须消失),
   钉的是 `StructureRestore.cs:159` 那次重建前的 `Reset()`;反证 = **M11**。
   ⚠️ 这正是 S12-4 列出的第 ④ 类「一次性 / 幂等类」判据,**首版恰好是它想抓的那种假绿**。
2. **AC-9 的判别力必须靠「先弄脏」。** 若只在空世界上下重放,「重建成功」与「什么都不做」
   observably 相同(`test_ac9b` 会与「保持原样」同时成立)。故每个腿都先用**第二个写者**
   (事件进 `DiscardingSink`,不进流)往同一世界放一条**不在流里**的结构;
   重放后它必须**消失**。`DiscardingSink` 不是 Fake 生产件,而是对「上一局的流已经不在」的建模。

---

## 三、证伪轮(S12-4 · 一属性一变异 · SHA-256 逐字节还原)

**命令**(可当场重跑):

```bash
python3 -u tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/s20-18-world-lifecycle-restore.json \
  --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
```

**口径**:`mode=EditMode`,`filter=StructureRestoreTest;world_load_service_test`(26 测)。
filter 必须收窄到本卡两个夹具 —— M1/M2/M6/M10 打在 `Sim/World`(多夹具共用),
放宽会让 `world_end_to_end_test` / `clinic_purity_replay_test` 一起红,红集不可归因。

### 主轮实测(17 条,一次跑完,预注册预期 vs 实得)

| # | 变异 | 落点 | 预期红 | 实得红 | 判定 |
|---|---|---|---|---|---|
| **M1** | 停用 `Array.Clear(_slotOccupied…)` | `Sim/World/World.cs:54` | 6 | **7**(+`ac9f`) | ⚠️ 超集,订正后复验(见 §3.2) |
| **M2** | 停用 `_nextStructureId = 1;` | `StructureKinds.cs:191` | 1 | 1 | ✅ exact |
| **M3** | 停用重放 `OccupyCells` | `StructureRestore.cs:188` | 2 | 2 | ✅ exact |
| **M4** | 停用重放 `FreeCells` | `StructureRestore.cs:200` | 3 | 3 | ✅ exact |
| **M5** | 用 `inst.Orientation` 顶掉 `op.Orientation` | `StructureRestore.cs:213` | 2 | 2 | ✅ exact |
| **M6** | 停用高水位抬升 | `StructureKinds.cs:226` | 1 | **2**(+`ac9f`) | ⚠️ 超集,订正后复验(见 §3.2) |
| **M7** | 删 catch 内的回滚 `_world.Reset()` | `StructureRestore.cs:174` | 3 | 3 | ✅ exact |
| **M8** | `worldLoad = null;` | `CompositionRoot.cs:889` | 2 | 2 | ✅ exact |
| **M9** | 重放传空 op 集 | `WorldLoadService.cs:114` | 4 | 4 | ✅ exact |
| **M10** | 停用 `Modify` 的 `FreeCells` | `StructureKinds.cs:477` | 1 | 1 | ✅ exact |
| **M11** | 删重建前的 `_world.Reset()` | `StructureRestore.cs:159` | 3 | 3 | ✅ exact |
| **G1** | 停用 events 守卫 | `WorldLoadService.cs:53` | 1 | 1 | ✅ exact |
| **G2** | 停用 decoder 守卫 | `WorldLoadService.cs:54` | 1 | 1 | ✅ exact |
| **G3** | 停用 world 守卫 | `StructureRestore.cs:132` | 1 | 1 | ✅ exact |
| **G4** | 停用 moduleCatalog 守卫 | `StructureRestore.cs:133` | 2 | 2 | ✅ exact |
| **G5** | 停用 ops 守卫 | `StructureRestore.cs:157` | 1 | 1 | ✅ exact |
| **G6** | 停用 `Modify` 的目录守卫 | `StructureKinds.cs:458` | 1 | 1 | ✅ exact |

**核心结论:17 条变异,17 条都让它所针对的断言转红 —— 零逃逸。**

还原与留痕(日志尾部原文):

```
──── 还原:SHA-256 逐字节校验通过 ────
──── 判据二:受保护路径**指纹未变**(证伪轮不留痕)────
✅ 指纹未变 —— 受保护路径:unity/Assets/DataCooked, unity/Assets/AddressableAssetsData
```

`grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l` → **0**(变异串零残留)。

### 3.1 为什么是 17 条,而不是卡面最初写的 9 条

卡面原写「9 条(AC-8+9 合一条)」。实现轮做了**两次订正,方向相反**:

- **9 → 10**(拆):AC-8(装配接线)与 AC-9(端到端重建)是两个正交属性,可由不同文件的单行守卫
  分别击破(`CompositionRoot.cs:889` / `WorldLoadService.cs:114`)⇒ 一条变异证不了两个。
- **10 → 17**(补):头 10 条只覆盖**AC 主干断言**,漏掉 S12-4 命中形态的两类 ——
  ④ 一次性 / 幂等(`ac9f`,由 **M11** 补)· ⑤ 阈值边界 / 参数守卫(G1–G6 六条,此前
  **零反证**)。「入口即拒」类判据**不反证就等于没测**:只断异常类型会被下层同类型异常冒充,
  故六个 `ArgumentNullException` 守卫各占一条变异。

> **G4 一变异两红是刻意的**:同一守卫有**两个入口**(直接构造 `StructureRestorer` /
> 经 `WorldLoadService` 转发),`test_ac7e` 与 `test_ac8g` 各证一次。这不是判别面不清 ——
> 是同一守卫在两个入口上的**完整**覆盖。

### 3.2 两条超集:预期漏列,不是变异打偏(原预期与订正理由逐字保留)

主轮里 **M1 / M6** 各多红了一条 `test_ac9f_secondRebuild_startsFromCleanSlate`。
**两条都不是「变异打到预期之外的断言」,而是我的推演漏算了 `ac9f`** ——
`ac9f` 是本轮**最后**补进去的夹具(它的首版是假绿,被改写成「重建 → 弄脏 → 再重建」),
而 M1/M6 的预期是在它之前写死的。原推演理由逐字如下,并附上它错在哪:

| 变异 | 原预期 | 原推演理由(逐字) | 错在哪 | 机制(为什么必然多这一红) |
|---|---|---|---|---|
| **M1** | 6 条,**不含** `ac9f` | 「ac9f(重放最终落点与残留格重叠或随后释放)」 | **事实错误** —— ac9f 的残留结构在 `(3,0,3)`、流内结构在 `(1,0,1)`,**不相邻、不重叠** | 占用表不清 ⇒ 残留格在第二次重建后**仍被占** ⇒ `IsEffectivelyWalkable(strayAnchor)` 为假 |
| **M6** | 1 条,**不含** `ac9f` | 「其余夹具:重放后**没有**新的 `Register`(ac9x 里的 `writer.Place` 都发生在重放**之前**)」 | **ac9f 是例外** —— 它在第一次重建**之后**再 `PlantStructureOutsideStream` | 不抬水位 ⇒ `Reset()` 后 `_nextStructureId` 停在 1 ⇒ 残留写者的 `Register` **也返回 1** ⇒ 覆盖流内 id=1 ⇒ `Structures.Count` 不增 ⇒ ac9f 的前置「世界已被弄脏」为假 |

**处置**:把两条的 `expectRed` 各 += `test_ac9f`(`_note` 里保留原预期与订正理由原文),
**另起一轮只跑这两条**复验是否 exact。复验结果单列于 §3.3 —— **不与主轮结果混记**。

### 3.3 复验轮(仅 M1 / M6,预期订正后)

```bash
python3 -u tools/falsify/unity_falsify.py \
  --spec <仅含 M1/M6 的临时规格> \
  --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
```

```
──── 施加 M1…:World.cs:54 ────   total=26 passed=19 failed=7
  ✅ 恰如预期转红:['test_ac1…', 'test_ac7a…', 'test_ac7b…', 'test_ac7c…',
                  'test_ac9_streamWithPlaced…', 'test_ac9b…', 'test_ac9f…']
──── 施加 M6…:StructureKinds.cs:226 ────   total=26 passed=24 failed=2
  ✅ 恰如预期转红:['test_ac6_replayKeepsIdsAndBumpsHighWater',
                  'test_ac9f_secondRebuild_startsFromCleanSlate']
──── 还原:SHA-256 逐字节校验通过 ────
✅ 指纹未变 —— unity/Assets/DataCooked, unity/Assets/AddressableAssetsData
══ 证伪轮通过:2 个变异全部恰如预期 ══
```

**复验结论**:订正后两条均 exact。**主轮 15 exact + 2 超集** 与 **复验 2 exact** 分列登记,
合并口径写「**17 条变异,17 条都让其目标断言转红;其中 2 条在主轮的命中集比预期宽一条,
逐条归因后订正预期并复验 exact**」—— **不把复验结果回填成主轮结果**。

### 3.4 补证轮 M12(分支覆盖,主轮之后新增)

**为什么要补**:主轮的 AC-10 只有 **M10**(停用 `FreeCells(inst.OccupiedCells)`,打「旧足迹须释放」
半边)。AC-10 是**合取判据**(旧格可走 **∧** 新格不可走),而 `StructureKinds.cs:478`
`_occupancy.OccupyCells(newCells)` 那半边 **零反证**。按一属性一条变异 + 分支覆盖纪律补 **M12**。

```bash
python3 -u tools/falsify/unity_falsify.py --spec <仅含 M12 的临时规格> \
  --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData
```

```
──── 施加 M12-ac10-modifyFootprintOccupyRemoved:unity/Assets/Sim/World/StructureKinds.cs:478 ────
  total=26 passed=25 failed=1 skipped=0
    RED: test_ac10_modifyAppliesFootprintChange
  ✅ 恰如预期转红:['test_ac10_modifyAppliesFootprintChange']
──── 还原:SHA-256 逐字节校验通过 ────
✅ 指纹未变 —— unity/Assets/DataCooked, unity/Assets/AddressableAssetsData
══ 证伪轮通过:1 个变异全部恰如预期 ══
```

### 3.5 证伪轮总账

| 轮次 | 变异数 | exact | 超集 | 逃逸 | 还原 |
|---|---|---|---|---|---|
| 主轮 | 17 | 15 | 2(M1 / M6,均订正后复验) | **0** | SHA-256 逐字节通过 · 指纹未变 |
| 复验轮(M1 / M6) | 2 | 2 | 0 | 0 | 同上 |
| 补证轮(M12) | 1 | 1 | 0 | 0 | 同上 |
| **合计(去重)** | **18** | — | — | **0** | — |

`grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l` → **0**(三轮之后复核,变异串零残留)。

---

## 四、全量回归

### 4.1 基线溯源三元组(强制)

| 套件 | 截至时刻(UTC) | 批次 | commit |
|---|---|---|---|
| EditMode | `2026-10-27T22:59` | S20-18 收口轮(最终树,证伪轮之后) | `c784d88` |
| PlayMode | `2026-10-27T23:02` | 同上 | `c784d88` |

> 回归数**不是**从旧件抄来的:两条都是本轮实跑,`unity/Logs/` 下留有各自的 XML。

### 4.2 EditMode

```bash
unity test unity --mode EditMode --output unity/Logs/s20-18-final-editmode.xml
```

| 项 | 值 |
|---|---|
| XML | `total=5601 passed=5566 failed=0 skipped=35 inconclusive=0` |
| 对照基线 | `5575 / 5540 / 0 / 35` —— **引用自**`production/session-state/active.md` 的 S20-17 收口记录(2026-10-28 · commit `c784d88`),**不是本轮实跑** |
| 差值 | **+26 / +26 / 0 / 0** |

**+26 的来源可逐条对上**:本轮新增两个夹具各 13 测 ——
`StructureRestoreTest` 13 + `world_load_service_test` 13 = 26。等式成立:
`5601 − 5575 = 26`,且 `5566 − 5540 = 26`。**零失败、零跳过变化。**

### 4.3 PlayMode

```bash
unity test unity --mode PlayMode --output unity/Logs/s20-18-final-playmode.xml
```

| 项 | 值 |
|---|---|
| XML | `total=238 passed=234 failed=0 skipped=2 inconclusive=2` |
| 进程退出码 | **2** |
| 对照基线 | `238 / 234 / 0 / 2 / 2`(退出码 2)—— 引用自 `production/session-state/active.md` 的 S20-17 收口记录 |
| 差值 | **逐项相同,零回归** |

> ⚠️ **退出码 2 不是本轮引入的**:基线即有此退出码,成因是 `inconclusive=2` + `skipped=2`
> (`failed=0`)。wrapper 的 `Error: 测试失败:Unity 进程以代码 2 退出` 这句是**按退出码**打的,
> 不区分「有失败用例」与「有 inconclusive」—— 判失败用例数须读 XML 的 `failed`,不能读这句。

### 4.4 一句话结论

| 套件 | 本轮 | 基线(引用) | 差值 | 失败 |
|---|---|---|---|---|
| EditMode | `5601 / 5566 / 0 / 35` | `5575 / 5540 / 0 / 35` | +26 / +26 | **0** |
| PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | 0 | **0** |

---

## 五、挂标不记绿(照抄卡面,一条不勾)

| # | 项 | 复算 / 依据 | 状态 |
|---|---|---|---|
| 1 | **重放器的生产实效 = 零** | `StructureWriter.Place` 的生产调用点 `grep` 实测 = 0 ⇒ 生产流**永不含** `StructurePlaced` ⇒ 重放器在生产上**恒空跑**,只有夹具走到 | ❌ 未勾 |
| 2 | **ADR-023 §⑤ 场景 / Addressables 拆序不在面内** | 那要等 `World.unity` 真被 additive 加载;本轮零加载点 | ❌ 未勾 |
| 3 | **`RequestLoad` 仍零调用方** | `BootRoot.RequestLoad` 只有定义(`BootRoot.cs:842`)+ 注释,零调用 | ❌ 未勾 |
| 4 | **`BuildSlotCatalog` 仍空** | `new BuildSlotCatalog(` 唯一落点 `MinimalWorldFactory.cs:99`,其注释即「空目录:RegisterSlot 零调用」 | ❌ 未勾 |
| 5 | **`Modify` 修复只覆盖「足迹随朝向变化」** | 朝向合法性仍由上游 `ModifiableChecker` 守,本批不重复判定 | ❌ 未勾 |
| 6 | **在场视图 / `PatientAppearedDriver` 内存态 / 视觉侧** 仍不在读档重建面 | 本批**只加世界注册表一项**,不代 `LoadSequencer` 类头注的既有清单清账 | ❌ 未勾 |

> **「重放器已接线」≠「读档已可用」** —— 第 1 与第 3 条同时成立:服务被构造、重放逻辑可证,
> 但**触发点零调用**且**生产流零 `StructurePlaced`**。两者都到位之前,读档路径是「通电待信」。

**四条挂标的实跑复算(逐条贴输出,不是散文)**:

```bash
# 挂标 1 —— 生产树零 Place 调用点(实得:0 行)
grep -rn "\.Place(" unity/Assets --include=*.cs | grep -v "/Tests/" | wc -l
# 0

# 挂标 3 —— RequestLoad 只有定义,零调用
# 未滤注释时命中 8 行(6 行是注释);滤掉注释(`grep -v "//"`)后实得 2 行:
grep -rn "RequestLoad" unity/Assets --include=*.cs | grep -v "/Tests/" | grep -v "//"
# unity/Assets/Gameplay.Boot/BootRoot.cs:842:        public void RequestLoad(Sim.Codec.SaveSlot slot)
# unity/Assets/Gameplay.Boot/BootRoot.cs:848:                    "生产装配必须走完整 BootRoot 启动序,RequestLoad 不接受部分装配");

# 挂标 4 —— BuildSlotCatalog 生产唯一落点(实得:1 行,且该行注释即「空目录」)
grep -rn "new BuildSlotCatalog(" unity/Assets --include=*.cs | grep -v "/Tests/"
# unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs:99:            var slotCatalog = new BuildSlotCatalog();   // 空目录:RegisterSlot 零调用
```

> ⚠️ 挂标 2 / 5 / 6 是**边界与范围的陈述**,不是可计数的判据面(「不在本卡面内」/「只覆盖 X」/
> 「不代它清账」),故不给命令 —— 它们的作用是**限制本件的可主张范围**,而非证明某物存在。

---

## 六、改动的文件

| 类 | 文件 | 说明 |
|---|---|---|
| 新增 | `unity/Assets/Sim/World/StructureRestore.cs` | `StructureRestoreKind` / `StructureRestoreOp` / `StructureRestorer.RebuildFromDecoded`(先清后重放 + 失败回滚) |
| 新增 | `unity/Assets/Gameplay.Boot/WorldLoadService.cs` | Boot 层解码组装(四支 fail-closed),`Rebuild()` → `StructureRestorer` |
| 改 | `unity/Assets/Sim/World/World.cs` | 新增 `Reset()`(清 `_slotOccupied` + 委派注册表 `Reset()`) |
| 改 | `unity/Assets/Sim/World/StructureKinds.cs` | 注册表新增 `Reset()`(清表 + 复位 `_nextStructureId = 1`)与 `Restore(...)`(按原号落库 + 抬高高水位);`StructureWriter.Modify` 补 `FreeCells` / `OccupyCells` + 目录守卫 |
| 改 | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 给 `world` 即构造 `WorldLoadService`(否则 `null`);服务袋新增 `WorldLoad` |
| 改 | `unity/Assets/Gameplay.Boot/CompositionRootServices.cs` | 新增 `WorldLoad` 属性 |
| 新增 | `unity/Assets/Tests/EditMode/ModularBuilding/structure_restore_test.cs` | 13 测(AC-1…AC-7、AC-10) |
| 新增 | `unity/Assets/Tests/EditMode/ModularBuilding/world_load_service_test.cs` | 13 测(AC-8、AC-9) |
| 新增 | `tools/falsify/specs/s20-18-world-lifecycle-restore.json` | **18 条**变异 spec(主轮 17 + 补 M12) |
| 改 | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_purity_replay_test.cs` | 随生产改动同步(见 §七) |

---

## 七、未及核查项 / 已知偏差

### 7.1 实施轮订正了一处既有夹具的**非法输入**(不是生产行为回归)

`unity/Assets/Tests/EditMode/ClinicMachine/clinic_purity_replay_test.cs:482`:
`newOrientation: 1` → **`90`**。

- **`1` 不是合法朝向。** `PlaceableChecker.ComputeOccupiedCells` 的 `switch`(`:121`)
  只认 `0/90/180/270`,其余走 `default: throw new ArgumentException(...)`(`:127`)。
- **为什么以前不炸**:改之前 `StructureWriter.Modify` **从不重算足迹** ⇒ 非法朝向被
  **静默存下并原样进载荷**。S20-18 修好「旋转必须改占用表」后,`Modify` 会真的去算足迹
  ⇒ 非法值**当场抛**。
- **定性**:该夹具用了**非法输入**,被生产缺陷掩盖;不是生产行为回归。
  订正为合法值 `90`;且床是 1 格足迹 ⇒ 旋转前后足迹恒为锚点格,该测原有的
  `EnvMod` / `RoomType` 不变断言**照旧成立**,不改变该测的判据面。
- 就地加注已写入该测试文件(`:474-481` 的注释块)。

### 7.2 行号漂移(S20-17 证据件)

S20-17 原件把生产树 `new StructureWriter(` 的落点记为 `CompositionRoot.cs:858`。
本轮在该行**上方**插入了 `WorldLoadService` 的声明与构造块 ⇒ 现为 **`:879`**。
**判据本身不变**(生产树恰 1 处),只是行号随插入下移:

```bash
grep -rn "new StructureWriter(" unity/Assets --include=*.cs | grep -v "/Tests/"
# → unity/Assets/Gameplay.Boot/CompositionRoot.cs:879(恰 1 行)
```

### 7.3 未及核查项(**NOT-RUN**,禁借绿)

| # | 项 | 为什么没做 | 要证它需要什么 |
|---|---|---|---|
| 1 | **`afterRestore` 钩子内 `worldLoad?.Rebuild()` 的调用本身** | 夹具只证到「`WorldLoad` 被构造」(AC-8)与「`Rebuild()` 行为正确」(AC-9),**没有一条**证明闭包真的调了它。落点 `CompositionRoot.cs:707-711` | 经 `LoadSequencer.Load(...)` 触发 —— 需真存档夹具;而 `BootRoot.RequestLoad` **零调用方**(挂标 #3),当前无生产入口可借 |
| 2 | 真存档往返(存档 → 读档 → 世界恢复) | 本卡只做到 `Rebuild()` | 上线 `SaveService` + `LoadSequencer` 的端到端夹具 |
| 3 | 重放器在生产上的**实效** | 生产流永不含 `StructurePlaced`(挂标 #1) | 42 侧建造发起方接线(`O-23-6`) |
| 4 | `Scenes/World.unity` 真被 additive 加载后的行为 | ADR-023 §⑤ 拆序不在本卡面内(挂标 #2) | 拆序轮 |

> ⚠️ 第 1 条按本仓「『某件有门』的自述须给 `file:line` + 可跑命令,缺一即按 `NOT-RUN` 登记」
> 的口径处理:**给了 `file:line`,但给不出能证明它真的会被调用的命令** ⇒ 登记为 NOT-RUN,
> 不按「已接线」计入任何判据面。
