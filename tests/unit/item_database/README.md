# unit/item_database/

按系统分目录的单元测试(命名 `[system]_[feature]_test.cs`)。
逻辑类故事的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

## Story 001(FixParse 边界契约)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/unit/item_database/fix_parse_boundary_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**。按 ADR-025 §⑤(2026-09-23 路径订正注),
实际编译落点 = **`unity/Assets/Tests/EditMode/ItemDatabase/fix_parse_boundary_test.cs`**
(装配 `Sim.Contracts.Tests`)。本目录只承载**账本与夹具**:

| 内容 | 路径 |
|---|---|
| 负向夹具(故事 QA 指定) | `fixtures/invalid_potency_float.json` |
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/fix_parse_boundary_test.cs` |

与 `tests/unit/sim/` 的既有先例一致(种子测试已由 U0 迁入 EditMode 树,根目录不留副本)。

## Story 002(Schema 类型与复合主键)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/schema_types_primary_key_test.cs`,
同 Story 001:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本与三个 QA 指定负向夹具:

| 内容 | 路径 |
|---|---|
| 负向夹具:复合主键重复(AC-21a-21) | `fixtures/invalid_dup_key.json` |
| 负向夹具:枚举外字面量(AC-21a-22) | `fixtures/invalid_enum.json` |
| 负向夹具:显式存储 stackable(AC-21a-59) | `fixtures/invalid_stored_stackable.json` |
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/schema_types_primary_key_test.cs` |
| 类型图违例夹具(仅测试程序集可见) | `unity/Assets/Tests/EditMode/ItemDatabase/invalid_instance_unity_ref.cs` |

**落点(unity-specialist 约束①,2026-09-23)**:schema **类型**住
`unity/Assets/Sim.Contracts/ItemDatabase/`(12 个文件,恰 = BCL);
校验/扫描**纯函数**住 `unity/Assets/Editor.Tools.Gates/ItemDbValidation.cs` 与
`PodTypeScanner.cs`(编辑期职责,不进玩家构建)。两处 asmdef 只改 `references` 数组:
Gates 增引 Sim.Contracts GUID,EditMode 增引 Gates GUID。
