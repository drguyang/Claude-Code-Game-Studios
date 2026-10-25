# TD-persist-05 动作 ② 实现证据 —— AC-7a-09 忘词令(规则七判据层)

**日期**:2026-10-25 · **commit 基准**:`19626d5` 工作树(未提交)· **承接**:`impl-td-persist-05-act1-2026-10-25.md`(动作 ①)

---

## 〇 范围与用户裁定记录

用户 2026-10-25 裁定(AskUserQuestion 回答原文):**「修 ② AC-7a-09(推荐), 开 ④ 裁定轮(TD-persist-04 ①)」**。
本件只覆盖 **②**;④ 裁定轮材料另行呈报。

**载体前提变化(本条解除 blocked 的依据,须留痕)**:
- **原 blocked 理由**(2026-10-25 早轮用户裁定「如实标 blocked」):`ADR-012` 已裁「载体 = CI 门」,
  而 CI 矩阵本身 `NOT-RUN`(blocked-by `TD-ci-01`);单独落扫描器 = **没有东西会跑它** ⇒ 记为「有载体」即借绿。
- **前提已变**:`AssemblyGates.RunAll` 现承载持久化门族(`SaveWritePathGate` / `SaveAsyncBoundaryGate`),
  且有**三处强制执行点**(菜单 / reload / 构建前门)⇒ 本门挂 RunAll 即有东西跑它。
- **与 ADR-012 不互斥**:CI 矩阵(TD-ci-01)落地时再挂一层同款判据;两者是叠加不是替换。

## 一 AC 语义回顾(权威来源)

`design/gdd/persistence-service.md:527-532`(AC-7a-09 原文):
> GIVEN 全仓符号表,WHEN 判据端扫描 `quicksave` / `quickload` / `save-now-slot` 类标识符,
> THEN 零命中(扫描集与假阳性白名单见 AC-7a-04)

规则七**双守**(`:142-150`):代码层 = `ISaveService` 接口签名反射(已有夹具
`folded_predicate_test.cs:655`);判据层 = **忘词令符号扫描**(本件)。
`OQ-7a-2`(✅ 2026-09-16 已裁):载体原裁 ADR-012 CI 门 —— 本轮为**前提变化后的本地强制点**兑现。

## 二 兑现落点(复算命令)

| 件 | 落点 | 复算 |
|---|---|---|
| **门本体** | `unity/Assets/Editor.Tools.Gates/SaveVocabularyGate.cs` | `grep -c "ForbiddenTerms" unity/Assets/Editor.Tools.Gates/SaveVocabularyGate.cs` ⇒ ≥5 |
| **RunAll 接线** | `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`(RunAll 内 `errs.AddRange(SaveVocabularyGate.RunAll())`) | `grep -n "SaveVocabularyGate.RunAll" unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` ⇒ ≥1 |
| **接线清单登记** | `unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs`(EntryWiringManifest + ClassVerdict 双处) | `grep -c "SaveVocabularyGate" unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs` ⇒ ≥2 |
| **夹具** | `unity/Assets/Tests/EditMode/Sim/save_vocabulary_gate_test.cs` **5 条** | Unity Test Runner:`-testFilter DaYiJingCheng.Tests.Unit.Sim.SaveVocabularyGateTest` ⇒ total=5 passed=5 |

**扫描形态 = Cecil 符号名扫描,不是源码文本 grep**:Cecil 只见**标识符**
(类型 / 方法 / 字段 / 属性 / 事件名),注释与字符串天然不可见
⇒ AC-7a-04「注释 / 字符串字面量假阳性由白名单防误伤」**结构性为零**,不需要白名单表。
GDD `:144`「断言**符号表**不含」的字面即此。

**三重零覆盖守卫**(承 MA20「空清单逃逸」教训):
① 词表空 ⇒ 红;② 扫描装配集空 ⇒ 红;③ dll 缺失 / 扫到 0 类型 ⇒ 红。
词表**内容钉死**在夹具(规则七点名的三个词逐个 `Contains`,不只验非空)。

