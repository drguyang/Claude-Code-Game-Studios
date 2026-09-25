# unit/input_system/

按系统分目录的单元测试(命名 `[system]_[feature]_test.cs`)。
逻辑类故事的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

## Story 002(F-3.1 轴处理)—— 落点说明

故事头登记的证据路径 `tests/unit/input_system/axis_processing_test.cs` 为**登记口径**;
Unity 只编译 `unity/Assets/` 树 ⇒ 实际编译落点 =
**`unity/Assets/Tests/EditMode/InputSystem/axis_processing_test.cs`**(装配 `Sim.Contracts.Tests`)。
本目录只承载**夹具**:

| 内容 | 路径 |
|---|---|
| AC-3-A9④ 七组反例夹具(四组越界常量 + ∞ + NaN + float token) | `fixtures/*.json` |

测试经 `[CallerFilePath]` 上溯仓库根直读本目录(编辑期文件 I/O,不经 Addressables)。
