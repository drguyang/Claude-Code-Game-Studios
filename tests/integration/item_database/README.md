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
  `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs`,覆盖**盘上全部 9 份**。
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
| 覆盖 | `unity/Assets/DataCooked/` **盘上全部 9 份**产物(原 4/9) |

**为什么单立目录**:本门覆盖 8 个系统(2 物品 / 2 诊断 / 1 病例 / 2 音频 / 1 输入 / 1 遥测),
是跨 Story 的数据管线基建,**不属于 `ItemDatabase/`**。初版落在 `ItemDatabase/`(续写先例的顺位),
判据面与位置不符 ⇒ 迁独立目录。

### 三条判据 + 两个哨兵(13 用例)

| # | 测试 | 判据 |
|---|---|---|
| ① ×9 | `drift[<产物名>]`(`TestCaseSource`) | 盘上字节 **逐字节** = 该 baker 现烘输出 |
| ② | `test_sweep_noGhostArtifact_onDiskButUnregistered` | 盘上每一份都在两张表里(禁**幽灵产物**) |
| ②b | `test_sweep_declaredArtifactNames_allTriaged` | 源码声明的每个产物名都在两张表里(禁**新增 baker 忘了登记**) |
| ③a | `test_registry_noDuplicateOrCrossRegistration` | 两张表内零重复、零交叉、欠账项须写理由 |
| ③b | `test_registry_knownUnbakedCount_isPinned` | 欠账清单条数钉定为 **6**(防其长成永久豁免名单) |

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

### 回归对账(2026-10-20 · 全目录门落盘后)

| 项 | 值 |
|---|---|
| 命令 | `unity test <unity/> --mode EditMode --output <绝对路径>` |
| 本轮 | **`total=3967 passed=3931 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次(加全目录门前) | `3954 / 3918 / 0 / 36` |
| Δ | **`+13 / +13 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(13 条:9 漂移 + 2 扫描 + 2 登记表)⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 10:32 本地` · 批次 `全目录漂移门落盘轮(全量 EditMode)` ·
> commit `0a144f1` + 未提交工作树(1 测试文件迁目录 + 3 账本)。
> ⚠️ `+13` 是实跑出来的;前次 `3954` 亦实跑且带三元组(`2026-10-20 10:17`),可复算。

### ⚠️ 本门勘查中发现、**不在门裁决面**的事项(待裁)

**实测**:6 份产物有 baker、有菜单项、有测试,**从未落盘**(git 历史 **0 笔**):

`interaction_kinds` · `diagnosis_negative_confidence` · `prescription_actions` ·
`disease_action_axis` · `53_outcome_table` · `53_delay_table`

逐份查明:① 无任何代码从 `DataCooked/` 读它们;② `AddressablesDataProvider.Preload()`
恰装载盘上那 9 份 ⇒ **不在运行期面内**,故从未被发现。

**⚠️ 关键事实**:这 6 份所归属的 **5 个 story 全部标 `Complete`**,其中
`interaction-system/story-007-interaction-kinds-bake-wiring`(名字即「烘焙接线」)的 AC 明写
「全绿 ⇒ 写出 deterministic `interaction_kinds.cooked`」,而该产物 git **0 笔**。

⇒ 这不是「被裁为延后」,是**有 baker 无产物**的漏项。门只如实登记,**处置归用户裁定**
(补烘 / 承认为欠账 / 或判定相关 story 状态标早了)。

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


