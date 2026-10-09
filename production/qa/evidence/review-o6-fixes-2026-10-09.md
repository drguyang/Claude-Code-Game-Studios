# 评审原件 — O-6 观察项修复轮 · 2026-10-09

> **对象**:O-6(`ActorCellEntered` 两写者绕过 `IPayloadEncoder` + b6 门只扫 `Sim/`)
> **流程**:改码 → unity CLI 测试 → 双代理评审(恰一轮)→ 修复 → 复跑绿 → 6 发判别力变异 → 本原件
> **前置原件**:`production/qa/evidence/review-o4o5-fixes-2026-10-09.md`(O-6 由其 §四 F2 登记)

---

## 〇、原判定(O-6 登记原文)

`production/qa/evidence/review-o4o5-fixes-2026-10-09.md:25`(O-4/O-5 轮代码面 F2):

> **O-4 漏网:`ActorCellEntered` 两写者绕过 encoder**,坐标手搓 `PayloadRef(cell.X,cell.Y,cell.Z)`
> 伪引用(无 actor 身份)⇒ 联机两 actor 同 tick 同格仍坍缩。根因先于本轮(registry 已定义
> `ActorCellEnteredPayload` 未被用;**b6 门只扫 `Sim/`**)。**归:encoder 接线轮 + b6 门扩扫**

**根因链**:两写者把格坐标塞进 `PayloadRef` 的引用三字段(零字节进池、无 actor 身份)⇒
O-4 修复后的条件键(`Seq < 0` 补 `BlobId/Offset/Length`)在两 actor 同 tick 同格时
**PayloadRef 同值** ⇒ 第二条被 `EventStream` 去重吞掉,联机坍缩为 1 条。

---

## 一、原判定(双代理评审 · 恰一轮 · 2026-10-09)

### 代码面(`lead-programmer`)**APPROVE**(0 BLOCKING + 9 ADVISORY)

核过零问题 12 项:asmdef 边界合法性(只见接口不见实现体)· b6 扫描体等价性 ·
注释误报面双专测 · 全库 `new PayloadRef(` 零残留(除 `Sim.Codec` 唯一合法) ·
行为等价(单 actor 正常路径未被动) · fail-closed(encoder null / actorId<0) ·
客户端零副作用(`Client` 首行 return 在编码之前) · O-4 兑现且无第二坍缩点(真 `EventStream`
断 `Count==2`) · 字节真进池(`TryGetBlob`/`Length`/`Offset` 三断言) · registry/codec 链路齐 ·
无越权裁决 · 测试标准。

| # | 级 | 位置 | 缺陷 | 处置 |
|---|---|---|---|---|
| 代码-1 | ADVISORY | `AssemblyGates.cs:12` / `:209` | 文件头仍写「b6 = Sim/ 内…」,扫描面已含表现层 ⇒ 与 ADR-029 §③ 原文漂移 | **本轮修**(门头 O-6 扩面注);ADR-029 §③ 回写登记 |
| 代码-2 | ADVISORY | `AssemblyGates.cs:234-238` | 扫描面未覆盖 `Gameplay.UI` / `Gameplay.Input` / `Tests` | 登记不修(当前三处全库零命中) |
| 代码-3 | ADVISORY | `AssemblyGates.cs:294-299` | 块注释剥除不保换行 ⇒ 行号下偏;`//` 剥字符串字面量 | 登记不修(豁免表空,休眠) |
| 代码-4 | ADVISORY | `AssemblyGates.cs:268` | `EndsWith` 豁免匹配偏松 | 登记不修(豁免表 `Array.Empty`,休眠) |
| 代码-5 | ADVISORY | `IPayloadEncoder.cs:26` ↔ `EventStream.cs:79-87` | 契约不保证同载荷同 BlobId ⇒ **重编码路径**得新 BlobId ⇒ 重复入流 | 登记归 45 轮(承 O-4 F1) |
| 代码-6 | ADVISORY | 组合层(全库 grep) | 非 Tests 代码零 `Initialize`/`new CellTransitionDetector` 命中 ⇒ **修复运行期不可达** | 登记归接线轮 |
| 代码-7 | ADVISORY | `PlayerController.cs:219` + `CellTransitionDetector.cs:96` | 同 actor 双写者同时挂 ⇒ 修复后两条(原伪引用坍缩为 1) | 登记归接线轮(二选一) |
| 代码-8 | ADVISORY | b6 测试 ↔ O6 测试 | 扫描面判据双写 | **本轮消解**(O6 版改断言不同维度:恰两面 + 去重) |
| 代码-9 | ADVISORY | `AssemblyGates.cs:278-285` | `<summary>` 重复两遍(既有) | 登记不修 |

