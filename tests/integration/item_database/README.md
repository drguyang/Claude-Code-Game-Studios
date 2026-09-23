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