**负例的可复算性**:夹具用 `AssemblyDefinition.CreateAssembly` **内存注入**一个含
`Do`+触发词+`Now` 方法的探针类型 —— 不改仓库源码、不写盘,即证明「门真的会红」;
对照组(干净探针装配)防「门对任何东西都红」的假门。

## 三 asmdef 踩坑记录(费三轮才通,防复发)

**现象**:测试装配 `Sim.Contracts.Tests` 编译报 CS0234(`Mono.Cecil` 不存在),生产装配零问题。

**排查实测(非文档,全为 rsp / 产物级证据)**:
1. 全仓**非测试装配**(`Sim` / `Sim.Codec` / `Editor.Tools.Gates` / VisualScripting / Timeline / TextMeshPro …)
   的 rsp 全部含 `-r:"…/PackageCache/com.unity.nuget.mono-cecil@…/Mono.Cecil.dll"` ——
   来源是 dll 插件的 **auto-reference**(`Mono.Cecil.dll.meta` 的 `isExplicitlyReferenced: 0`,
   Editor-only 平台启用)。
2. **四棵用户测试装配树**(`Sim.Contracts.Tests` / `Casebook.Tests` / `Gameplay.Tests` /
   `SkeuomorphicUI.Tests`)rsp 计数**全部为 0** —— 测试装配被排除在该 auto-reference 之外,
   与 asmdef 写法无关。
3. 显式引用包 asmdef `Unity.Nuget.Mono-Cecil`:**按名字**(第一次)与**按 GUID**
   `aaf6804a66b394e9b8c133f80fc0c3e3`(第二次)**均被静默丢弃** —— rsp 零变化、日志零警告。

**解**(plan B,语义明确的机制):测试 asmdef 自持
`"overrideReferences": true` + `"precompiledReferences": ["nunit.framework.dll", "Mono.Cecil.dll"]`。
⚠️ **nunit 必须一并显式列出** —— `overrideReferences` 关掉 auto-reference 后,
既有 nunit 引用会一起断掉(全部测试的 `NUnit.Framework` using 会编译失败)。

## 四 首验

| 轮 | 结果 |
|---|---|
| 第 1 次 | CS0234(名字引用被丢)|
| 第 2 次 | CS0234(同上 —— rsp 未变,确认静默丢弃)|
| 第 3 次 | CS0234(GUID 引用同样被丢)|
| **第 4 次(plan B)** | **EXIT=0;`SaveVocabularyGateTest` 5/5 绿 + `AssemblyGateRunAllTest` 2/2 绿 + `gate_wiring_manifest_test` 5/5 绿,零失败** |

命令(自包含):
```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$HOME/.claude/jobs/181888b1/tmp/vocab-first.xml" \
  -logFile "$HOME/.claude/jobs/181888b1/tmp/vocab-first.log" \
  -testFilter "DaYiJingCheng.Tests.Unit.Sim.SaveVocabularyGateTest|DaYiJingCheng.Tests.Unit.Sim.AssemblyGateRunAllTest|DaYiJingCheng.Tests.Sim.gate_wiring_manifest_test"
# 预期:EXIT=0;XML 内 total="7" passed="7"(门 5 + RunAll 2)与 total="5" passed="5"(接线清单)
```

## 五 证伪轮(4 变异,驱动器 `falsify_vocab.py`)

纪律:每条变异 = 「改 → 跑 → 还原」同进程完成;还原判据 = SHA-256 逐字节;
filter 覆盖判据所在 fixture(MA11 教训)。

| 变异 | 内容 | 结果 |
|---|---|---|
| MV1 | 清空 `ForbiddenTerms` | **红 4**(notEmpty / greenOnAll / injected_isRed / cleanProbe)|
| MV2 | 清空 `RuntimeAssemblies` | **红 2**(notEmpty / greenOnAll)|
| MV3 | 词表失真(`quicksave` → 假词) | ⚠️ **首跑绿(假绿)→ 修夹具后红 1**(见下)|
| MV4 | 去掉 `ToLowerInvariant` | **红 1**(injected_isRed —— 探针是大写形态)|

