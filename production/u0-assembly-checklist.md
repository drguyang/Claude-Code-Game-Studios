# U0 装配清单 · 工程根 + ADR-025 六装配

> **Status**: OPEN(工程根已裁 **2026-09-22 用户裁定 = 乙案 `unity/` 子目录**;本卡 = U0 的可执行形态)
> **前置**:U0a 工具链已闭合(`production/u0a-toolchain-checklist.md` · Unity `6000.3.24f1`)
> **权威件**:`docs/architecture/adr-025-contract-assembly-manifest.md`(六装配裁决)+ ADR-017 §二(门 A)
> **执行分工**:**【超算】**起草文本 + 提交 · **【桌面】**建工程根 / 落 asmdef / 跑 Unity / 跑 UTF

---

## §0 工程根裁定(2026-09-22 用户裁定)

**工程根 = 仓库根下的 `unity/` 子目录。** 承 ADR-014「作者态 / 出货态分离」哲学。

| 候选 | 结论 |
|---|---|
| 甲 仓库根 | ❌ 否决 —— `assets/`(小写,已存在)与 Unity 强制的 `Assets/`(大写)在 mac / Windows **大小写不敏感**文件系统上冲突;ADR-012 的 Apple Silicon 格会踩到 |
| **乙 `unity/` 子目录** | ✅ **采纳** —— 无大小写冲突 · 根目录既有结构(`design/` `docs/` `production/` `assets/`)不动 · 作者态与 Unity 工程物理分离 |
| 丁 仓库根 + 吞并 `assets/` | ❌ 否决 —— 改 `assets/data/` 路径 = ADR-014 / ADR-022 的导出契约全线重写 |

### §0.1 路径映射表

| ADR-025 概念 | 落地路径 | 说明 |
|---|---|---|
| 工程根 | `unity/` | Unity Hub 打开的就是这个目录 |
| `Assets/` | `unity/Assets/` | Unity 强制 |
| `Packages/` | `unity/Packages/` | Unity 强制,`manifest.json` + `packages-lock.json` **须提交** |
| `ProjectSettings/` | `unity/ProjectSettings/` | Unity 强制,**须提交** |
| 装配 `Sim` | `unity/Assets/Sim/` | asmdef 名 = `Sim`(ADR-025「不改名」) |
| 装配 `Sim.Contracts` | `unity/Assets/Sim.Contracts/` | |
| 装配 `Sim.Codec` | `unity/Assets/Sim.Codec/` | |
| 装配 `Gameplay.Presentation` | `unity/Assets/Gameplay.Presentation/` | |
| 装配 `Gameplay.UI` | `unity/Assets/Gameplay.UI/` | |
| 装配 `Editor.Tools` 族 | `unity/Assets/Editor.Tools.Level/` · `unity/Assets/Editor.Tools.Kindgen/` | 两具名 asmdef,均限 `Editor` |
| 测试装配 | `unity/Assets/Tests/EditMode/` · `unity/Assets/Tests/PlayMode/` | **见 §0.2** |
| 作者态数据源 | `assets/data/*.json`(维持原位) | 承 ADR-014 / ADR-022,不迁入 `Assets/` |

### §0.2 ⚠️ ADR-025 §⑤ 测试路径需修订(登记,本轮不改 ADR-025)

ADR-025 §⑤ 字面写 «`tests/EditMode` → `Sim.Contracts.Tests`»、«`tests/PlayMode` → `Gameplay.Tests`»。
**该字面路径在乙案下不成立** —— Unity **只编译 `Assets/` 之内的测试装配**,仓库根的 `tests/` 不在其中。

- **修订口径(本卡执行)**:两个 UTF 装配落 `unity/Assets/Tests/{EditMode,PlayMode}/`;
  仓库根 `tests/` 保留为**非 Unity 产物**(`evidence/` / `smoke/` / `integration/` README)。
