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
该增补属 **ADR-025 §⑤「测试装配族」**范围,**不动六装配清单**(§④ 封闭性不受影响)。

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

**测试计数**:**22 [Test] + 1 TestCaseSource × 30 夹具 = 52 个用例**
(31 夹具 − 1 跳过 `invalid_quality_dist.json` = 30)。EditMode **407 = 355 + 52**。

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