### 测试面(`qa-lead`)**FIX-THEN-APPROVE**(1 BLOCKING + 9 ADVISORY)

| # | 级 | 缺陷 | 逃逸变异 | 处置 |
|---|---|---|---|---|
| 测-1 | **BLOCKING** | 门「生效链路」零断言:无测试证明入口把 `PayloadRefScanDirs` 传给扫描体;无 `Directory.Exists` 存在性断言(`continue` 空转不可区分零违例 vs 零扫描) | **C′**:入口改硬编码 `{"Assets/Sim"}` 或空实现 ⇒ 既有三条全绿,O-6 原病静默复发 | **本轮修**(补 `test_b6_entryUsesScanDirs_andDirsExist`) |
| 测-2 | ADVISORY | 探针平铺临时目录根,扫描深度无断言 | `TopDirectoryOnly` ⇒ 全绿 | **本轮修**(探针改 `tmp/Sub/Bad.cs`) |
| 测-3 | ADVISORY | 「两 actor 同格」只在 detector 写者验证,controller 侧缺对偶 | controller 侧坍缩无测试红 | **本轮修**(补 `test_o6_twoControllersSameTickSameCell_bothKeptInStream`) |
| 测-4 | ADVISORY | 组合层零接线 ⇒ 修复运行期不可达(与代码-6 同源) | 接线层写死 actorId=0 今日全绿 | 登记归接线轮 |
| 测-5 | ADVISORY | `Assert.AreEqual(0, _pool.Count)` client 过约束,将来 F1 聚合上行会假红 | — | **本轮修**(出口条件注释,`:175`) |
| 测-6 | ADVISORY | `_actorId` 白名单按名豁免,改自增计数器不报 | `_actorId++` 全绿 | **本轮修**(`IsInitOnly` 结构断言,`cell_transition_test.cs:434`) |
| 测-7 | ADVISORY | 扫描面判据双写 | — | **本轮消解**(同代码-8) |
| 测-8 | ADVISORY | O6 探针测碰文件系统,字面违反 test-standards「unit 不依赖 filesystem」 | — | 登记**豁免型**(既有 b6 测试同型;`try/finally` 自建自删) |
| 测-9 | ADVISORY(既有) | `occupancy_overlay_test.cs:37,45,126-139` **伪引用自洽环**:测试自造 `PayloadRef(blobId: structureId,…)` + 自己按 `BlobId` 读回,只测夹具不测生产(O-6 同型) | 生产编码路径坏该文件照绿 | **登记新观察项 O-7**(见 §二之二) |
| 测-10 | ADVISORY(S4) | `AssemblyGates.cs:278-283` `<summary>` 重复(同代码-9,既有) | — | 登记不修 |

测试面另附:变异 A/B/D/E 逐条判定**全不逃逸**;C 题面不逃逸但引出 C′(→ 测-1 BLOCKING);
计数自洽三项独立复核(过滤 77 夹具求和 / 全量 +8 fullname 集合差 / PlayMode 98);
假绿扫描整体干净(值断言非存在性断言、负向探针真走生产方法、真 `EventStream`+真池)。

---

## 二、修复落点(已实施)

### O-6 主体(评审前 · 3 生产文件)

**1. `unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs`**
- 构造签名扩为 `(IEventSink, ITickProvider, IPayloadEncoder encoder, int actorId)`;
  `encoder == null` → `ArgumentNullException`(`:48`);
  `actorId < 0` → `ArgumentOutOfRangeException`(`:49-51`,ADR-006 Amendment B 计数器 id 空间)。
- `OnTickEdge`(`:97-109`):`new ActorCellEnteredPayload(_actorId, cell, tick)` →
  `_encoder.Encode(EventKind.ActorCellEntered, payload)` —— 载荷编码唯一路径 = `IPayloadEncoder`
  (ADR-029 §③)。
- 头注(`:16`)登记失效模式(伪引用三字段)。

**2. `unity/Assets/Gameplay.Presentation/Player/PlayerController.cs`**
- `Initialize(mode, sink, tickProvider, IPayloadEncoder encoder, int actorId)`(`:65-66`),
  同两参同校验(`:72`);字段 `_encoder` / `_actorId`(`:51`)。
- `AppendCellEnteredEvent`(`:227-233`)同走 encoder。
- `Client` 模式首行 return 在编码之前 ⇒ 零 Append、零入池(专测钉住)。

**3. `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`(b6 门扩扫 + 可测化)**
- `PayloadRefScanDirs = { "Assets/Sim", "Assets/Gameplay.Presentation" }`(`:238-243`,
  `internal static readonly` 提为字段供反射断言)。
