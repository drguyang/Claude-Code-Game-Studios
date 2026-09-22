# U0 装配清单 · 工程根 + ADR-025 六装配

> **Status**: **U0-a ✅ CLOSED(2026-09-22 桌面实测 + 超算复核 · 闭于 `7772e3d`)**;
> **U0-b 待启**;§6.1 登记本轮实测勘误四条。
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
- **登记残留**:ADR-025 §⑤ 原文待回写加注(归回写轮,承「不追改原文」惯例)。✅ **2026-09-23 回写轮已落**(ADR-025 §⑤ 节首已挂 U0 路径订正注)。

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
> 原路径措辞归回写轮加注。✅ **2026-09-23 回写轮已落**(ADR-022 §一 程序集行 + ADR-024 §⑤ 节首均已挂 U0 路径订正注)。

### 1.8 测试装配(照抄生成器形状 · **勿叠加 `references`**)

> **2026-09-22 二轮勘误(实地实测推翻初稿)**。初稿两处都错:
> ① 手写 `noEngineReferences: true` + 漏 `UnityEngine.TestRunner` 引用 —— 会编译失败;
> ② 「生成后只改 `name`/`references` 三处」的**叙述与 6.3 实生成的形状不符** ——
> 生成器**根本不写 `references` / `precompiledReferences` / `noEngineReferences`**。
>
> **实测形状(桌面机 6.3 实生成,2026-09-22)**:
> ```json
> { "name": "Tests", "optionalUnityReferences": ["TestAssemblies"], "includePlatforms": ["Editor"] }
> ```
> 用的是**老机制 `optionalUnityReferences: ["TestAssemblies"]`**(Unity 自动挂测试框架引用),
> 不是 §1.8 初稿假设的「显式列 `UnityEngine.TestRunner`」。
>
> **⇒ 本卡自此收窄为「照抄生成器形状,只改名与命名空间」** —— 不改它认识的结构,不叠加引用。

**【桌面】操作**(与初稿一致):

1. 打开 `Window → General → Test Runner`
2. 点 **`Create EditMode Test Assembly Folder`** → 检查落在 `Assets/Tests/EditMode`
3. 点 **`Create PlayMode Test Assembly Folder`** → **须落在 `Assets/Tests/PlayMode`**

> ⚠️ **实测坑(2026-09-22)**:PlayMode 那次**没有生成 `.asmdef`**,只生成了 `PlayMode.cs`
> ⇒ `PlayMode.cs` 掉进默认的 `Assembly-CSharp` 装配 ⇒ 报
> `CS0246: The type or namespace name 'UnityTest' could not be found`。
> **每次生成后必须 `ls` 确认 `.asmdef` 真的存在**,不能假定按钮生效了。

生成后**只改 `name` 与补 `rootNamespace`,并把 PlayMode 的 `includePlatforms` 清空**(其余字段逐字保留):

**`unity/Assets/Tests/EditMode/EditMode.asmdef`**

```json
{
    "name": "Sim.Contracts.Tests",
    "rootNamespace": "DaYiJingCheng.Tests.Unit.Sim",
    "optionalUnityReferences": [ "TestAssemblies" ],
    "includePlatforms": [ "Editor" ]
}
```

**`unity/Assets/Tests/PlayMode/PlayMode.asmdef`**(若生成器漏建,手工新建此文件)

```json
{
    "name": "Gameplay.Tests",
    "rootNamespace": "DaYiJingCheng.Tests.PlayMode",
    "optionalUnityReferences": [ "TestAssemblies" ],
    "includePlatforms": []
}
```

#### ⚠️ 两条刻意的取舍(与 ADR-025 §⑤ 的次序差异)

① **不叠加 `references`** —— U0-a 用不上:种子测试**自包含**(自带 `Q16Codec`,只依赖 `System` + `NUnit`),
   不碰任何 Sim 类型;且 `Sim` / `Sim.Codec` 目录**此刻是空的**,引用空装配引入新风险面。
   **ADR-025 §⑤ 要的那组引用归 U0-b**(等 `Sim` 内有真类型了再加)—— 自愈型依赖,不是欠账。