- **种子测试随迁**:`tests/unit/sim/sim_fixedpoint_test.cs` → `unity/Assets/Tests/EditMode/Sim/sim_fixedpoint_test.cs`
- **登记残留**:ADR-025 §⑤ 原文待回写加注(归回写轮,承「不追改原文」惯例)。

---

## §1 六装配 asmdef 全文(可直接落盘)

> ADR-025 §①「**不改名**」⇒ `name` 字段逐字照抄裁决名。
> `autoReferenced: false` 全场统一 —— 理由:ADR-017:173 自己点名 `autoReferenced` 是**门 A 的漏洞**
> (「未显式列 `references` 时仍可能被自动解析进来」);关掉它 + 显式 `references`,才把「谁引用谁」
> 变成**声明事实**而非隐式解析结果。

### 1.1 `unity/Assets/Sim/Sim.asmdef`

```json
{
    "name": "Sim",
    "rootNamespace": "DaYiJingCheng.Sim",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

### 1.2 `unity/Assets/Sim.Contracts/Sim.Contracts.asmdef`

```json
{
    "name": "Sim.Contracts",
    "rootNamespace": "DaYiJingCheng.Sim.Contracts",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

### 1.3 `unity/Assets/Sim.Codec/Sim.Codec.asmdef`

```json
{
    "name": "Sim.Codec",
    "rootNamespace": "DaYiJingCheng.Sim.Codec",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

配套 `.cs`(承 ADR-025 §① `InternalsVisibleTo` —— **这行走代码不走 asmdef**):

```csharp
// unity/Assets/Sim.Codec/AssemblyInfo.cs
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
```

### 1.4 `unity/Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef`

```json
{
    "name": "Gameplay.Presentation",
    "rootNamespace": "DaYiJingCheng.Gameplay.Presentation",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> ⚠️ **`references` 起步 = 仅 `Sim.Contracts`**。URP / Input System / Addressables 的包引随各自
> 模块进 U0-b 逐个加(包名如 `Unity.RenderPipelines.Universal.Runtime` · `Unity.InputSystem`);
> **现不预填** —— 预填未安装的包会让 asmdef 报错。

### 1.5 `unity/Assets/Gameplay.UI/Gameplay.UI.asmdef`

```json
{
    "name": "Gameplay.UI",
    "rootNamespace": "DaYiJingCheng.Gameplay.UI",
    "references": [ "Sim.Contracts", "Gameplay.Presentation" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> **单向**:`Gameplay.UI → Gameplay.Presentation` **允许**;反向 **禁止**
> (承 ADR-013 焦点单栈门 —— 分装配是它的编译期表达)。反向引用 = §3 的封闭性断言应捕获。

### 1.6 `unity/Assets/Editor.Tools.Level/Editor.Tools.Level.asmdef`

```json
{
    "name": "Editor.Tools.Level",
    "rootNamespace": "DaYiJingCheng.EditorTools.Level",
    "references": [],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 1.7 `unity/Assets/Editor.Tools.Kindgen/Editor.Tools.Kindgen.asmdef`

```json
{
    "name": "Editor.Tools.Kindgen",
    "rootNamespace": "DaYiJingCheng.EditorTools.Kindgen",
    "references": [],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> `includePlatforms: ["Editor"]` = **不进构建**(承 ADR-022 §① · ADR-025 §①)。
> ⚠️ `tools/level/` 与 `tools/kindgen/`(ADR-022 / ADR-024 记的路径)在乙案下**即上述两目录**;
> 原路径措辞归回写轮加注。

### 1.8 测试装配(**用 Unity 的生成器,勿手写**)

> ⚠️ **2026-09-22 勘误**:本卡初稿曾给 `Sim.Contracts.Tests` 手写 `noEngineReferences: true` +
> 漏 `UnityEngine.TestRunner` 引用 —— **那是错的,会编译失败**。UTF 的测试装配**必须**引用
> `UnityEngine.TestRunner`(它本身就是 UnityEngine 装配)⇒ 与 `noEngineReferences: true` 直接冲突。
> 测试装配的必需引用集**随编辑器版本变化**,手写必漏。**改为让 Unity 自己生成。**

**【桌面】操作**:在 Project 窗口 → `Assets/Tests/EditMode` 上右键 →
**Create → Testing → Tests Assembly Folder**(命名 `EditMode`);对 `Assets/Tests/PlayMode` 同样操作。
Unity 会生成带全部必需引用的 asmdef。生成后**只改两处**:

| 改什么 | 改成 |
|---|---|
| `"name"` | `Sim.Contracts.Tests`(EditMode)· `Gameplay.Tests`(PlayMode) |
| `"references"` | 在 Unity 自动填好的基础上,**追加** `Sim` / `Sim.Codec`(EditMode)或 `Sim.Contracts` / `Gameplay.Presentation` / `Gameplay.UI`(PlayMode) |

> **`rootNamespace`** 手填 `DaYiJingCheng.Tests.Unit.Sim`(EditMode)—— Unity 生成器留空,须补。
> **其余字段一律不动**(含 `includePlatforms` / `precompiledReferences` / `defineConstraints`)——
> 那些正是「随版本变化」的部分,改了就是踩坑。

#### 附:预期生成的形状(仅供参考,**以 Unity 实生成为准**)

```json
{
    "name": "Sim.Contracts.Tests",
    "rootNamespace": "DaYiJingCheng.Tests.Unit.Sim",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Sim",
        "Sim.Contracts",
        "Sim.Codec"
    ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> **`noEngineReferences` 必为 `false`** —— 这是本勘误的核心。
>
> **⇒ 「测试代码不得碰 `UnityEngine`」这条纪律因此降级**:它**不再由 asmdef 强制**,降为
> **评审级规则**。需要 `UnityEngine` 的测试应归 `Gameplay.Tests`(PlayMode),不塞进 EditMode 装配。
> **登记**:此为 §4 D7,若日后要硬化,须另寻机制(如 Roslyn 分析器 / 构建期扫描),**不在本卡范围内**。

---

## §2 U0-a 切分(首批可交付 · **只求种子测试转绿**)

| # | 动作 | 系统 |
|---|---|---|
| a1 | 建 `unity/` 工程根(Unity Hub 建 **空白 URP** 工程,命名任意;移到仓库根的 `unity/`) | 【桌面】 |
| a2 | 清掉模板自带示例场景内容(保 `Assets/` 最小;承 ADR-023 §②「零 gameplay 对象」取向) | 【桌面】 |
| a3 | 落 §1.1–1.8 的 **8 个 asmdef + `AssemblyInfo.cs`**(空目录亦可,asmdef 先行) | 【桌面】 |
| a4 | 迁移种子测试:`tests/unit/sim/sim_fixedpoint_test.cs` → `unity/Assets/Tests/EditMode/Sim/` | 【桌面】 |
| a5 | Unity 导入 → Test Runner 跑 EditMode → **种子测试首次转绿** | 【桌面】 |
| a6 | 提交 `ProjectSettings/` · `Packages/`(含 lock) · 全部 `*.meta` · asmdef | 【桌面】commit → 【超算】pull(或反向亦然) |

**U0-a 验收**:种子测试在 `Sim.Contracts.Tests` 下**编译通过且跑绿**;`git status` 无未跟踪的 `.meta`。

## §2.1 U0-b 后续(不在本批)

| # | 动作 | 承 |
|---|---|---|
| b1 | `Sim.Contracts` 类型本体(`WorldPos` · 六抽象点 · `SimEvent`/`PatientId`/`StreamId`/`EventKind` · `VitalsDto` · `Fix`+`FixParse` · `IDataProvider` · `AudioCueDto`+`IAudioCueSink` · `IPositionalChannel` · `ClinicEnvDto`+`IClinicEnvQuery`) | ADR-005 · §2.2 成员清单 |
| b2 | 门 A 白名单断言(构建期读 `GetReferencedAssemblies()`,Sim 引用集 ≠ {BCL, Sim.Contracts} = 失败) | ADR-017 §二 · ADR-025 §① 注 |
| b3 | 装配封闭性断言(工程内 asmdef 集合 = §1 清单 ∪ 测试族) | ADR-025 §④ |
| b4 | `ToFloat()` 调用点扫描(`Sim` 内出现 = 失败) | ADR-025 §② 甲案 · V-4 |
| b5 | 种子测试去掉自带 `Q16Codec`,改指生产类型 | 种子测试文件头 ⚠️1 |
| b6 | `tools/kindgen/` 生成器 + `StreamRouting.g.cs` | ADR-024 §⑤ · D-R2 |
| b7 | `Gameplay.Presentation` / `Gameplay.UI` 包引(URP / Input System / UI Toolkit / Addressables) | ADR-014 · ADR-013 |

---

## §3 验收判据(承 ADR-025 §Validation,禁借绿)

| # | 判据 | 落点 |
|---|---|---|
| V-1 | 工程内 asmdef 集合 = §1 清单 ∪ 测试族 | b3 断言 |
| V-2 | `Sim` 引用集实测恰 = {BCL, `Sim.Contracts`};含任何 `UnityEngine.*` = 构建失败 | b2 断言 |
| V-3 | 种子测试在 `Sim.Contracts.Tests` 下**编译并首跑**(ADR-012 矩阵内) | a5 |
| V-4 | `ToFloat()` 调用点扫描在 `Sim` 内出现 = 失败样例一条 | b4 |
| V-5 | 全库 grep「门面程序集 / 独立契约程序集」命中处均带「现名」注 | 回写轮 |
| V-6 | ADR-017 `"references": []` 歧义的订正记录挂讫 | ✅ 已就地加注(`adr-017:262`) |

---

## §4 需用户裁决的残留点(本卡已取推荐默认值,可推翻)

| # | 点 | 本卡取值 | 备选 |
|---|---|---|---|
| D1 | 命名空间根 | **`DaYiJingCheng`**(种子测试已在用;全仓唯一出现处) | 无根 / 换名(须先改种子测试) |
| D2 | 测试装配路径 | **`unity/Assets/Tests/`**(§0.2) | 字面守 ADR-025 §⑤(须先证明 Unity 能编译根 `tests/`) |
| D3 | `autoReferenced` | **全场 `false`** | `true`(Unity 默认,显式引用面变松) |
| D4 | `unity/` 内的文档 | 根 `CLAUDE.md` 不动;`src/CLAUDE.md` 的引擎标准迁 `unity/CLAUDE.md` | 保留 `src/` 双份 |
| D5 | `src/` 目录去留 | **退役**(代码归 `unity/Assets/`);`.gitkeep` 删 | 保留为纯文档位 |
| D6 | 工程模板 | **空白 URP** + 清示例内容 | 3D Core 模板 + 事后加 URP 包 |
| D7 | 测试装配纪律 | 「EditMode 测试不得碰 `UnityEngine`」**降为评审级**(§1.8 勘误:asmdef 无法强制) | 另寻硬化机制(分析器 / 构建扫描)—— 非本卡范围 |

---

## §5 CI 关联

- `PROJECT_PATH` 已由 `.` 改为 **`unity`**(`.github/workflows/tests.yml:29`)。
- ⚠️ **CI 在 U0-a 完成前仍红**,且是**预期状态**(承 `tests.yml:9-14` 的四条未解条件):
  ① 工程根刚落 → ② asmdef 刚落但 UTF 装配未绿 → ③ `UNITY_LICENSE` secret 未配 → ④ 黄金夹具未签。
- **U0-a 完成只解 ①②**;③④ 分别在 U0-b 与夹具轮。

## §6 队列

```
U0a ✅ CLOSED → U0-a(本卡 §2:空装配 + 种子测试转绿)
              → U0-b(§2.1:类型本体 + 三断言 + kindgen)
              → U1 spike 批:R-A 手柄焦点桥 · R-B 门 A · R-C int64 溢出 UB
                 · OQ-1-12 · ADR-023 S1/S3/S4 · 10 的两动作手感原型
```
