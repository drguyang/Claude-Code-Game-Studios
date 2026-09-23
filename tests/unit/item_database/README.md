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