- 入口 `CheckPayloadRefCallsites(errs)` 纯委托 → 扫描体
  `CheckPayloadRefCallsitesIn(errs, string[] dirs)`(`:244-251`)—— 目录形参化,
  测试可注入**工程外临时目录**做行为级负向验证(不污染工程)。
- 门头(`:206-235`)补 2026-10-09 O-6 扩面注(代码-1 当轮兑现)。

**为何这样修(设计论证)**
- **asmdef 边界**:`Gameplay.Presentation` 引用集 = {`Sim.Contracts`, `Sim`, Addressables,
  ResourceManager, UnityEngine} —— **不引 `Sim.Codec`** ⇒ 写者只能持接口
  `IPayloadEncoder`(住 `Sim.Contracts`),实现体 `PayloadEncoder` 不进表现层字段
  (`motor_lease_test` 白名单 + 注释钉住)。符合 ADR-025 §①。
- **坍缩为何消失**:每次 `Encode → IBlobSink.Store` 发**新 BlobId** ⇒ 两 actor 同 tick 同格
  两条事件的 `PayloadRef` 必不同 ⇒ O-4 条件键(`BlobId/Offset/Length` 身份)可区分 ⇒ 双条入流。
- **门为何可测**:扫描体目录形参化后,负向探针建在 `Path.GetTempPath()` 下、`finally` 自删,
  既能验递归深度(`Sub/`)、又能验入口接线(改写 `dirs[0]` 走入口)、又能验剥注释
  (`Doc.cs` 仅注释)—— 三个逃逸面各一条可证伪断言。

### 评审后修复(恰 6 项 + 1 项双写消解)

| # | 对应发现 | 落点 |
|---|---|---|
| 1 | 测-1 **BLOCKING** | `assembly_gate_b6_test.cs:97-145` `test_b6_entryUsesScanDirs_andDirsExist`:(a) 每项 `Directory.Exists`(反空转,报错带 cwd);(b) `dirs[0]` 覆盖为临时**子目录探针** → 走**入口** `GateMethod.Invoke` → 断言恰 1 条 + `StringAssert.Contains("Bad.cs")` → `finally` 逐元素还原 snapshot |
| 2 | 测-2 | 探针改写 `tmp/Sub/Bad.cs`(`assembly_gate_b6_test.cs:121-128` + `o6_actor_cell_payload_test.cs` `Sub/Bad.cs`/`Sub/Doc.cs`)—— 兼验 `SearchOption.AllDirectories` |
| 3 | 测-3 | `o6_actor_cell_payload_test.cs:88` `test_o6_twoControllersSameTickSameCell_bothKeptInStream`(controller 支对偶,真 `EventStream` `Count==2` + 解码出 ActorId 1/2) |
| 4 | 测-6 | `cell_transition_test.cs:434` `_actorId.IsInitOnly` 结构断言(防同名改自增) |
| 5 | 测-5 | `o6_actor_cell_payload_test.cs:175` client 池断言出口条件注(守「`OnTickEdge` 不泄漏编码」;F1 聚合上行须换判据) |
| 6 | 代码-1 | `AssemblyGates.cs:206-235` 门头 O-6 扩面注(ADR-029 §③ 原文只写 `Sim/`,现面 + 表现层) |
| 7 | 代码-8 / 测-7 | `o6_actor_cell_payload_test.cs:240` `test_b6_scanDirsExactlyTwoFaces`:改断言**不同维度** —— 恰两面(`Length==2`)+ `Distinct()` 去重 + `Contains` 两目录,消解与 `test_b6_scanDirsCoverPresentation` 的判据双写 |

### 既有测试订正(7 文件,评审前)

`cell_transition_test.cs`(SetUp 建真池/编码器 + 13 处构造改 helper + 4 处伪 ref 断言改解码 +
`allowedIntFields` 增 `_actorId`)· `host_authority_test.cs`(1 构造 + 3 `Initialize` 补参)·
`stream_bound_test.cs`(2 构造补参)· `chunk_activation_test.cs`(构造建池 + 格还原改解码,
断言字节真进池)· `motor_lease_test.cs`(`AllowedFieldTypes` 加 `IPayloadEncoder`)·
`assembly_gate_b6_test.cs`(入口接线测试新增;`test_b6_currentSimSourcePasses` 改名
`test_b6_currentScanSourcePasses` + 新增 `test_b6_scanDirsCoverPresentation`)·
`o6_actor_cell_payload_test.cs`(**新建**,8 条 + `.meta` guid `5196a0835c9d4896830915f55f2e7324`)。

---

## 二之二、登记不修 + 新观察项

