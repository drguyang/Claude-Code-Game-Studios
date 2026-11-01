# 24 复活床格端口(规则六)—— 实现 + flaky 根治 · 证据

**日期**: 2026-11-01
**批次**: 用户指令「远端变体(45)/ 相机两段式(机位轮)/ 床格端口(24)归属不变。这个你来测试修复」
**裁定**: 用户选 **「只做 ①(推荐)」** —— 床格端口完整实现 + 测试;②③ 登记为债,不假装做。

---

## 1. 三项的勘察结论(为什么只做 ①)

| 项 | 归属系统状态 | 能否真做 | 结论 |
|---|---|---|---|
| **① 床格端口** | **24 = 6/6 Complete**,GDD 规则六已定(2026-10-30 补节),只差「24 输出 `revive_cell`」+ 装配 | **能完整实现** | ✅ **本批做** |
| ② 相机两段式 | 2 = 6/6 Complete,但「下沉 + 微倾」机位动作需新机位实现,属 2 的**数值轮** | 只能做「按相位切档位」的接线,**越界** | ❌ 登记,归属不变 |
| ③ 远端变体 | **45 = P1b「未开始」**,零网络实现;`IPositionalChannel` 亦无生产实现 | **P0 单机下结构上跑不通** | ❌ 登记,归属不变 |

> ②③ 的「归属不变」不是推诿:② 要做的是 2 的数值轮才定的机位曲线,③ 要做的是 45 才有的网络通道。
> 在 P0 单机里造它们的临时桩,会让「看起来做完了」掩盖「真联机仍不可用」—— 那是本仓 F2/F6 同族的假绿。

---

## 2. 实现(四件)

| 文件 | 层 | 职责 |
|---|---|---|
| `Sim/Clinic/ClinicReviveCellRule.cs` | `Sim`(门 A 内,引用集恰 = {BCL, Sim.Contracts}) | **规则六的纯判定**:候选集判据 + `(y,x,z)` 字典序 min + 无候选返回 null |
| `Gameplay.Presentation/ClinicReviveCellQuery.cs` | `Gameplay.Presentation` | `IClinicReviveCell` 的**生产实现**;链 = 23 注册表 → 视图 → 房间析出(真判据)→ 规则六 |
| `Sim/RespawnCommandIssuer.cs` | `Sim` | 新增 `AttachClinicCell` **后置注入**(装配序约束,见下) |
| `Gameplay.Boot/CompositionRoot.cs` | 装配层 | 在 24 段注入真查询面 |

### 2.1 为什么判定住 `Sim`、胶水住 `Presentation`

链上两个零件(`StructureInstanceReadView` / `BuildModulesCatalog`)要读 cooked 字节,
而 `Sim` 引用集恰 = `{BCL, Sim.Contracts}`(ADR-025 §①)⇒ 胶水只能住装配侧。
与既有 `ClinicRoomMatchingService` **同款**先例。

### 2.2 为什么是后置注入(`AttachClinicCell`)而不是构造形参

装配序硬约束:`RespawnCommandIssuer` 在**结算段**(`CompositionRoot.cs:403`)构造,
而 24 的三份烘焙数据要等**世界段**(`~:1186`,依赖 `structures != null` + 三次产物装载)才具备。
提前 24 段 ⇒ 产物未装载即抛;推后结算段 ⇒ 动 40 余处下游次序。
⇒ 后置注入代价最小,且 `二次注入抛`(fail-loud,承 `TeleportCommandSink.BindPlayer` 同纪律)。

### 2.3 一处**口径订正**(如实登记,非裁量)

GDD 规则六 ① 写 `m.has_tag("bed")`,但**全仓无 tag 字段**(2026-11-01 实测两张表):
- `assets/data/build_modules.json`:10 行,键 = `module_id / module_type / furn_type / …`,床 = `furn_type: "bed"`
- `assets/data/item_database_furniture.json`:7 行,键 = `type / tier / e_env`,床 = `type: "bed"`

⇒ 按 `furn_type == "bed"` 实现,判据收敛为**唯一常量** `BedFurnType`(GDD 的意图「加新床型不改本节」
由单一常量兑现,不由字面匹配模块 id 兑现 —— 那正是 GDD 禁止的)。

---