② **`noEngineReferences` 不写** —— 生成器没给,`optionalUnityReferences: ["TestAssemblies"]` 机制下
   测试装配本就引用 UnityEngine ⇒ 「EditMode 测试不得碰 `UnityEngine`」**无法由 asmdef 强制**,
   降为**评审级规则**(承初稿登记,§4 **D7**)。

#### 验收自检(落盘后跑)

```bash
find unity/Assets -name '*.asmdef' | sort
# 期望 9 行:7 个 §1.1–§1.7 + Tests/EditMode + Tests/PlayMode
python3 -c "import json; [json.load(open(p)) for p in __import__('glob').glob('unity/Assets/**/*.asmdef', recursive=True)]; print('all asmdef JSON valid')"
```

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

### §2.0 U0-a 实测记分(2026-09-22,桌面机 Unity `6000.3.24f1` + 【超算】库复核)

| # | 动作 | 结果 | 证据 |
|---|---|---|---|
| a1 | 建 `unity/` 工程根(空白 URP) | ✅ | `unity/ProjectSettings/ProjectVersion.txt` = `6000.3.24f1` |
| a2 | 清模板示例 | ⚠️ **部分 —— 见 §6.1 ④** | `Settings/` 7 资产**全部必留**;残留仅 `SampleScene.unity` + `Readme.asset` |
| a3 | 落 8 asmdef + `AssemblyInfo.cs` | ✅ | 实测 **9 个 asmdef**(7 六装配 + 2 测试族)+ 1 `AssemblyInfo.cs` |
| a4 | 迁移种子测试 | ✅ | `git` 记为 `R  tests/unit/sim/… → unity/Assets/Tests/EditMode/Sim/…`(纯移动,内容零改) |
| a5 | 跑 EditMode | ✅ | **16 绿** = 源码 `[TestCase]×14 + [Test]×2` 精确吻合 |
| a6 | 提交工程本体 | ✅ | `c398507`(迁移)+ `7772e3d`(工程根);`.slnx` / `unity/.vscode/` 经 `b1d7583` 正确忽略 |

**空装配未报错**(实测推翻起草时的担忧):`Sim` / `Sim.Contracts` / `Sim.Codec` 三目录**落 asmdef 但零 `.cs`**,Unity 导入**无任何报错**。

**V-3 口径拆分(承 §3 禁借绿)**:
- ✅ **编译** —— `Sim.Contracts.Tests` 编译通过(16 用例全部出现在 Test Runner)
- ✅ **首跑** —— 桌面机本地跑绿
- ⏳ **CI 矩阵内** —— **未完成**(`UNITY_LICENSE` secret 未配,见 §5 ③)⇒ **V-3 不得记全绿**

> **关于 PlayMode 2 绿 + Player 2 绿**:属 **URP 模板自带**(`unity/Assets/Tests/PlayMode/PlayMode.cs`),
> **不是**本项目的种子测试。用户裁定**保留**(它对称证明了 UTF 的 PlayMode 通路可用)。
> ⚠️ 不得把「EditMode 16 绿」与「PlayMode 2 绿」并读为「18 个种子测试通过」。


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
- ⚠️ **CI 在 U0-a 完成后仍红**,且是**预期状态**(承 `tests.yml:9-14` 的四条未解条件):
  ① ~~工程根刚落~~ **✅ 已解(U0-a)** → ② ~~asmdef 刚落~~ **✅ 已解(U0-a 实测 9 个 asmdef)** →
  ③ `UNITY_LICENSE` secret 未配 → ④ 黄金夹具未签。
- **U0-a 完成解了 ①②**;③④ 分别在 U0-b 与夹具轮。**`game-ci` 前两步现已具备启动条件**,
  首次 CI 跑会在 ③ 处失败(**预期**,不得读作测试缺陷)。

## §6 队列