**登记不修(承双评审 ADVISORY,共 11 项)**:
代码-2(扫描面未含 `Gameplay.UI` 等)· 代码-3(块注释行号下偏)· 代码-4(`EndsWith` 偏松)·
代码-5(重编码重发去重 → 45 轮,承 O-4 F1)· 代码-6(组合层零接线)· 代码-7(同 actor 双写者
二选一 → 接线轮)· 代码-9 / 测-10(重复 `<summary>`,既有)· 测-4(组合层端到端 → 接线轮,
与 O-5 同族)· 测-8(文件系统探针 = **豁免型**,既有 b6 先例,非新引入)。
另承测试面覆盖缺口清单 #4(多 actor × 多 tick 真流有界性)与 #6(存档/网络 blob 重映射
round-trip)→ 分别归接线轮 / 7a·45 轮。

**⚠️ 新观察项 O-7(本轮登记,归下轮)**:
> `unity/Assets/Tests/EditMode/ModularBuilding/occupancy_overlay_test.cs:37,45,126-139` ——
> 测试**自造** `new PayloadRef(blobId: structureId, offset: …)` 事件 + 自己按 `Payload.BlobId`
> 读回。生产 `StructureKinds` 已改走 `IPayloadEncoder`(`structure_kinds_test.cs:59,129`
> 订正注),本文件仍是 O-6 同型的**伪引用自洽环**(只测夹具不测生产)——
> 生产编码路径若坏,该文件照绿。**归:O-7 修复轮(测试改走真 `PayloadCodec` 解码)。**

---

## 三、验证命令(可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# 1) 过滤 run(8 夹具;须带工程路径,漏工程会 108% CPU 死锁)
unity test unity --mode EditMode \
  --filter "O6ActorCellPayloadTest|CellTransitionTest|HostAuthorityTest|StreamBoundTest|AssemblyGateB6Test|ChunkActivationTest|MotorLeaseTest|EventStreamTest" \
  --output unity/Logs/editmode_o6_fix2.xml

# 2) 全量 EditMode + PlayMode(终态)
unity test unity --mode EditMode --output unity/Logs/editmode_full_20261009_o6final.xml
unity test unity --mode PlayMode  --output unity/Logs/playmode_full_20261009_o6final.xml

# 3) 判别力变异(6 发;python 反向注入/恢复,禁 git checkout --)
bash /tmp/o6_mut_loop.sh                 # apply→过滤测→记红→restore,逐发
# 单发等价:python3 /tmp/o6_mutations.py apply A → unity test … → restore A

# 4) 净态核验
sha256sum -c /tmp/o6_prod.sha256         # 三生产文件 = 评审后终态哈希
grep -n "MUT-" unity/Assets/Gameplay.Presentation/Player/{CellTransitionDetector,PlayerController}.cs \
                unity/Assets/Editor.Tools.Gates/AssemblyGates.cs   # 期望 0 行
