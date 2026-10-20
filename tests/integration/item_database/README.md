# integration/item_database/

按系统分目录的集成测试(命名 `[system]_[feature]_test.cs`)。集成类故事的 BLOCKING
证据落点(coding-standards §Testing Evidence by Story Type)。

## Story 003(配方结算唯一求解器 —— AC-21a-5 / 6)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/integration/item_database/recipe_settlement_solver_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**。承 `tests/integration/README.md`
「Unity 侧实际编译进 PlayModeTests.asmdef(待 ADR #2)」—— 该装配名已由 **ADR-025 §⑤**
钉为 **`Gameplay.Tests`**,故实际编译落点 =

**`unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs` |
| 装配 | `unity/Assets/Tests/PlayMode/PlayMode.asmdef`(name = `Gameplay.Tests`) |

**装配引用增补(2026-09-24)**:PlayMode 装配的 `references` 原仅
`Unity.Addressables` / `Unity.ResourceManager`,**不可见 `Sim` / `Sim.Contracts`** ⇒
AC-21a-5/6 的断言无从编译。已增两条 GUID:
`f1e625c719f6812dcae9009189e8b2c0`(`Sim.Contracts`)· `49b36e3ee94a392fe97bd7fe76bb27fb`(`Sim`)。
该增补属 **ADR-025 §⑤「测试装配族」**范围,**不动 §① 装配清单**(§④ 封闭性不受影响)。

> ⚠️ **订正(同日)**:本条初稿曾把两 GUID 标反(把 `f1e6…` 标为 `Sim`、把 `Sim.Codec` 的
> `889729…` 标为 `Sim.Contracts`)。`Sim.Codec` **已从引用集移除** —— 本测试仅在**字符串**
> 白名单里出现 `"Sim.Codec/PayloadCodec.World.cs"` 路径,未引用其任何类型 ⇒ 不需要该装配。
> 现引用集恰 = {`Sim.Contracts`,`Sim`},与测试 `using DaYiJingCheng.Sim;` +
> `using DaYiJingCheng.Sim.Contracts;` 对位。

**为何 AC-21a-5/6 是集成级**:两者判的不是某个纯函数的值,而是**跨系统结构事实** ——
① 18 炮制 / 19 制作两条入口是否收敛到 21 的**同一个** `MethodInfo`;
② 17/18/19 的源码里是否**不存在**第二份结算公式体。故按 GDD §Acceptance Criteria 图例
落 **[I] 级**。

**18 / 19 程序集尚未落地**:故本文件对
`Sim/ItemDatabase/RecipeSettlementEntryPoints.cs` 的两个入口接缝
(`ProcessingSettlementEntry` / `CraftingSettlementEntry`)断言 —— 它们是 18/19
落地后**必须**收敛到的同一 `MethodInfo`。18/19 出现后本文件**不改判据**
(仅扫描面自动纳入其源文件)。

**AC-21a-6 扫描口径**(承 Story 002 `schema_types_primary_key_test.cs` 的 AC-48 先例):
- 扫描面 = `unity/Assets/**` 的 `*.cs`,**排除 `/Assets/Tests/`**(测试文件自身含断言字符串,
  不排除即自我扫红);
- **注释剥离**(文档性提及不算「当公式体用」);
- 白名单**逐条带理由,共 3 个文件**:
  ① `Sim/ItemDatabase/RecipeSettlementSolver.cs`(唯一求解器 = 公式体的唯一合法落点);
  ② `Sim.Contracts/Payloads/WorldPayloads.cs` 与 ③ `Sim.Codec/PayloadCodec.World.cs`
  —— `Craft` 世界流事件的**载荷字段名**(registry schema,ADR-024)与其编码器,
  出现 `ActualConsumed` / `OutputQuality` 是**数据形状**,不是公式体
  (先例:Story 002 的 AC-21a-48 白名单已把 `ActualConsumed` 登记为 registry schema 字段)。
  **入口接缝文件不在白名单**(它按 18/19 侧对待);
- **大小写不敏感**裸标识符匹配(`OrdinalIgnoreCase` —— 命名变体 `qtyMultiplier` /
  `outputQuality` 同命中;这是同义词表的第一层,不另设登记处);
- 标识符表 9 项,全为**合法 C# 代码文本**(非 GDD 记号):`QtyMultiplier` / `SumOfModifiers` /
  `OutputQuality` / `ActualConsumed` / `Retain(` / `QualityMod` / `EnvModTotal` / `Efficiency` /
  `CeilDiv`。初稿曾收 `EnvMod_total` / `max(1,` —— 二者**不是可出现的源码形态 = 死项**,
  永不可命中、只给扫描面制造虚假宽度,已删;
- 17 的 F3(`gather_profile` / `quality_distribution` / `qty_per_node`)是 17 拥有的动作
  (GDD 明文「不归 F1 的唯一求解器」)⇒ 与 F1 公式体标识符**结构不相交**(测试内断言自证),防误报;
- **容忍 17/18/19 目录缺失**(当前不存在 ⇒ 只扫存在的树);
- **自证非伪证**:`test_formulaBodyScanner_selfProvesItCanDetectDuplication` 调用
  **与真扫描同一**的匹配谓词 `MatchIdentifiers(...)`(真扫描 `ScanForDuplicates` 也走它)——
  否则禁用真扫描器后自证仍绿 = 伪证。

**注释剥离的边界**:剥离只处理 `//` 与 `/* */`;**字符串字面量内的标识符不剥**
(白名单里 `"Sim.Codec/PayloadCodec.World.cs"` 是**路径字符串**,故 `PayloadCodec` 装配
无需被引用 —— 见上「装配引用增补」订正)。

---

## Story 008(数据管线烘焙与 Addressables 预载 —— AC-21a-26 / 47 / 48)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/integration/item_database/data_pipeline_bake_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**(承 Story 001–007 同一先例)。真身 =

**`unity/Assets/Tests/EditMode/ItemDatabase/data_pipeline_bake_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/data_pipeline_bake_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(增引 `Editor.Tools.Bake` 平名引用) |
| 负向夹具(新增) | `tests/unit/item_database/fixtures/invalid_state_int.json` |
| AC-47 冒烟(ADVISORY) | `production/qa/smoke-2026-09-24.md` |

**测试计数**:**25 [Test] + 1 TestCaseSource × 33 夹具 = 58 个用例**
(**2026-10-20 实测**;`fixtures/*.json` = 34,减 1 跳过 `invalid_quality_dist.json` = 33)。

> ⚠️ **本行曾记「22 [Test] + 1 TestCaseSource × 30 夹具 = 52」**(2026-09-24)。
> 现值的差额分两部分,**来源不同,不得混记**:
> - **+3 [Test] = 产物漂移门**(`test_drift_*`,2026-10-20 本文件 §产物漂移门 所述);
> - **+3 夹具**(30 → 33)= **其他 Story 后续增量**,非本次改动 ——
>   该行自 2026-09-24 起未随夹具增长而刷新。
> 本表**只报实跑实测值**;历史 52 保留作对账参照。


**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode **407 全绿**;同批 PlayMode **12 全绿**。
AC-47 冒烟三数已回填 `production/qa/smoke-2026-09-24.md`;E-13 自检五跑收敛(反向经反射探针
第五跑通过,正向成功见证 = 首/二跑 items=4/recipes=2,真实基础设施故障下 [E-13] 包裹 =
第四/五跑 stale-bundle)—— ADR-014 §五 契约三点由跨跑聚合证据坐实,详见 smoke 收敛判读。

**两阶段烘焙接线**(ADR-014 §二/§三):阶段1 `JsonStage1Lexer`(`JsonTextReader` **仅词法**,
`DateParseHandling.None` 钉死)→ 阶段2 `ItemDatabaseBinder`(白名单 / `FixParse` / 类型 /
schema_version)→ `ItemDatabaseBaker.RunGates`(**28 条执法体**,Stories 004–007 全部接入)
→ `CookedWriter` 确定性 LE 编码。**绑定零错误才跑门**(防残缺记录制造噪声);
**全案唯一 throw 点 = `BakeValidationException`**(门本体维持零 throw —— 承 Story 004–007 纪律,
Story 008 = 聚合抛出方)。

**AC-21a-26 扫描面**:① 绑定层 —— `processing_state` 落 Integer/Float token ⇒ 拒收(引
`AC-21a-26 · D-21-13`);② 产物层 —— `StateEncodingScanner.ScanAssetProducts` 递归扫
`*.asset`(YAML `processing_state: 3` 亦命中;跳过 `/Tests/`),每次烘焙菜单执行前先跑。

**AC-21a-48 结构守卫**:`RuntimeSourceGuard` 扫运行期 `*.cs`,违例 token = `JsonConvert` /
`JsonTextReader` / `JObject.Parse` / `FixParse.Parse` / 字符串字面量 `assets/data`;
豁免 = `/Tests/` 段 · `Editor.*` 目录段 · `FixParse.cs` 定义自身。剥离注释后扫(**字符串
字面量保留** —— `assets/data` 直读必须命中);与 Story 002 旋钮扫描不重复报(分工:002 扫
调参 token,008 扫 JSON 解析器 / FixParse / 直读)。**Editor 侧 Newtonsoft 词法结构性豁免**
(`Editor.Tools.Bake` 不进玩家构建)。

**ConfigVersion**:FNV-1a 32(`ConfigVersionUtility`)覆盖 = 排序后的 (文件名, 字节) 对,
BCL-only(禁 `UnityEngine.Hash128` —— ADR-010/014 纪律);头部偏移 12(configVersion u32,
`CookedFormat` 布局);`CompareConfigVersion` 不匹配 ⇒ `Fatal=false`(ADR-010 §七),不可读 ⇒
`Fatal=true`。`ConfigVersion = 0`(字节全零)为**未初始化哨兵**,与「哈希恰为 0」区分。

**facet 形夹具的浅层拒**(已登记 ADVISORY ⑧):31 夹具中仅 4 个为完整 items 形
(`invalid_state_int` / `invalid_dup_key` / `invalid_enum` / `invalid_stored_stackable`);
其余为单门 facet 形,作为 items 源喂入时在绑定层白名单 / 缺 `items` 键处硬失败 —— 仍满足
TR-027「经本管线执行 ⇒ 硬失败」,深度浅于 Story 006/007 的单门直测。完整形的 4 个走其设计
路径。跳过夹具 `invalid_quality_dist.json` 的理由 + 可证伪断言见测试 `SkippedFixture` 注释
(`test_itemDatabase_qualityDistributionFixture_documentedAsSkipped` 断言「文件存在**且**被排除」)。

---

## Story 009(Craft 事件载荷与全序键 —— AC-21a-52)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/integration/item_database/craft_event_payload_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**(承 Story 001–008 同一先例)。真身 =

**`unity/Assets/Tests/EditMode/ItemDatabase/craft_event_payload_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/craft_event_payload_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(引用集已含 `Sim` / `Sim.Contracts` / `Sim.Codec`) |
| 新增生产件 | `unity/Assets/Sim.Contracts/EventOrderKey.cs` · `unity/Assets/Sim/EventOrder.cs` |

**五子条件 → 用例映射**:①② 静态/快照类型无 `ActualConsumed`(反射负向 + 正向对照防空断言)
= `test_ac21a52_actualConsumed_absentFromStaticAndSnapshotTypes`;③ `Ceil(基数/当次 EFF)`
独立重算 + EFF&lt;EFF_MAX / EFF=EFF_MAX 双边例 + 编解码往返 = `..._recomputeEquals_whenEffBelowMax` ·
`..._equalsBase_whenEffEqualsMax` · `..._fullEvent_roundTripPreservesPayloadAndOrderKey`;
④ 载荷字段集 = registry `payload_schema` 十位 + 整数域 = `..._craftPayloadFieldSet_matchesRegistrySchema`;
⑤ 全序键可排全序(跨流 + 哨兵 + 比较器公理 + 键四分量无 actor)= `..._orderKeyFieldSet_isHeaderOnlyNoActor` ·
`..._orderKey_sortsMixedStreams_totalOrder` · `..._orderKey_craftKey_deterministicAcrossCalls`。

**7a 往返子条件的承位口径(禁借绿)**:7a 文件级存档(校验和 / checkpoint / 迁移)未实现 ⇒
本文件以 `SimEventCodec` + `PayloadCodec` 字节往返承位(ADR-010 存档体的同一条字节路径);
真存档往返归 7a 落地后补跑,**Story 009 不宣称该子条件已绿**。

---

## Story 010(实例权威与持久化往返 —— AC-21a-31 / 34 / 35 / 53 / 58 / 63)—— 落点说明

故事头登记两条证据路径:集成账本
`tests/integration/item_database/instance_authority_persistence_test.cs` + 逻辑账本
`tests/unit/item_database/instance_authority_persistence_test.cs` + GDD 点名
`fix_codec_roundtrip.cs` / `id_authority.cs` —— 同 Story 001…009:**Unity 不编译
`unity/Assets/` 之外的代码** ⇒ 三条账本全部指向 EditMode 树真身
(**AC-31/35 的集成账本与 AC-34/58 的单元账本共用同一真身**):

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身,四账本合一) | `unity/Assets/Tests/EditMode/ItemDatabase/instance_authority_persistence_test.cs` |
| AC-53 真身(GDD 照录名) | `unity/Assets/Tests/EditMode/ItemDatabase/fix_codec_roundtrip.cs` |
| AC-63 真身(GDD 照录名) | `unity/Assets/Tests/EditMode/ItemDatabase/id_authority.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(`Sim.Contracts.Tests`) |
| 新增生产件 | `Sim/ItemDatabase/{IdAuthority,ContainerClosure,InstanceResolver}.cs` · `Sim.Codec/ItemInstanceCodec.cs` |
| 负向夹具(QA 指名) | `tests/unit/item_database/fixtures/invalid_container_closure.json` |

**7a 往返承位口径(禁借绿)**:文件级存档(头 + 校验和 + checkpoint)未实现 ⇒ 本故事
AC-31/35 的「7a 持久化往返」以 `ItemInstanceCodec` 字节级 encode→decode 承位
(ADR-010 存档快照段同一编码路径);真存档往返归 7a 落地后补跑,Story 010 不宣称该半边已绿。
详细 AC→用例映射见 `tests/unit/item_database/README.md` §Story 010。

---

## Story 011(跨平台确定性黄金夹具 —— AC-21a-29)—— 落点说明

故事 QA 为 AC-29 指名本账本 `tests/integration/item_database/determinism_golden_fixtures_test.cs`
+ CI 独立 IL2CPP job。**本账本文件仍不建**:Unity 不编译仓库根(承 Story 001–010 先例),
空账本 = 假证据面。**✅ AC-29 已于 2026-09-24 实测 VERIFIED,真身与证据落点如下:**

| 内容 | 路径 | 状态 |
|---|---|---|
| AC-29 双腿对拍真身(3 测:19 条金标准 + F7 回绕 + F7 峰值) | `unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs`(装配 `Gameplay.Tests`,增引 Sim.Codec GUID) | ✅ Mono 腿 3/3 + IL2CPP 腿 3/3 |
| AC-29 判决书(命令/后端标记/三方 diff/订正记录/残余表) | `production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md` | ✅ 19/19 逐位 |
| Mono 腿输出 | `production/qa/evidence/ac29-hashes-mono.txt` | ✅ `backend=Mono` |
| IL2CPP 腿输出 | `production/qa/evidence/ac29-hashes-il2cpp.txt` | ✅ `backend=IL2CPP` |
| 执行载体 | 编辑器 PlayMode(Mono,F4 恒 Mono)+ StandaloneLinux64 UTF player(独立 IL2CPP 构建,`-testPlatform`) | ✅ 各 3/3 |
| 后端切换器 | `unity/Assets/Editor.Tools.Spike/Ac29Il2CppSwitch.cs`(跑完归位 + `git restore`) | ✅ 已归位 |
| **CI 三格常驻矩阵 job** | CI `unity-builder@v4` 独立 job(ADR-012 §二) | **未建(BY-矩阵建设轮)** |
| **ARM64 交叉格 / 发版前两格 / F7 反汇编** | ADR-012 F2 / 发版前 / §三 | **未跑(未跑不冒充)** |

已交付的 Mono 侧可跑面(AC-28 黄金比对 + AC-30 静态扫描,5 测)落点表见
`tests/unit/item_database/README.md` §Story 011;AC-29 腿的双腿跑法与受控重复纪律同见该节。

---

## 产物漂移门(2026-10-20 立 · 跨 Story 的基建件,不属于任何单一 Story)

### 为什么会有这条门

**实测暴露的缺口**:改 `assets/data/item_database_*.json` 而**不重烘**
`unity/Assets/DataCooked/*.cooked.bytes`,**全量 EditMode 照样全绿**。

**为什么绿**:本套件加门之前的 **55 个用例全部**走 `BakeFromSourceText` / `BakeFromRepo`(**现烘**),
**没有一条**读 `DataCooked/` 下那份**已提交的**字节 ⇒ 漂移通道无断言覆盖。

> ⚠️ **这不是「断言写错」,是「该断言的通道没被覆盖」** —— 属借绿家族的
> 「扫不到被测装配」同族形态。发现经过:S-2
> (`processing/story-001` AC-18-18 补口,2026-10-20)时,盘上产物是**改动前**烘的陈旧件,
> 而全量套件 `3951/3915/0/36` 全绿 —— 这才把缺口照出来。

**同型门早已在仓里,只是没扩到这里**:`Boot/examine_driver_test.cs:92-108` 对
`diagnosis_signs` / `diagnosis_read_floor` 两份产物断言「库内字节须 = baker 现烘输出」。
本条门 = **把同一形态扩到 item DB 两产物**(照抄先例,不引入新机制)。

### 落点

| 内容 | 路径 |
|---|---|
| 门真身 | `unity/Assets/Tests/EditMode/ItemDatabase/data_pipeline_bake_test.cs` |
| 段名 | `══════════ 产物漂移门(改源必重烘)══════════` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(零新增引用 —— 不用 `UnityEngine`,路径由既有的 `UnityAssetsRoot` 常量拼) |

### 三条断言

| 测试 | 判据 | 判别力 |
|---|---|---|
| `test_drift_itemsCooked_matchesBakerFreshOutput` | 盘上 items 产物 **逐字节** = baker 现烘 | ✅ 变异实测转红 |
| `test_drift_recipesCooked_matchesBakerFreshOutput` | 盘上 recipes 产物 **逐字节** = baker 现烘 | ✅ 变异实测转红 |
| `test_drift_configVersion_onDisk_matchesFreshlyDerived` | 两产物头 `ConfigVersion` 各 = 由现源复算值 | ✅ 单侧陈旧时转红 |

> ⚠️ **第三条不是前两条的替代** —— 两份同源同旧时,头值仍**互相一致**,
> `ConfigVersion` 门**看不出来**。漂移只能由**逐字节比对**兜住。三条各查一个面,都保留。

### 证伪轮(判别力实测)

变异全部作用于**已提交的产物字节**(不动源 JSON),等价于「改了源却没重烘」:

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| **D0** | 无(基线) | `3/0` | ✅ `3 / 0` |
| **D1** | 篡改 items 产物 1 字节 | items 漂移门红 | ✅ `2 / 1`,红的是 `test_drift_itemsCooked_matchesBakerFreshOutput` |
| **D2** | 篡改 recipes 产物 1 字节 | recipes 漂移门红 | ✅ `2 / 1`,红的是 `test_drift_recipesCooked_matchesBakerFreshOutput` |
| **D3** | 改 items 头 `ConfigVersion` 4 字节 | items 漂移门 **+** configVersion 门 **两红** | ✅ `1 / 2`,两项皆红 |

**逐例红项(实测取证,非「期望」)**:

| # | 实际转红的测试 |
|---|---|
| D1 | `test_drift_itemsCooked_matchesBakerFreshOutput` |
| D2 | `test_drift_recipesCooked_matchesBakerFreshOutput` |
| D3 | `test_drift_configVersion_onDisk_matchesFreshlyDerived` · `test_drift_itemsCooked_matchesBakerFreshOutput` |

> ⚠️ **判别力的关键**:D1/D2 各只转红**对应的一项**,另一项仍绿 ——
> 证明两条漂移门**各自独立**,不是「任一产物变了就整体红」(那会掩盖是哪一侧陈旧)。
> D3 转红两项且 `passed=1`(recipes 漂移门仍绿,因 recipes 产物未动)—— 同理。

**还原纪律**:每例跑完**立即** `cp` 回并做 `sha256` 逐字节比对(承 S-2 证伪轮纪律;
禁 `git checkout` / `git stash` —— 会抹掉未提交改动)。
**实测还原**:items `ff2c7d1b…831cf0` · recipes `55fc80d1…588ef2`,**逐字节一致 ✅**;
`git status unity/Assets/DataCooked/` **干净**。
脚本原件:`/tmp/fz-drift-gate.py`(临时件,不入库)。

### ⚠️ 本条门的边界(禁借绿)

- **只查产出侧字节一致性,不查源内容正确性** —— 源里写错数值而正确重烘,本条门**绿**。
  源内容正确性归各 AC 自身的门。
- ~~**只覆盖 item DB 两份产物**~~ —— **2026-10-20 已成过去式**:全目录通用门已落
  `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs`,覆盖**盘上全部 15 份**。
  ⚠️ **原记的「须先统一各 baker 的 `BakeFromRepo` 签名与产物命名」这一前置被实测证伪** ——
  14 个 baker 的返回体虽是各自文件内的 `BakeOutput`(不共享类型),但都以 `byte[] Cooked` 暴露产物;
  `ItemDatabaseBaker` 返回 `BakeResult` 带两个产物。**用委托登记表(`Func<string, byte[]>`)
  即可绕开签名不统一,无需改动任何 baker**。该前置本就不成立,不是「已满足」而是「不必满足」。
- **不替代**「改源必重烘」的流程纪律,只是把静默失败变成**可观测的失败**。

---

## 全目录通用漂移门(2026-10-20 立 · 跨系统基建)

### 落点

| 内容 | 路径 |
|---|---|
| 门真身 | `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` |
| 命名空间 | `DaYiJingCheng.Tests.Unit.DataPipeline` |
| 装配 | `Tests/EditMode/EditMode.asmdef`(零新增引用) |
| 覆盖 | `unity/Assets/DataCooked/` **盘上全部 15 份**产物(原 4/9;2026-10-20 补烘 6 份欠账后) |

**为什么单立目录**:本门覆盖 10 个系统(2 物品 / 3 诊断 / 1 病例 / 2 音频 / 1 输入 /
1 遥测 / 1 交互 / 1 处方 / 1 处置轴 / 2 医疗后果),
是跨 Story 的数据管线基建,**不属于 `ItemDatabase/`**。初版落在 `ItemDatabase/`(续写先例的顺位),
判据面与位置不符 ⇒ 迁独立目录。

### 三条判据 + 两个哨兵(19 用例)

| # | 测试 | 判据 |
|---|---|---|
| ① ×15 | `drift[<产物名>]`(`TestCaseSource`) | 盘上字节 **逐字节** = 该 baker 现烘输出 |
| ② | `test_sweep_noGhostArtifact_onDiskButUnregistered` | 盘上每一份都在两张表里(禁**幽灵产物**) |
| ②b | `test_sweep_declaredArtifactNames_allTriaged` | 源码声明的每个产物名都在两张表里(禁**新增 baker 忘了登记**) |
| ③a | `test_registry_noDuplicateOrCrossRegistration` | 两张表内零重复、零交叉、欠账项须写理由 |
| ③b | `test_registry_knownUnbakedCount_isPinned` | 欠账清单条数钉定为 **0**(2026-10-20 补烘前为 6;防其长成永久豁免名单) |

**封闭性**:`已烘 ⊆ 盘上`(① 的 `File.Exists`)· `盘上 ⊆ 已烘 ∪ 欠账`(②)· `声明 ⊆ 已烘 ∪ 欠账`(②b)
—— 三条合起来,新增产物无论走哪条路都必须显式登记。

**扫描器哨兵**:②b 内钉 `pinnedDeclared = 15`(源码声明产物名总数)。若注释剥离器被改坏、把字面量
吞掉,声明集会**变小** ⇒ 该断言先红 —— 这是本门唯一能自证「扫全了」的判据。

### 证伪轮(实测,不是「测试名 + 行号」)

**纪律**:禁 `git checkout` / `git stash`(会抹未提交改动)⇒ 备份 → 单点变异 → 跑 → `cp` 回 → `sha256` 比对。

| # | 变异 | 期望 | 实测红项 |
|---|---|---|---|
| **F0** | 无(基线) | `13 / 0` | ✅ `13/13` |
| **F1** | 篡改 `audio_events` 产物 1 字节 | 仅该份漂移门红 | ✅ `12/1` — `drift[audio_events.cooked.bytes]` |
| **F2** | 篡改 `case_judgment_lexicon` 产物 | 仅该份红 | ✅ `12/1` — `drift[case_judgment_lexicon.cooked.bytes]` |
| **F3** | 伪造 `zz_ghost.cooked.bytes` | 门② 红 | ✅ `12/1` — `test_sweep_noGhostArtifact_onDiskButUnregistered` |
| **F4b** | 非 Tests 源码(`DataBakeMenu.cs`)插入未登记产物名 | 门②b 红 | ✅ `12/1` — `test_sweep_declaredArtifactNames_allTriaged` |
| **F5** | 欠账清单增一条 | 钉定条数红 | ✅ `12/1` — `test_registry_knownUnbakedCount_isPinned` |

**F1/F2 的判别力关键**:各只转红**自己那份**,其余 12 条仍绿 ⇒ 9 条漂移门**逐产物独立**,
不是「任一产物变了就整体红」(那会掩盖究竟哪一份陈旧)。

> ⚠️ **F4 首版设计错误(留作记录)**:首版把未登记产物名插进**测试文件自身**,
> 结果 `13/13` 全绿、被初读为「门无判别力」。真相是**变异落错了面** ——
> 扫描面**排除 `/Tests/`**(否则测试文件里的 15 个产物名字面量会让门永久自红),
> 所以插在测试里当然看不见。**这次失败反向证明了 `/Tests/` 排除确实生效**,
> 但它验证不了门本身 ⇒ 重做为 F4b(变异落 `DataBakeMenu.cs`),才真正转红。
> 教训与 S-2 首轮同型:**「没转红」先查变异是否落在判据面内**,再断言门失效。

**还原**:audio / lexicon / `DataBakeMenu.cs` 三处 `sha256` 逐字节一致;
`grep UnregisteredProbeCooked` = **0** 残留。脚本原件 `/tmp/fz-all-artifacts.py` · `/tmp/fz-f4b.py`(临时件,不入库)。

### ⚠️ 本条门的边界(禁借绿)

- **只查字节一致性,不查源内容正确性** —— 源里写错数值而正确重烘,本门**绿**。
- **不裁决「该不该有某份产物」** —— 那是接线决策,归各 epic。6 份未落盘产物走**具名欠账登记**。
- **残余盲区**:一份产物若**既不落盘、又不被任何源码字面量提到**,本门看不见它(无来源可比对)。
  但那种形态连「谁烘它」都没写下来,已不属本门判据面。

### 回归对账(2026-10-20 · 两轮)

**轮 1 —— 全目录门落盘后(覆盖 9 份)**

| 项 | 值 |
|---|---|
| 本轮 | **`total=3967 passed=3931 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(加全目录门前) | `3954 / 3918 / 0 / 36` |
| Δ | **`+13 / +13 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(13 条:9 漂移 + 2 扫描 + 2 登记表)⇒ **✅ 精确成立** |

> 三元组:截至时刻 `2026-10-20 10:32 本地` · 批次 `全目录漂移门落盘轮` · commit `0a144f1` + 工作树。

**轮 2 —— 6 份欠账补烘后(覆盖 15 份)**

| 项 | 值 |
|---|---|
| 本轮 | **`total=3973 passed=3937 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(轮 1) | `3967 / 3931 / 0 / 36` |
| Δ | **`+6 / +6 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(6 条新漂移测 = 6 份补烘产物)⇒ **✅ 精确成立,零回归** |

> 三元组:截至时刻 `2026-10-20 10:42 本地` · 批次 `6 份欠账补烘轮(全量 EditMode)` ·
> commit `95af239` + 未提交工作树(6 产物 + 6 `.meta` + 门 + 2 账本)。
> ⚠️ 本门 19 条在轮 2 中**全通过**;`+6` 恰为「6 份新产物进入门①」的数目,可复算。

**定向验证(补烘后)**:**`drift-15.xml` = 19/19 passed** —— 15 份产物逐字节 = baker 现烘输出,
证明补烘产出的是**正确字节**而非空壳。

### 6 份欠账的发现与补烘(2026-10-20 · 已结案)

**发现**:6 份产物有 baker、有菜单项、有测试,**从未落盘**(git 历史 **0 笔**):

`interaction_kinds` · `diagnosis_negative_confidence` · `prescription_actions` ·
`disease_action_axis` · `53_outcome_table` · `53_delay_table`

逐份查明:① 无任何代码从 `DataCooked/` 读它们;② `AddressablesDataProvider.Preload()`
恰装载盘上那 9 份 ⇒ **不在运行期面内**,故从未被发现。

**⚠️ 关键事实**:这 6 份所归属的 **5 个 story 全部标 `Complete`**,其中
`interaction-system/story-007-interaction-kinds-bake-wiring`(名字即「烘焙接线」)的 AC 明写
「全绿 ⇒ 写出 deterministic `interaction_kinds.cooked`」,而该产物 git **0 笔**。
⇒ 这不是「被裁为延后」,是**有 baker 无产物**的漏项。

**✅ 处置(用户 2026-10-20 裁定:「那 6 份补烘掉」)—— 六份全部补烘落盘**

| 产物 | 大小 | ConfigVersion | 菜单方法 |
|---|---|---|---|
| `interaction_kinds` | 330 B | `0x75D32E3B` | `BakeInteractionKinds` |
| `diagnosis_negative_confidence` | 312 B | `0x1CE313DD` | `BakeDiagnosisNegativeConfidence` |
| `prescription_actions` | 77 B | `0xA5273A45` | `BakePrescriptionActions` |
| `disease_action_axis` | 304 B | `0x6C83B441` | `BakeDiseaseActionAxis` |
| `53_outcome_table` | 176 B | `0x47F95050` | `BakeOutcomeTable` |
| `53_delay_table` | 60 B | `0xDE533AC6` | `BakeDelayTable` |

- 命令:`unity build <unity/> --target StandaloneLinux64 --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.<方法>`
- 六次全部 `Exiting batchmode successfully now!` · exit **0**;`.meta` 由 Unity 自动生成、随产物入库
- **ConfigVersion 全部非零** ⇒ 源内容哈希派生链路生效(ADR-014 §五),不是空壳
- 六份已从 `KnownUnbaked` **移入 `BakedArtifacts`** ⇒ 受门① **逐字节**保护
- `KnownUnbaked` 清空(6 → 0),钉定值同步改 0

> ⚠️ **补烘 ≠ 接线**:这 6 份现在**有正确字节**,但仍**未接入运行期装载面**
> (`AddressablesDataProvider` 无对应 address、`Preload()` 不含它们)。
> 补烘解决的是「有 baker 无产物」;**「产物有了但没有读者」是另一个问题**,
> 归各归属 epic 的接线轮,不在本条门裁决面。逐份的接线状态见上表所属 story。
>
> ✅ **2026-10-20 第二次裁定「接到运行期装载面」—— 路 A,已落 2 份**:
> `interaction_kinds` + `diagnosis_negative_confidence`(其余 4 份需新运行期数据集类型 + 解码器,
> 各自 120–220 行,另开一轮)。详细见下节 §运行期装载面接线。

### 运行期装载面接线(2026-10-20 · 路 A 2/6 → 第二轮 4/6 ⇒ **6/6 全部接线**)

> **两轮合记**:路 A 接 `interaction_kinds` / `diagnosis_negative_confidence`;
> 第二轮(用户裁定「把剩下 4 份也接上」)接 `prescription_actions` / `disease_action_axis` /
> `53_outcome_table` / `53_delay_table`,并同时为它们写运行期 codec + 数据集类型。
> 原件:`production/qa/evidence/impl-data-core-wiring-a-2026-10-20.md`(路 A)·
> `production/qa/evidence/impl-data-core-wiring-b-2026-10-20.md`(第二轮)。

**为什么不能「补烘完就标完」**:6 份产物归属的 5 个 story **全部 `Complete`**,
但其中一件名字就叫「烘焙接线」(`story-007-interaction-kinds-bake-wiring`)——
**「烘焙接线」这个词在仓库里被用来指「烘焙管线接线」,而运行期装载是另一条线**。
补烘让字节成立;接线让**启动时会有人读它**。两者是不同的失效面。

**路 A 的四要素(少一处即静默失效)**

| # | 要素 | 落点 | 失了的症状 |
|---|---|---|---|
| ① | address 常量 | `AddressablesDataProvider.{InteractionKinds,DiagnosisNegativeConfidence,PrescriptionActions,DiseaseActionAxis,OutcomeTable,DelayTable}Address` | 无处可查 |
| ② | Addressables 组条目 | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset`(**入库**) | 启动预载 `FetchBytes` 直接 **E-13 硬失败**,整链起不来 |
| ③ | 组条目 GUID ↔ 产物 `.meta` | 同上(YAML 手可编辑) | 条目指向虚空,打包期才炸 |
| ④ | `Preload()` 调用 + `DataCoreAddresses` | `AddressablesDataProvider.Preload()` · `DataCorePreloader.DataCoreAddresses` | 方法存在但缓存永不填充,首次 `Load` 在远处炸 |

**第二轮补的两件事(路 A 不需要、后 4 份缺)**

| 件 | 落点 | 为什么必须有 |
|---|---|---|
| 运行期 codec + 数据集类型 ×4 | `Gameplay.Presentation/{PrescriptionActions,DiseaseActionAxis,OutcomeTable,DelayTable}CookedCodec.cs` · `CookedReadUtil.cs` | 后 4 份此前**零读方**;行类型住 `Editor.Tools.Bake`(不进玩家构建),故各 codec 自带同形只读行 |
| **读方镜像门** | `DataPipeline/cooked_codec_roundtrip_test.cs`(4 用例) | 漂移门只证「字节 == 现烘」,**不证有人读得懂**。本轮首次为 `disease_action_axis` 写读方,即抓出写方用 7-bit 变长串而 `CookedFormat` 规定 i32(详见证据 §2);该门把这类「字节稳定、写读不同形」变成红 |

**门真身**:`unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`
(7 → **11** 用例;装配零新增引用 —— 走 `Tests/EditMode/EditMode.asmdef`)

| 用例 | 判据 | 对应失效模式 |
|---|---|---|
| `test_wiring_newArtifactAddresses_matchBakerArtifactNames` | 6 组 address 字面串 == `DataBakeMenu.*CookedAssetName` | ① |
| `test_wiring_dataCoreGroup_containsEveryProviderAddress` | 每个 `*Address` 常量在组里**恰 1 条** | ② |
| `test_wiring_dataCoreGroup_entriesResolveToExistingArtifacts` | 组每条 → `DataCooked/` 有真文件 | ② |
| `test_wiring_dataCoreGroup_guidsMatchArtifactMeta` | 组 `m_GUID` == 产物 `.meta` 的 guid | ③ |
| `test_wiring_preload_invokesEveryEnsureMethod` | `Ensure*` 方法集 == `Preload()` 调用集 == 地址表集 | ④ |
| `test_wiring_loadBranch_{interactionKinds,negativeConfidence,prescriptionActions,diseaseActionAxis,outcomeTable,delayTable}_exists` | `Load<T>` 走到装载分支(非「未知类型」兜底) | 分支缺失 |

> ⚠️ 两个 `LoadBranch` 用例**不依赖装载成功** —— EditMode 无 Addressables locator,装载必失败;
> 判别靠**失败报文**:走到装载分支 ⇒ 报文点名产物基名;分支缺失 ⇒ 报文是「未知数据集类型」。
> 两者互斥 ⇒ 有判别力。(`LogAssert.ignoreFailingMessages` 只关日志门,断言照跑。)

**证伪轮(两轮合记 · **13/13 全转红**)

路 A(F1–F6,对象 = 路 A 的两份):

| # | 变异 | 实测红项 |
|---|---|---|
| **F1** | provider 的 `InteractionKindsAddress` 字面串改一字 | `newArtifactAddresses` + `dataCoreGroup_containsEvery…` **2 红** |
| **F2** | 组里删掉 `diagnosis_negative_confidence` 条目 | `dataCoreGroup_containsEvery…` |
| **F3** | 组条目 GUID 末位 `8→9` | `dataCoreGroup_guidsMatchArtifactMeta` |
| **F4** | `Preload()` 删掉 `EnsureInteractionKinds();` | `preload_invokesEveryEnsureMethod` |
| **F5** | 删掉 `Load<T>` 的 `InteractionKindDataSet` 分支 | `loadBranch_interactionKinds_exists` |
| **F6** | `DataCoreAddresses` 删掉 `InteractionKindsAddress` | `preload_invokesEveryEnsureMethod` |

第二轮(F1′–F7′,对象 = 后 4 份 + 镜像门):

| # | 变异 | 实测红项 |
|---|---|---|
| **F1′** | `PrescriptionActionsAddress` 字面串改一字 | `newArtifactAddresses` + `dataCoreGroup_containsEvery…` **2 红** |
| **F2′** | 组里删掉 `53_delay_table` 条目 | `dataCoreGroup_containsEvery…` |
| **F3′** | 组条目 GUID 末位 `c→d` | `dataCoreGroup_guidsMatchArtifactMeta` |
| **F4′** | `Preload()` 删掉 `EnsureDelayTable();` | `preload_invokesEveryEnsureMethod` |
| **F5′** | 删掉 `Load<T>` 的 `DelayTable53DataSet` 分支 | `loadBranch_delayTable_exists` |
| **F6′** | `DataCoreAddresses` 删掉 `DelayTableAddress` | `preload_invokesEveryEnsureMethod` |
| **F7′** | `DiseaseActionAxisCookedWriter` 回退 `BinaryWriter.Write(string)`(7-bit 变长) | `roundTrip_diseaseActionAxis_diskMatchesFreshBake` |

- 基线:路 A `7/7` · 第二轮 `11/11` + `4/4`;每例「备份 → 单点变异 → 跑 → 还原 → `sha256` 比对」**同一执行内**完成
- 十三例还原后 `sha256` **逐字节一致**;`git status` 与变异前同形
- **`F1`/`F1′` 红 2 条是预期**:它们同时改坏字面串 ⇒ 组里自然查不到那个 address,
  故 ② 与 ① 同红;不是误伤(其余十一例各**仅**红 1 条,证明隔离性好)
- **`F7′` 是本轮信息量最大的一例**:它复现了 `disease_action_axis` 写方的历史违例
  (`CookedFormat` 规定 `i32` 长度串,写方用 7-bit 变长),证明镜像门对该形态**有判别力**
- 脚本原件 `/tmp/fz-wiring.py`(路 A)· `/tmp/fz-wiring-b.py`(第二轮)—— 临时件,不入库

**⚠️ 必须如实登记的四条边界(禁借绿)**

1. **6 个新数据集在运行期**仍然**零读者**。实测非 `Tests/` 面:
   `Load<InteractionKindDataSet>` / `Load<DiagnosisNegativeConfidenceDataSet>` /
   `Load<PrescriptionActionsDataSet>` / `Load<DiseaseActionAxisDataSet>` /
   `Load<OutcomeTable53DataSet>` / `Load<DelayTable53DataSet>` **全部零命中**;
   `ImmutableInteractionKindTable`(story-007 `## Dependencies` 里「Unlocks」的那件)在仓库里
   **不存在**;`Outcome53.Judge.Evaluate` 生产侧零调用点;`ConsequenceEmitter`(消费
   `DelayTable53.Table`)仍**只在测试里被构造**。
   ⇒ 两轮做的是「**可装载**」,不是「已被使用」。读者归各 epic 的消费轮。
2. **本机 catalog 必须重建后才生效**。`unity/Library/com.unity.addressables/aa/Linux/catalog.bin`
   是**机器本地产物**(`.gitignore:61` `[Ll]ibrary/`)。实测旧 catalog(**10-19 22:42**)里
   `interaction_kinds.cooked.bytes` / `diagnosis_negative_confidence.cooked.bytes` **均 MISS**。
   ⇒ 新检出 / 未重建 catalog 的机器上,`test_precheck_liveCatalog_allRealAddressesLocatable`
   (PlayMode)会判死。**这不是缺陷,是 E-13 的设计行为**;但跨机复现须先跑
   `大医精诚/数据管线/构建 Addressables 内容`。第二轮重建后实测 **15/15 HIT**。
3. **`53_outcome_table` 的 `has_default` 位不在载荷里**。载荷只有
   `u_band_count` / `default_outcome` / `row_count` / 行集。读方据此传 `hasDefault: true`,
   依据是 `OutcomeTableBinder.ValidateCoverage` **硬拒无 default 行的表** ⇒ 合法产物该位恒真。
   **这是从烘焙期不变量推出的,不是载荷携带的** —— 已在 codec 文件头写明;若需读方独立判别,
   须升 `CookedFormat.FormatVersion`(不在本轮面内)。
4. **IL2CPP 三格(ADR-012 矩阵)NOT-RUN** —— 本机仅 Mono。

**连带改动**:`DataCorePreloader.DataCoreAddresses` 9 → 11 → **15 条**(与 `Preload()` 的
`Ensure*` 调用数逐一相等,由 `test_preloader_list_sources_matchPreloadedProducts` 与
`test_wiring_preload_invokesEveryEnsureMethod` 双门钉住);`Preload()` 的 E-13 未知类型
错误串同步订正。

**⚠️ 第二轮抓出的写方契约违例(如实登记)**

`DiseaseActionAxisCookedWriter` 原以 `BinaryWriter.Write(string)` 写字符串 = **7-bit 变长前缀**,
而 `CookedFormat` 明文规定载荷内字符串 = **`i32` 字节长 + UTF-8**(九个姊妹写方均照此)。
该产物此前**全库零读方**,漂移门只比对「盘上 == 现烘」⇒ 自洽的错字节长期不可见。
第二轮首次为它写读方即暴露。**处置 = 修写方 + 重烘**(而非让读方去实现 7-bit ——
那会把偏离固化成永久行为);产物 **304 → 370 B**,`ConfigVersion = 0x6C83B441`,
`drift[disease_action_axis.cooked.bytes]` 证明盘上与现烘一致。
回归夹具 = `roundTrip_diseaseActionAxis_diskMatchesFreshBake`(F7′ 实测回退即红)。

**回归对账(2026-10-20 · 第二轮接线轮)**

| 项 | 值 |
|---|---|
| 本轮 | **`total=3988 passed=3952 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(路 A 接线轮) | `3980 / 3944 / 0 / 36` |
| Δ | **`+8 / +8 / 0 / 0`**(4 条 `LoadBranch` + 4 条镜像门) |
| 等式判据 | `total − 前次 = 新增数` ⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 11:30 本地` · 批次 `第二轮接线轮(全量 EditMode)` ·
> commit `bdadfb3` + 未提交工作树。结果原件 `unity/Logs/wiring-b-full.xml`;
> 报告原件 `production/qa/evidence/impl-data-core-wiring-b-2026-10-20.md`。

**回归对账(2026-10-20 · 路 A 接线轮)**

| 项 | 值 |
|---|---|
| 本轮 | **`total=3980 passed=3944 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(补烘轮) | `3973 / 3937 / 0 / 36` |
| Δ | **`+7 / +7 / 0 / 0`** |
| 等式判据 | 7 条新接线测 ⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 11:01 本地` · 批次 `路 A 接线轮(全量 EditMode)` ·
> commit `08fb813` + 未提交工作树。原件
> `production/qa/evidence/impl-data-core-wiring-a-2026-10-20.md`。


### 回归对账(2026-10-20 · 加门后)

| 项 | 值 |
|---|---|
| 命令 | `unity test <unity/> --mode EditMode --output <绝对路径>` |
| 本轮 | **`total=3954 passed=3918 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(加门前) | `3951 / 3915 / 0 / 36` |
| Δ | **`+3 / +3 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(3 条新门)⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 10:17 本地` · 批次 `产物漂移门落盘轮(全量 EditMode)` ·
> commit `6afc18d` + 未提交工作树(1 测试文件 + 2 账本)。
> ⚠️ **`+3` 是实跑出来的** —— 前次 `3951` 亦实跑且带三元组(`2026-10-20 09:46`,S-2 批),可复算。
> ⚠️ **`3951` 与 `3954` 的用途不同,勿混引**:`3951` 是**加门之前**的值,
> 它「产物陈旧却全绿」这件事本身就是**缺口证据**(见 §为什么会有这条门)。



---

## 2026-10-20 更新④ —— **读者面接线**(装载 → 消费)

上两轮(①②)做完的是「**可装载**」,并在原件 §7.1 如实登记了边界:

> 「6 个新数据集在运行期**仍然零读者**…… 读者归各 epic 的消费轮。」

本轮把那条边界收口 —— **接 2 份 · 如实登记 4 份**。

### 6 份按「有没有真读点」实测分三档

| # | 数据集 | 生产侧现状 | 档 | 本轮 |
|---|---|---|---|---|
| 1 | `interaction_kinds` | `KindPriorityTable` 硬编码 10 项;选择器 + 校验器都在读它 | **甲** | ✅ 换源 |
| 2 | `prescription_actions` | `PrescribeRequestDriver._entries` 硬编码单条;`doseBase: 65536` 字面量 | **甲** | ✅ 换源 |
| 3 | `disease_action_axis` | `ValidateActionIdFromAxis` 零生产调用;R1-19 是显式 `NotImplementedException` | 乙 | 登记 |
| 4 | `diagnosis_negative_confidence` | `Evaluator` 零生产调用;8 的运行期求值走阳性族 | 乙 | 登记 |
| 5 | `53_outcome_table` | `Judge.Evaluate` / `JudgeInputs` 零生产构造 | 丙 | 登记 |
| 6 | `53_delay_table` | `new ConsequenceEmitter(` 零生产构造 | 丙 | 登记 |

**为什么不把 4 份也「接上」**:乙档缺的是**调用方**、丙档缺的是**整条消费链**。
往缝里塞 provider 而没人读 = 「已装载但无人用」,与前两轮登记的边界**同一形态**,
属借绿家族。逐份的 grep 证据见
`production/qa/evidence/impl-data-core-readers-2026-10-20.md` §5。

### 门:`ReaderWiringGateTest`(10 用例)

| 段 | 用例 | 性质 |
|---|---|---|
| 交互表 | `artifactRows_matchFallbackLiteral` | **对照**(字面量作独立 oracle) |
| | `artifactRows_totalOrderAndCoverEnum` | 数据完整性 |
| | `providerInjection_swapsTable` | **换源(承重)** |
| | `assemblyInjectsProvider` | **接线(承重)** |
| | `duplicateKindInRows_throws` | 负向 |
| 处方表 | `artifactRows_matchFallbackLiteral` | **对照** |
| | `realArtifact_rowIsPrescribable` | 正向 |
| | `syntheticRow_drivesEntriesAndDoseBase` | **换源(承重)** |
| | `duplicateItemKey_throws` | 负向 |
| | `unknownPolarity_throws` | 负向 |

> ⚠️ **「换源」两段刻意用合成夹具**:两份产物的数据**逐值等于**既有硬编码字面量
> ⇒ 用真产物测「源换了」是**恒绿假门**。真产物只用于「对照」与「正向可跑」。
> **本轮不改变任何行为** —— 判据是「源真的换了」,不是「行为变了」。

### ⚠️ 一处必须看清的边界

`InteractionSelector` 在生产侧**仍零构造点**(实测:非 Tests 面唯一命中是 `ModalGate` 的形参)。
⇒ 本轮接的是**契约层读点**(选择器第二键查表 + 4-DC-3 校验器),
**不是**「运行期已有实例在查」。选择器的生产装配归 4 的装配轮。

详见 `production/qa/evidence/impl-data-core-readers-2026-10-20.md`。

### 回归对账(2026-10-20 12:19 CST · commit `1a55c81` · 工作树 9 个未提交改动)

| 项 | 值 |
|---|---|
| 前次 | `total=3988 passed=3952 failed=0 inconclusive=0 skipped=36` |
| 本轮 | **`total=3998 passed=3962 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 等式判据 | `total − 前次 = 新增数`(10 条门用例)⇒ **✅ 精确成立,零回归** |
| PlayMode | **NOT-RUN** ⇒ P-4(`ResetForPlayModeTests` 清 Provider)保持 `Landed ✅ / Proven ✗` |

> ⚠️ **本轮第二次踩「没出结果 ≠ 绿的」**:首轮全量回归**根本没启动** —— 等待前置
> `until ! pgrep -f "Unity.*batchmode" …` 里,`pgrep -f` 匹配整条命令行,而等待脚本
> **自身命令行就含该模式字面量** ⇒ 自匹配 ⇒ `until` 恒假 ⇒ 四个等待任务全部空转。
> 处置改**同步前台**跑。判据:XML 的 mtime 必须晚于启动时刻。
> 完整记载见证据原件 §7.1(前两个变体:XML 陈旧、变异脚本编译失败)。

---

## 2026-10-20 更新⑤ —— 乙档第一份接线(`disease_action_axis` → DC-4)+ 第二份四层登记

**用户裁定**(AskUserQuestion 两问):B(`diagnosis_negative_confidence`)「只接 A,B 降级登记」·
A 的接线点「Processor 构造注入 + Process 入口校验」。

### 新增门:`ActionAxisReaderGateTest`(9 用例)

| 组 | 用例 | 性质 |
|---|---|---|
| A | `artifactOwner10Set_matchesEmergencyActionEnum` | **对照** |
| A | `artifactIdsUniqueAndOwnerClosed` | 数据完整性 |
| B | `syntheticAxis_rejectsActionOutsideAxis` | **换源(承重 · 拒)** |
| B | `syntheticAxis_acceptsActionInsideAxis` | **换源(承重 · 放)** |
| B | `gatesPayloadActionNotJustRowActionId` | **门覆盖载荷侧** |
| C | `emptyAxis_throwsDistinctFromNotInjected` | 负向(空集 ≠ 未注入) |
| C | `notInjected_fallsBackToEnumShadow` | 回退(零行为变化) |
| D | `assemblyWithArtifact_acceptsActionInAxis` | 接线(对照半边,**单独看无判别力**) |
| D | `assemblyInjectsSyntheticAxis_overridesShadow` | **接线(承重半边)** |

> ⚠️ **「换源」必须用合成夹具** —— 产物实测 `owner=="10"` 段 = `{0,1}` **恰等于**
> `EmergencyAction` 枚举 ⇒ 接线前后行为**逐位相同**,用真产物测「源换了」是**恒绿假门**。
> 这不是修辞:证伪 F4 首轮删掉组合根注入,**9 条全绿**(假绿夹具的形态),
> 补合成轴 `{99}` 那条后才转红。

### B 侧四层缺口(不硬接的理由)

| 层 | 缺口 |
|---|---|
| ① 求值 | 实现**齐备** —— 不缺 |
| ② 装载 | `Load<DiagnosisNegativeConfidenceDataSet>` **全库零调用**(含测试) |
| ③ 传输 | `ExaminedSet` 只 4 字段,**无把握度**;`ExamineDriver` 无注入点 |
| ④ 呈现 | 42 `ConfidenceColumn` **无数值字段**;`PendingConfidence` 是 u8 判断档位,非 F-8.3 把握度 |
| ⑤ 下游 | `ConstitutesExclusion` / `ExclusionLevel` / `CurveAt` **零生产调用** |

⇒ 即便接上 ②,①③④⑤ 全缺 ⇒ 「装载了但**零玩法效果**」。**`AC-8-13` 仍未可执行**。

### 回归对账(2026-10-20 12:51 CST · commit `370345c` · 工作树 6 个未提交改动)

| 项 | 值 |
|---|---|
| 前次 | `total=3998 passed=3962 failed=0 inconclusive=0 skipped=36` |
| 本轮 | **`total=4007 passed=3971 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 等式判据 | `total − 前次 = 新增数`(9 条门用例)⇒ **✅ 精确成立,零回归** |
| PlayMode | **NOT-RUN** |

> ⚠️ **首次全量跑出 6 条红**(`passed=3965 failed=6`),**是本轮真回归,不是测试污染**。
> 其中 **5 条**是装配面扩大的正常后果(`ReaderWiringGateTest` 的 provider 缺新表);
> 第 **6** 条(`cooked_drift[interaction_kinds]`)是**跨夹具、跨命名空间的静态状态泄漏**
> (`CompositionRoot:348` 设进程级 `KindPriorityTable.Provider`,新夹具没复位)。
> 区分方法:删掉新增测试文件后 5 条仍红(产品接线仍在),第 6 条转绿。
> 因果由证伪 F6 反证:重新去掉复位,该 case **精确转红**。

详见 `production/qa/evidence/impl-data-core-readers-b-2026-10-20.md`。

---

## 2026-10-20 更新⑥ —— 甲档剩余项接线(`prescription_actions.MAX_DOSE_DETENTS` → `PrescribeRequestDriver`)+ 三方值分歧订正

**用户裁定**(AskUserQuestion):「**先改 JSON 为 7 重烘,再接**」。

### 「甲档剩下的」是怎么定出来的 —— 逐字段普查,不是估计

甲档 2 份(`interaction_kinds` / `prescription_actions`)的**主读点**在 `370345c` 已接。
本轮把两份产物的**每一个字段**过一遍生产读者,才定出唯一可接项:

| 产物 | 可接(甲) | 缺调用方(乙) | 缺整条链(丙) |
|---|---|---|---|
| `interaction_kinds` | `kind_priority`(已接) | `r_interact` / `routes_to` / `stable_id_source` | `suppresses_motor` / `duration_owner` / `intent_uplink` |
| `prescription_actions` | `DOSE_BASE` / `actions[]`(已接)· **`MAX_DOSE_DETENTS`(本件)** | `single_dose_max` | — |

⇒ **`MAX_DOSE_DETENTS` 是甲档 2 份的最后一个零读者字段**。其余 6 项本轮**不改判**,
只是把「乙/丙」细化到字段级(理由逐条可复算,见原件 §0/§7.3)。

### ⚠️ 普查抓出的真问题:同一个旋钮**三个值互不相同**

| 来源 | 改前 | 权威性 |
|---|---|---|
| GDD `prescription-and-medication.md:794` | **7** | **已裁**(2026-10-07;原「建议 ≤ 9」的**窄化**) |
| 产物 JSON | 5 | 文件 `_note` 自述「占位种子 / 夹具值」 |
| 代码常量 `RegisteredMaxDoseDetents` | 9 | **过期** —— 注释称「GDD 规则十三」,实取**窄化前**的旧值 |

**没有任何机械判据发现它** —— 产物漂移门只比「盘上字节 == 现烘字节」,常量与产物之间**零对拍**。
当前因药档窄(`dose_range = (1,5)`)无可观测差异,属**潜伏**缺陷。
⇒ 按裁定:JSON 5 → **7**、重烘、再接 ⇒ **三处统一为 7**,此后数值轮改值只须改 JSON。

### 改动 + 新增门(`ReaderWiringGateTest` 10 → **15** 用例)

| 用例 | 性质 |
|---|---|
| `maxDoseDetents_artifactMatchesFallbackConstant` | **对照**(钉「产物与回退常量不许分叉」) |
| `maxDoseDetents_assemblyReadsArtifact` | 装配面 |
| **`maxDoseDetents_syntheticArtifact_drivesCeiling`** | **换源(唯一承重)**:合成上限 6 |
| `maxDoseDetents_withoutArtifact_fallsBackToConstant` | 回退 |
| `maxDoseDetents_detentCountIsBlindToCeiling` | **判别力论证** |

> ⚠️ **观察面必须是「上限」,不能是「落点数」** —— 当前药 `dose_range = (1,5)` ⇒ **5 落点**;
> 上限 6 / 7 / 9 都不改变 `DoseDetentCount`。拿落点数当判据 = **恒绿假门**。
> 最后一条把这个陷阱**变成可红的机器**(前置断言两装配上限**确实**为 6 / 9 且不同)。
> 另:真产物 = 回退常量 = 7 ⇒ 用真产物测「源换了」恒绿 ⇒ **承重只能靠合成夹具**。

### 证伪轮(4 变异 · **4/4 全部转红**,`finally` 还原后 sha256 逐字节一致)

| # | 变异 | 结果 | 红点 |
|---|---|---|---|
| F1 | 组合根不传上限 | `13/15 failed=2` | 换源 + 判别力 |
| F2 | 驱动忽略构造参 | `13/15 failed=2` | 同上 |
| F3 | 回退常量改 99 | `14/15 failed=1` | **只有对照条响**(其余四条不受影响是**对的** —— 它们不比常量与产物) |
| F4 | 属性改报落点数(假门形态本身) | `11/15 failed=4` | 装配面 / 换源 / 回退 / 判别力**四条同时响** |

### 回归对账(2026-10-20 13:25 CST · 批次 `sprint-15 甲档剩余项接线轮(全量 EditMode,单批)` · commit `001de75` · 工作树 8 个未提交改动 + 1 新证据件)

| 项 | 值 |
|---|---|
| 前次 | `total=4007 passed=3971 failed=0 inconclusive=0 skipped=36` |
| 本轮 | **`total=4012 passed=3976 failed=0 inconclusive=0 skipped=36`** |
| 等式判据 | `total − 前次 = 新增数`(5 条门用例)⇒ **✅ 精确成立,零回归** |
| 红点 | **无**(遍历 XML 无非 `Passed`/`Skipped` 用例;36 条 `Skipped` 与前次同,非本轮引入) |
| PlayMode | **NOT-RUN** |

> ⚠️ **退出码本轮未采集** —— 以 `nohup … &` 起跑,stdout/stderr 落盘实测 **0 字节**。
> ⇒ 判读**只依据 XML**(`failed=0` + 零红点),**不主张 `exit 0`**。
> 另:XML 根节点 `result="Skipped:Ignored"` 是 NUnit 在**有 Ignored 用例**时的根结果串,
> **不是失败信号**,防下游误读。

### ⚠️ 本轮两处如实标「未证」(禁借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| A-3 `RegisteredMaxDoseDetents` 9 → 7 | **Landed ✅ / Proven 部分** | 实体动作有 F3 间接覆盖;**但「注释所称 GDD 规则十三 = 7」是散文,无判据可钉** |
| T-3 `dentch_input_test` 扫描解耦 | **Landed ✅ / Proven ✗** | 改的是 **PlayMode** 夹具,而 PlayMode 本轮 **NOT-RUN** ⇒ 连「能被跑」都未证 |

**其余未结**(原件 §9):`single_dose_max` 仍未接(乙档)· `interaction_kinds` 6 字段未接 ·
GDD 的 `dose_range.hi ≤ 7` **启动期无门**(运行期由戥子「档数 > 上限 ⇒ 硬失败」兜住)·
IL2CPP 三格 NOT-RUN。

详见 `production/qa/evidence/impl-data-core-maxdosedetents-2026-10-20.md`。

---

## 2026-10-20 更新⑦ —— 乙档剩余 4 项:**全登记为阻塞,零代码改动**

**用户裁定**(AskUserQuestion):「**三项全登记为阻塞,不动代码**」。

甲档已清零(`7d563a2`)。乙档原 5 项,`001de75` 接走 1 项 ⇒ **剩 4 项**,本轮**全部登记为阻塞**,
但**两类阻塞的根因完全不同**,不可混为一谈:

| 字段 | 阻塞根因 | 解除前置 |
|---|---|---|
| `r_interact` · `routes_to` · `stable_id_source` | **装配链整条不存在**(载体齐备) | 4 的运行期装配轮 |
| `single_dose_max` | **缺数值裁定**(载体已在跑) | `MAX_ACTIVE_DOSE` 定值(数值轮)+ 9 装载面 |

### 一、三项交互字段:载体是好的,缺的是「谁构造它」

载体**都带真实判据**(不是没实现):`InteractionRadius`(4-DC-1 下界)· `KindRouteTable`(4-DC-5 + 双向闭合)·
`CandidateSetLoader`(4-DC-4 ②)。

**可复算的阻塞证据**:

| 测点 | 结果 |
|---|---|
| 4 侧类生产构造点(`InteractionSelector` / `ModalGate` / `NeighbourhoodReporter` / `CandidateSetLoader` / `KindRouteTable`) | **各 0** |
| `InteractionRadius` | 1 —— 但那一处在 `InteractionSelector` **自己的便捷重载**里,选择器零构造 ⇒ **不可达** |
| `IDiscoveryReporter`(4 的**唯一**出境通道)生产实现 | **0** |
| `IDropSource` / `IWorldBakedSource` / `IPatientSource` / `CandidateSources` 生产实现 | **各 0** |
| Boot 侧 4 的装配代码 | **0**(唯一命中是 `CompositionRoot:345` 的**注释**) |

**硬接的后果是可预判的**:`new InteractionRadius(ds.RInteract)` 造出没人持有的对象;
`new KindRouteTable(...)` 造出没人查的表;`CandidateSetLoader` **构造不出来**(三源零实现)。
共同形态 = **已装载但无人用** = 借绿家族。**不做。**

> ⚠️ **这是复测,不是新发现** —— 更新④ 的「一处必须看清的边界」已登记同一件事
> (「`InteractionSelector` 生产侧仍零构造点……选择器的生产装配归 4 的装配轮」)。
> **本轮实测:该边界未变。**

### 二、`single_dose_max`:载体已在跑,断在两因子乘积的另半边

**要分清:这一项与上面三项不同 —— 它的数据流是通的**:
派生(`PrescriptionDerivedBaker`,AC-11-09 零手填)✅ · 落盘(i64 raw `= 32768`,已字节解码)✅ ·
解码(`PrescriptionActionsCookedCodec:36`)✅ · 消费 API 已实现**且有测** ✅。

断点 = `DiseaseVitalsService:260` 不传第七参 ⇒ `doseClampBound = null` ⇒ 不 clamp。

**为什么不能顺手传 `single_dose_max`** —— clamp 界 = **`MAX_ACTIVE_DOSE × single_dose_max`**,两因子:

| 因子 | 值 |
|---|---|
| `single_dose_max` | ✅ `32768`(产物派生) |
| `MAX_ACTIVE_DOSE` | ❌ **`待定`**(`entities.yaml:1755` 逐字;GDD `:1576` 同款) |

⇒ 只传 `single_dose_max` = **隐含把 `MAX_ACTIVE_DOSE` 当 1**,而这个假设**零出处**。

**⚠️ 且这不是「无害的保守估计」** —— 传单剂值 `32768` 会**截断多次给药**:
GDD `:874` 特意把界定为两因子相乘(而不是单剂值),正因为单剂界会误截叠加剂量。
⇒ 传它是**语义错误的界**,不是中间态。**保持 `null` 并登记,是当前唯一正确处置。**

> ⚠️ **别拿 `entities.yaml:1772`(`single_dose_max` 也写「待定」)当「它没值」的证据** ——
> 那是**登记表旋钮行**口径;它是**派生量、不由人定**(GDD `:795` 逐字「⚠️ **不由人定** —— 手填必漂移」)。
> **它有值,值是派生的。**

### 三、附带发现(不在本轮裁决面):运行期 `4-DC` 校验门不存在

```
$ grep -rn "InteractionKindTableValidator.Validate" unity/Assets --include=*.cs | grep -v "/Tests/"
unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs:128   ⇒ 生产面唯一命中 = 烘焙期
```

`AddressablesDataProvider` 对**任何**数据集**零语义校验** —— codec 只查魔数 / 版本 / 载荷长度 /
`AssertFullyConsumed`,**不查** kind 闭集 / 优先级互异 / `routes_to` 已登记 / `duration_owner` 合法 / `r_interact` 区间。

⇒ 若接上,可让 **5 个死字段**(含 2 个丙档)获得真的生产消费者,且它是**会 throw 的**消费者。
**但**:(a) 是全库第一个数据集运行期语义校验 = **新先例**;(b) **它不解除 §一 的阻塞** ——
校验器读字段 ≠ 4 在跑;`routes_to` 仍无**路由**消费者,只是多了**校验**消费者
(**不得**把「字段被校验」写成「字段被使用」)。**本轮登记,不立不裁。**

### 回归

**无回归可跑** —— 本件零代码 / 零数据 / 零产物改动。
末次全量 EditMode = `7d563a2` 轮的 `4012 / 3976 / 0 / 36`(截至 `2026-10-20 13:25 CST`)。
⚠️ **该数不得引用为「本轮的回归结果」** —— 它只是「登记前的最后已知基线」。

详见 `production/qa/evidence/blocked-data-core-tier-b-remaining-2026-10-20.md`。