```
U0a ✅ CLOSED → U0-a ✅ CLOSED(§2 · 桌面实测 + 超算复核 · 闭于 7772e3d)
              → U0-a′:a7 三场景落地(§6.2,待裁 —— 是 U0 缺口,见 §6.1 ④)
              → U0-b(§2.1:类型本体 + 三断言 + kindgen)
              → U1 spike 批:R-A 手柄焦点桥 · R-B 门 A · R-C int64 溢出 UB
                 · OQ-1-12 · ADR-023 S1/S3/S4 · 10 的两动作手感原型
```

## §6.1 本轮实测勘误(U0-a 执行期发现,均需回写)

| # | 勘误 | 原判断 | 实测 | 处置 |
|---|---|---|---|---|
| ① | **`.slnx` 未忽略** | `.gitignore` 有 `*.sln` 即够 | Unity 6.3 生成 **`unity.slnx`**,`*.sln` 不匹配 | ✅ `b1d7583` 补 `*.slnx` |
| ② | **`.vscode/` 漏网** | `.vscode/settings.json` 已忽略 | 该规则**带斜杠 = 根锚定**,匹配不到 `unity/.vscode/` | ✅ `b1d7583` 补 `unity/.vscode/` |
| ③ | **`src/` 有桌面机独有内容** | 假设两台一致 | 桌面 `src/new medicine/` 悬空(超算无);用户确认属误放,删除 | 登记:跨机 `git status` 差异须先辨来源 |
| ④ | **`Settings/` 无模板残留可清** | 起草假设「清示例内容」可从 `Settings/` 下手 | 7 资产**全部被引用**(`SampleSceneProfile` 被 PC/Mobile 两个 RPAsset 共同引用 —— **非 SampleScene 专属**)⇒ **全留** | 见 §6.2 |

### §6.1.1 ⚠️ 起草时误判一处,已就地订正

我(起草方)曾判 `SampleSceneProfile.asset`「随 SampleScene 走,可删」。**实测证伪**:
其 guid 被 `PC_RPAsset` + `Mobile_RPAsset` + `SampleScene` **三处**引用,是**两条管线共用的默认 Volume Profile**。
⇒ **删除引用扫描是硬前置,不得凭名判**。本条即 §6.2 a7 的前置方法。

## §6.2 U0-a′ 缺口:a7 三场景落地(登记 · 待用户裁决归属)

**缺口**:ADR-023 §① 裁决 **三场景制**(`Boot.unity` 常驻 / `MainMenu.unity` / `World.unity` additive),
而本卡 §2(a1–a6)与 §2.1(b1–b7)**均无建场景步骤** ⇒ 照卡走完 U0,工程里仍是模板的 `SampleScene`。

**实测现状**:
- `unity/ProjectSettings/EditorBuildSettings.asset:9` = `path: Assets/Scenes/SampleScene.unity`(**唯一场景**)
- `unity/Assets/Readme.asset` = **零引用**(扫描确认)⇒ 纯模板残留

**a7 建议步骤**(仅【桌面】,归 U0-a′ 还是 U1 **待裁**):
1. 建 `Boot.unity`:仅含主相机 + `AudioListener`(ADR-023 §③「相机与 AudioListener 住 Boot」);
   ⚠️ **P0 阶段可先空场景** —— tick driver / Addressables 引导属 U0-b/U1,不在本步
2. Build Settings 首场景位 → `Boot.unity`(顶掉 `SampleScene`)
3. `git rm unity/Assets/Scenes/SampleScene.unity{,.meta}`
4. `git rm unity/Assets/Readme.asset{,.meta}`
5. `MainMenu.unity` / `World.unity` —— **可留到需要时建**(ADR-023 §① 允许增量;
   但 `World.unity` 建时须过 §②「零 gameplay 对象」扫描)

**⚠️ 为什么不现在直接删 `SampleScene`**:它是 Build Settings 里**唯一**的场景。
删了又不建 `Boot.unity` ⇒ 工程失去「能进 Play 模式」状态,而 a5 的绿正是建立在该状态上。
**顺序必须是「先建 Boot 顶位,再删 Sample」**,不可颠倒。