## 3. 测试(12 条,`clinic_revive_cell_test.cs`)

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_ruleSix_keyOrder_isYThenXThenZ` | 排序键三段**逐段独立**(每用例只在一个键上不同) |
| 2 | `test_ruleSix_selectionIndependentOfInputOrder` | **确定性**:穷举 3! = 6 种输入序,结果逐位相同 |
| 3 | `test_ruleSix_nonBedFurnitureExcluded` | 候选集判据(非床类不进) |
| 4 | `test_ruleSix_noCandidate_returnsNull_notZeroSentinel` | 无候选 ⇒ null,**且反歧义**(床在原点 ≠ 无床) |
| 5 | `test_ruleSix_nullInputs_throw` | 构造器守卫 |
| 6 | `test_query_bedInRoom_returnsDeterministicCell` | 生产链端到端(**真**注册表 + **真**数据集,不打桩) |
| 7 | `test_query_noBed_returnsFalse_noFallbackCell` | **24 不填兜底**(兜底是 29 的裁决面) |
| 8 | `test_query_emptyLayout_returnsFalse_notThrow` | 空布局是合法输入(EC-24-01) |
| 9 | `test_query_negativeActorId_throws` | fail-loud |
| 10 | `test_query_nullDependency_throws` | 三支依赖各造一个 null 用例 |
| 11 | `test_attachClinicCell_injectsOnce_secondThrows` | 后置注入 + 二次注入抛 |
| 12 | `test_query_hasNoInlineOrderingLogic` | **零判定逻辑**:源码内不得内联比较 / 排序(**含正控**) |

> ⚠️ 既有 `death_respawn_command_test` 的 `FixedBed` 桩**不算本件证据** ——
> 它测的是 `RespawnCommandIssuer` 的两分支选择,**不测规则六本体**。本件正是补这个空。

### 3.1 首轮一红(测试自身缺陷,非生产 bug)

`test_query_bedInRoom` 首版把床与药罐放在 **(1,0,1) / (1,0,2)**(沿 z 相邻)。
连通判据 = `ABS(dx)+ABS(dy)==1`(**z 不参与**,GDD 规则一)⇒ 两个独立簇
⇒ `ROOM_NONE`(EC-24-07)⇒ 恒「无床」。改为沿 x 相邻 `(1,0,1)/(2,0,1)` 后转绿。

---

## 4. 证伪轮(6/6 各转红,SHA-256 逐字节还原)

同命令内「改 → 跑 → 还原」:

| 变异 | 内容 | 转红 |
|---|---|---|
| **M1** | 键序 `y` 打头 → `z` 打头 | `keyOrder` + `selectionIndependentOfInputOrder` |
| **M2** | 去掉 min(取首个命中 ⇒ 依赖遍历序) | `selectionIndependentOfInputOrder` |
| **M3** | 候选集判据放宽(`furnType != null` 即算床) | 3 条(`noBed` / `noCandidate` / `nonBedFurnitureExcluded`) |
| **M4** | 无候选时填兜底 `new WorldPos(0,0,0)` | 2 条(`noBed` / `noCandidate`) |
| **M5** | 查询面内联坐标比较(第二真源) | `hasNoInlineOrderingLogic` |
| **M6** | 停用二次注入守卫 | `attachClinicCell_injectsOnce_secondThrows` |

⚠️ M5 首版变异脚本**写坏了**(编译失败 ⇒ NO-XML)。重做后精确转红。
如实登记:那一次「NO-XML」不是绿,是**没跑成**。

还原哈希(与变异前逐字节相同):

```
b955e54b919dcc5ddc97df932933ba2c24dbf7fc6f8f8e7691c217e81bdc4428  ClinicReviveCellRule.cs
be51090e9f7524349a4fc39f1aaf349384ae7131e4cce214060f5e31f3402469  ClinicReviveCellQuery.cs
9ee568c1d5ced9169bffb6d7b668f080051221015c2ecaca363ed993e7dd24a3  RespawnCommandIssuer.cs
```

---

## 5. ⚠️ 全量回归抓到的 **flaky**(本批最大的意外)

### 5.1 症状

`PlayMode` 全量:**run1 绿 / run2 红 / run3 绿** ——
`test_deathToRevive_...` 报 `AC-20-10 AudioListener 单挂点:active 2 个 / 挂载总数 2 个`。
**定向单跑恒绿**(每次基数都是 1)。

### 5.2 四步根因链(每步实测)

1. `camera_follow_wiring_test` 的 TearDown 写
   `var preset = GameObject.Find("Main Camera"); if (preset != null) preset.SetActive(true);`
   —— `GameObject.Find` **只查 active**,而该预摆已被启动序 `SetActive(false)`
   ⇒ `Find` 返回 **null** ⇒ 「恢复激活」那句**恒不执行** ⇒ 预摆(带 `AudioListener`)**留场**。
2. `death_transition_playmode_test` 自造同名 `Main Camera`(带 listener)。
3. `BootRoot.FindInactiveInclusive("Main Camera")` 用
   `FindObjectsByType<Camera>(Include, SortMode.None)` 取**第一个**同名相机摘 listener。
4. **`SortMode.None` 的枚举序不保证稳定** ⇒ 有时先返回**残留旧件**(摘错 ⇒ 2 个 ⇒ 抛),
   有时先返回**本次新建的**(摘对 ⇒ 绿)。⇒ **偶发**。

### 5.3 修法(两半:治标 + 立门)

**治标**(4 个测试文件):
- `camera_follow_wiring_test` / `composition_root_smoke_test` / `save_slot_save_wiring_test` /
  `SkeuomorphicUI/ui_input_module_wiring_test` —— TearDown 改用
  `DestroyAllNamed`(`Resources.FindObjectsOfTypeAll` **含 inactive** + `go.scene.IsValid()` 排除资产)。
- `ui_input_module_wiring_test` 的「**恢复激活、不销毁**」改判为**销毁** —— 其原文理由是
  「后续测试依赖这台泄漏相机过 listener 门」,**该前提是错的**(后续测试各自造预摆);
  残留件的真实作用只有「让 `FindInactiveInclusive` 选错」。原文保留不删。
- `death_transition_playmode_test` 清场加「拆净一切同名 `Main Camera`」。

**立门**(`PlayModeTeardownFindGate`):
扫 `Assets/Tests/PlayMode/**/*.cs`,凡 `[UnityTearDown]` 的 `TearDown` **方法体**内出现
`GameObject.Find(` ⇒ 红。**只扫 TearDown 体**(断言里用 `Find` 是合法的 —— 那条测的就是「激活」语义)。
**不设白名单**(正确写法在任何清场场景都成立)。

### 5.4 门的两处自纠

| 问题 | 处置 |
|---|---|
| 路径拼接错(`.AssetsTestsPlayMode…` 缺分隔符) | `projectRoot.TrimEnd('.')` 在 `"."` 时得空串 ⇒ 改 `Path.GetRelativePath` |
| 正则被转义吃掉(`/*.*?*/` → Nested quantifier) | 改 `@"/*.*?*/"` 为 `@"\*.*?\*/"`(带反斜杠) |

### 5.5 门证伪(注入违规 ⇒ 转红,哈希逐字节还原)

把 `composition_root_smoke_test` 的 `DestroyAllNamed("Player")` 改回 `GameObject.Find` ⇒
`[pm-teardown-find] Assets/Tests/PlayMode/composition_root_smoke_test.cs:50 …` 转红。
还原后 `4293b811a20a314a1bbfb6b8dadfdbe12286980223d6c3796784fa928e31033d` 逐字节相同。

### 5.6 门链 manifest 的两处登记(全量回归抓到的)

新增门类**必须同批**登记,否则 `GateWiringManifestTest` 两条红:
- `ClassConsumerManifest` 补 `PlayModeTeardownFindGate`(Kind = Chain,Consumer = RunAll)
- `EntryWiringManifest` 的 `RunAll` 组补 `PlayModeTeardownFindGate.RunAll`

> ⚠️ 这两条**只有全量回归才暴露** —— 又一次「定向跑 ≠ 全量跑」。

---

## 6. 回归(三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | 2026-11-01 **21:22:06 CST**(EditMode)/ **21:24:26 CST**(PlayMode 第三轮) |
| **批次** | S24 床格端口(规则六)+ flaky 根治 |
| **commit** | `166f7d76` + 工作树 |

| 套件 | 结果 | 前次(本日 §7) | 等式 |
|---|---|---|---|
| EditMode | `total=6455 passed=6418 failed=0 skipped=37` | `6443 / 6406 / 0 / 37` | `6455 − 6443 = 12` ✅(新增 12 测) |
| PlayMode | `total=274 passed=270 failed=0 skipped=2`(**连跑三轮全同**) | `274 / 270 / 0 / 2` | 持平 ✅(无新增) |

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/em-final2.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
for f in 1 2 3; do python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/pm-fin$f.xml').getroot();print('run$f',r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"; done
```

---

## 7. 仍未做(不借绿)

| 项 | 归属 | 状态 |
|---|---|---|
| **② 相机两段式**(Φ1→Φ2 下沉 + 微倾) | 2 的**数值轮**(机位曲线) | **NOT-RUN**,本批未动 |
| **③ 远端变体**(局部水墨标识) | 45(**P1b 未开始**) | **NOT-RUN**;`IPositionalChannel` 亦无生产实现 |
| `TD-clinic-18` 的**文档半** | 24 GDD 规则六(已于 2026-10-30 补节) | 文档已落;**实现半由本批结清** |
| AC-29-11 / AC-29-12(`[L]` 节奏走查 / 听测) | 用户桌面批 | **NOT-RUN**(承 §7.8 口径不变) |

> ⚠️ `RespawnCommandIssuer` 的 `actorId` 在 P0 单机下**与床格无关**(医馆是全局布局)——
> 本实现对它只做非负校验,不逐玩家分叉。逐玩家分册是 45 的会话面(P1b)。
> 契约形状保留该参数,删了会破 45 的扩展点。