全部还原:`SaveVocabularyGate.cs` SHA-256 = `bb2c8d368a248a62…`(逐字节)。

### MV3 首跑绿 = 夹具自指(第五假绿族·自指判据的新子形态)

**根因**:探针成员名从 `SaveVocabularyGate.ForbiddenTerms[0]` **派生** ⇒
词表被换成假词时,探针名跟着变成假词 ⇒ 照样命中 ⇒ 零红逃逸。
**变异同时改了判据的两边** —— 与 W1 / W4 同族(自指判据),第 4 个实例子形态:
「断言引用被测类的**常量数组**」。

**修法(两全解)**:
1. 判据改**源码级拼接字面量**:`"Do" + ("quick" + "save").ToUpperInvariant() + "Now"` ——
   既与被测常量解耦,又保住「夹具源码不含连续触发词字面」的自约束;
2. 词表**内容钉死**:`test_vocabularyGate_forbiddenTerms_notEmpty` 从「只验非空」升为
   三个触发词逐个 `Contains`(用 `"quick"+"save"` 拼接字面量比对)——
   词表失真时**守卫自检自身**即转红。

**复跑**:干净基线 5/5 绿(`vocab-clean2`);MV3 红 1(`forbiddenTerms_notEmpty`)✓。

## 六 回归(全量 EditMode)

- 基线:`4966 / 4931 / 0 / 35`(`td07-3` · 2026-10-25 08:39 UTC · commit `3ca1920` 工作树)。
- 本轮:**`4971 / 4936 / 0 / 35`**(`td09` · `2026-10-25T09:12 UTC` · commit `19626d5` 工作树,未提交)。
- 等式:`4971 − 4966 = 5` = 恰好 `SaveVocabularyGateTest` 5 条新增
  (`gate_wiring_manifest_test` 只加登记项不加测试)✓ **零回归**。
- 复算命令:
  ```bash
  python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('$HOME/.claude/jobs/181888b1/tmp/td09-full.xml').getroot(); print(r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))"
  # 预期输出:4971 4936 0 35
  ```

## 七 缺口与边界(如实登记)

1. **`save-now-slot` 的词表收录形态是判断,不是逐字**:`-` 非合法标识符字符,
   标识符中只可能是 `SaveNowSlot` 拼写 ⇒ 以 `savenow` 收录 + flat 化(去 `-`/`_` + 小写)。
   若未来出现**跨命名空间分段模拟连字符**的形态(如 `Save.Now` 类型嵌套),Cecil 逐成员扫描
   **不跨段拼接** —— 该形态当前不存在,如实登记为扫描面边界。
2. **`AssemblyGates.AllTypes` 的嵌套类型覆盖**:门复用既有枚举器(与 SaveWritePathGate 同款);
   嵌套类型的覆盖面由该枚举器决定,本轮未单独证伪嵌套类型注入(负例用顶层类型)。
3. **非运行期装配不在扫描集**:`Editor.Tools.*`(编辑期)与测试装配不在 `RuntimeAssemblies`
   —— AC 原文「全仓符号表」的字面大于本轮扫描集;收窄理由 = 门的本义是
   **玩家可触达的运行期面**不得出现 quicksave 语义;编辑期工具自身不含存档语义。
   若用户裁定须扩到全仓,改 `RuntimeAssemblies` 一处即可(有守卫 ② 钉住非空)。
4. **ADR-012 CI 腿未落**:CI 矩阵(TD-ci-01)落地时须挂同款判据(文件头已写明叠加关系)。
5. **`o` 探针仅覆盖方法名**:负例注入的是方法;字段 / 属性 / 事件分支与 `type.Name` 分支
   走同一 `CheckOne`,判别力共享,但独立负例未逐一落(4 条变异覆盖的是门级守卫与归一逻辑,
   不是五种成员形态各自的红)。

---

**结论**:AC-7a-09 判据层(忘词令)落地且有强制执行点,首验绿 + 证伪轮 4 变异全红
(含 MV3 假绿发现与修复)。**代码层半守(接口反射)与判据层(本门)合流,规则七双守齐。**