```

**XML 解析规则**:统计根 `type="TestSuite" name="unity"` 节点的
`total/passed/failed/skipped`(`test-case` 按 `result` 字段;无 `executed` 属性)。
⚠️ `unity test` 全量 EditMode 慢性返回 wrapper「Unity 进程以代码 2 退出 / `EM_EXIT=8`」
(历史 508 次,环境怪癖)—— **XML 为判据**,不作失败处理。

---

## 四、判别力变异(6 发 · 全部由 `/tmp/o6_mutations.py` python 反向注入/恢复)

过滤集同 §三-1(79 条);锚点 `count==1` 断言;逐发 restore 后残留 0 + 哈希净。

| 发 | 变异(注入) | 实测红 | 判定 |
|---|---|---|---|
| **A** | 两写者退回 `new PayloadRef(cell.X, cell.Y, cell.Z)`(两文件各一锚点) | **11 红**:`test_b6_currentScanSourcePasses` · `test_b6_entryUsesScanDirs_andDirsExist` · `test_o6_payloadRoundTrip_actorCellTick` · `test_o6_playerController_payloadRoundTrip` · `test_o6_twoActorsSameTickSameCell_bothKeptInStream` · `test_o6_twoControllersSameTickSameCell_bothKeptInStream` · `test_ac103_diagonalCrossing_committedCellHasCorrectZ` · `test_ac103_mergeOperator_samplesReachesLast` · `test_ac113_diagonalCrossing_committedCellHasCorrectZ` · `test_ac115_teleportOnlyDestination` · `test_ac623_discoverGate_drivenByRealCellEntry` | **不逃逸(双层)**:b6 真源扫描 + 行为断言(真流 `Count==2` / 解码值)各自独立红 |
| **B** | detector 载荷 `actor_id` 写常量 0 | **2 红**:`test_o6_payloadRoundTrip_actorCellTick`(ActorId==7)· `test_o6_twoActorsSameTickSameCell_bothKeptInStream`(1/2 值断言) | **不逃逸**(值断言,缺省 0 兜不住) |
| **C** | `PayloadRefScanDirs` 删 `"Assets/Gameplay.Presentation"` | **2 红**:`test_b6_scanDirsCoverPresentation` · `test_o6_b6Gate_scanDirsExactlyTwoFaces` | **不逃逸**(双保险:旧面 Contains + 新面恰两面) |
| **D** | detector 删 `actorId<0` fail-closed | **1 红**:`test_o6_negativeActorId_rejected` | **不逃逸** |
| **E** | b6 扫描体不剥注释(直读原文) | **3 红**:`test_b6_currentScanSourcePasses`(生产注释含 token)· `test_o6_b6Gate_catchesHandRolledRef`(`Doc.cs` 注释面)· `test_b6_entryUsesScanDirs_andDirsExist`(探针 `errs` 计数变 2 ≠ 1) | **不逃逸(三重)** |
| **F** | **入口改硬编码 `new[]{"Assets/Sim"}`(不再读数组)** —— 测-1 BLOCKING 修复的判别力证明(评审题面 C′) | **1 红**:`test_b6_entryUsesScanDirs_andDirsExist` | **BLOCKING 已闭**:原逃逸路径(数组断言绿 + 扫描体探针不经入口 + IsEmpty 对"只扫 Sim"同真)现被入口接线负向断言封死 |

**逐发恢复核验**:每发 restore 后脚本内嵌断言(锚点反向 `count==1` + 目标文件 `MUT-` 残留 = 0)
全部通过;6 发跑完 `sha256sum -c /tmp/o6_prod.sha256` 三文件全「成功」。
⚠️ 残留检查收窄到本轮回写目标文件 —— 全仓 `unity/Assets` 另有 9 处历史变异注释
(`PatientCueSchedule` / `PatientSpatialDirector` 等),非本轮标记,不计入。

---

## 五、复跑实数(终态)

| run | total | passed | failed | skipped | inc | 说明 |
|---|---|---|---|---|---|---|
| 过滤 fix1(8 夹具) | 77 | 76 | **0** | 1 | — | 评审前;O6 专测 7 条 |
| 过滤 fix2(8 夹具) | 79 | 78 | **0** | 1 | — | 评审后 +BLOCKING 入口测 +controller 对偶 |
| 过滤 postrun(6 发变异全恢复后净态) | 79 | 78 | **0** | 1 | — | `editmode_o6_postrun.xml` 实读;与 fix2 同数,零红 |
| 全量 EditMode(评审前) | 3057 | 3010 | **0** | 46 | 1 | 基线(O-4/O-5 终态)3049/3002 ⇒ +8 = O6 7 条 + b6 新增断言 1(净,含 1 改名) |
| **全量 EditMode(终态 o6final)** | **3059** | **3012** | **0** | 46 | 1 | fix1 +2 = BLOCKING 入口测 + controller 对偶;相对基线 **+10** |
| **全量 PlayMode(终态)** | **98** | **98** | **0** | 0 | — | 与基线持平,零回归 |

唯一 skip = `CellTransitionTest.test_ac104_vrZeroEvents`(既有 `Assert.Ignore` VR 项);
恒 inc = `SettingsExposureTest.test_monoOption_existsWithValidDefault`(既有,与本轮无关)。

**未跑/未核声明**:PlayMode 逐名清单未逐条核(仅总数与零红,本轮测试全 EditMode);
Boot 场景 prefab/unity 序列化侧未翻开(静态 grep 零 `Initialize` 调用点,见代码-6)。

---

## 六、判定链

**双代理恰一轮评审**(代码面 APPROVE 0B+9A · 测试面 FIX-THEN-APPROVE 1B+9A)→
**评审后 6 项修复 + 1 项双写消解全落**(BLOCKING = 入口接线/目录存在性测试)→
**复跑绿**(过滤 79/78 · 全量 EditMode 3059/3012/0 红 · PlayMode 98/98)→
**6 发判别力变异全中**(A 11 红 · B 2 · C 2 · D 1 · E 3 · F 1;
含题面 C′「入口硬编码」的正面闭合)→
**净态核验**(锚点反向恢复 · 残留 0 · 三生产文件哈希匹配 · 净态过滤复跑绿)→
**APPROVE**(两面必修项全部闭环,O-6 结案;登记 11 项不修 + 新观察项 **O-7**)。
