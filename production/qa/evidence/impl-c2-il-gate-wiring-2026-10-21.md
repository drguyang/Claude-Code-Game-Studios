# C-2 证据原件 —— 6 / 10 两个 IL 门接进 BuildGate

**日期**:2026-10-21
**任务**:`TD-gate-02` 收口的最后一笔(用户裁定「接 —— 复刻 b5 先例」)
**基线溯源三元组**:截至 `2026-10-21 01:38 CST` · 批次 `C-2 接线轮(GateWiringManifestTest)` · commit `33188fd`(工作树含本件未提交改动)

---

## 一、原判定 —— 以及**我自己写错的那一版**

`production/sprint-status.yaml` 的 `conditions_open` 最后一条:

> 「两条 BLOCKING 级 AC(AC-6-22 / AC-10-02·03)零构建期强制点。须另裁(接 = 改构建前门语义 + 须确认 Cecil 在 batch 下的 DLL 新鲜度 —— `OnPreprocessBuild` 跑时 `Library/ScriptAssemblies/*.dll` 是**上一版编译产物**,而测试侧已有 `EditorUtility.scriptCompilationFailed` 守卫、BuildGate 无等价守卫)。C-1 只做具名登记,未擅自接。」

### 1.1 🔴 我给用户的「不接」理由是错的

我在 2026-10-21 向用户提案时的措辞是:**「IL 级门不能进 BuildGate;BuildGate 无等价守卫 ⇒ 接 = 开假绿面」**。三条实测全部推翻:

| 我说的 | 实测 | 出处 |
|---|---|---|
| 「BuildGate 无等价守卫」 | `OnPreprocessBuild` **本就在调 IL 门** `RunAudioBoundary` | `AssemblyGates.cs:1230` |
| 「IL 级门不能进 BuildGate」 | `RunAudioBoundary` 就是 Cecil + `dllPath` 的 IL 门 | `AssemblyGates.cs:1151` |
| 「接 = 开假绿面」 | `scriptCompilationFailed` 守卫**就住在 `RunAudioBoundary` 内部** | `AssemblyGates.cs:1155` |

**真因**:守卫不是没有,是**这两个门没把守卫写在自己身上**(它们把守卫留给了测试调用方)。
故接线成本 = 把 b5 那段守卫抄进门自己的 `RunAll()`,**不是**「不可能」。

### 1.2 错误链(留痕)

债册 `§TD-gate 结案记录` 原文写的是:

> 「IL 级门(Cecil + 真 DLL)放不进 **reload 路径**(会读陈旧产物)—— 那不是缺陷是设计」

**这句是对的**:`RunAll` 随域重载触发,那时 `Library/ScriptAssemblies/*.dll` 必然是上一版。
而我把它读成了「放不进 **BuildGate**」—— 但 `OnPreprocessBuild` 恰恰是**编译完成后**才跑,
是全案**最适合**放 IL 门的位置。

**更糟的一处**:`gate_wiring_manifest_test.cs` 的 `ClassVerdict` Note 里**本来就写着**
「IL 门…放不进 reload 路径(会读陈旧产物),但**可**进 BuildGate —— 现未接」。**我也没读到。**

> ⚠️ **与 `TD-gate-03` 同族,方向相反**:那一次是**登记描述本身错**(驱动根本没有残留词表);
> 这一次是**我把登记读错**。两次都靠「打开代码看」纠正。
> ⇒ **登记的措辞要验证,而我读登记的方式同样要验证。**

---

## 二、修复落点

### 2.1 门侧:守卫住进门自己(复刻 b5,不新造机制)

| 文件 | 新增 |
|---|---|
| `EcozoneIntegerGates.cs` | `RunAll(out List<string> closureMembers)` + `RunAll()`;`+using UnityEditor` |
| `EmergencyIntegerGates.cs` | `RunAll(out int scannedMethods)` + `RunAll()`;`+using UnityEditor` |

```csharp
// 两门同构。以 Ecozone 为例:
public static List<string> RunAll(out List<string> closureMembers)
{
    var errs = new List<string>();
    closureMembers = new List<string>();
    if (EditorUtility.scriptCompilationFailed)
    {
        errs.Add("[AC-6-22] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                 "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
        return errs;          // 记红,不静默跳过 —— 静默跳过 = 该次构建里本门不存在
    }
    var dll = AssemblyGates.ScriptAssemblyPath(SimAssemblyName);
    errs.AddRange(CheckEcozoneIntegerIl(dll, EcozoneRoots, out closureMembers));
    return errs;
}
```

`EmergencyIntegerGates.RunAll` 扫**两个**装配:`AC-10-02 → Gameplay.Input`(3 侧边界)、
`AC-10-03 → Sim`(10 判定路径整数域)。

> **为什么守卫必须住在门里,不写在调用点**:写两遍就会被漏掉一遍。
> `AssemblyGates` 里已加注释明示「不要在此处另加 `scriptCompilationFailed` 判断:
> 那样会把守卫变成调用点纪律,下一个调用方就会漏」。

### 2.2 宿主侧:两个入口各接两行

`AssemblyGates.cs` —— `RunMenu`(`:114-115`)与 `OnPreprocessBuild`(`:1252-1253`)各:

```csharp
errs.AddRange(EcozoneIntegerGates.RunAll());       // AC-6-22
errs.AddRange(EmergencyIntegerGates.RunAll());     // AC-10-02 / AC-10-03
```

`BuildFailedException` 报文同步补两项门名。

### 2.3 manifest 侧:Kind 改判 + 缺口表清空

| 改动 | 内容 |
|---|---|
| 两入口清单各 +2 行 | `"EcozoneIntegerGates.RunAll"` / `"EmergencyIntegerGates.RunAll"` |
| 两门 `Kind` | `TestDriven` → **`Chain`**;`Consumer` = `"RunMenu+BuildGate"` |
| `KnownTestDrivenBlockingGaps` | 两行按本表自身协议**删除** |

### 2.4 ⚠️ 清空缺口表挖出**新的假绿面**,同批补掉

缺口表清空后,守它的 `test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded`
**遍历零条 = 恒绿**。此时若有人把某个已上线的门悄悄改回 `TestDriven`,缺口表还是空的,
**全绿、无人报警**。

⇒ 新增封闭清单 + 一条双向断言:

```csharp
private static readonly string[] ExpectedTestDrivenClasses =
    { "PodTypeScanner", "PresentationDtoGuard" };

[Test] public void test_testDrivenKind_isClosedSet_notDefault()   // 正反向都判
```

**这不是顺手加戏** —— 不清空缺口表就不需要它;是清空动作本身带来的新义务。

---

## 三、证伪轮

驱动:`tools/falsify/unity_falsify.py`;**规格**:`tools/falsify/specs/c2-il-gate-wiring.json`;
过滤器 `GateWiringManifestTest`。

| # | 变异 | 打在 | 预期转红 | 实测 | 判 |
|---|---|---|---|---|---|
| **F1** | 删 `BuildGate` 的 Ecozone 接线(`:1252`) | 生产侧接线 | `bidirectionally` | 同 | ✅ exact |
| **F2** | 删 `BuildGate` 的 Emergency 接线(`:1253`) | 生产侧接线 | `bidirectionally` | 同 | ✅ exact |
| **F3** | `EcozoneIntegerGates` 的 `Kind` 改回 `TestDriven` | manifest 回退方向 | `testDrivenKind_isClosedSet` **+** `libraryAndTestConsumersAreRealReferences` | 同两条 | ✅ exact(首轮漏预期,见 §3.1) |
| **F4** | `PodTypeScanner` 谎报 `Chain` | 证明 `chainKind…` 承重 | `chainKindEntriesAreActuallyWired` **+** `testDrivenKind_isClosedSet` | 同两条 | ✅ exact |

**F3 / F4 各自承重的分工**:

- **F4 证明 `chainKindEntriesAreActuallyWired` 有效** —— F1/F2 证不了它(见 §3.2)。
- **F3 证明新加的 `ExpectedTestDrivenClasses` 真的堵上了缺口表清空后的洞** ——
  若这条不红,说明清空缺口表确实开了个门。

### 3.1 F3 首跑红了 2 条而我只预期 1 条 —— 处置留痕

首跑驱动判 **exit 1(判别面不清)**。多红的是 `libraryAndTestConsumersAreRealReferences`。

**处理方式 = 先论证「它为什么**应该**红」**,不是把实得抄进预期:

> 该断言对 `TestDriven` 档要求 `Consumer` 是**文件路径**。而 `EcozoneIntegerGates` 的
> `Consumer` 仍写着 `"RunMenu+BuildGate"`(那是 `Chain` 档的语义)⇒
> `Path.Combine(root, "RunMenu+BuildGate")` 不存在 ⇒ 红。
> **这是同一处谎报的第二重自洽检查**:改 `Kind` 却不改 `Consumer`,本来就该红,不是误伤。

论证成立后才改 `expectRed`,并在 spec 里留 `_first_run_note`。
**没有直接把实得抄进预期** —— 那会让 `exactRed` 退化成 tautology(承 C-1 轮 §6.0 同款纪律)。

### 3.2 ⚠️ F1 / F2 **证不了** `chainKindEntriesAreActuallyWired`(如实登记)

该断言取的是**三入口并集**。而 Ecozone / Emergency 在 `RunMenu` 与 `BuildGate` **两处**都接了
⇒ 删任一处,并集不变 ⇒ 该断言**保持绿**。

⇒ 我**没有**把「并集恰好没变」写成「判据有效」,而是**另立 F4** 去真正证明它。

### 3.3 两轮跑数(首轮不过,第二轮通过 —— **两轮都留档**)

| 轮 | 退出码 | 结果 |
|---|---|---|
| 首轮 | **1** | F1 ✅ / F2 ✅ / **F3 ❌(红 2 条,预期 1 条)** / F4 ✅ ⇒ 驱动判「判别面不清」 |
| 第二轮 | **0** | **F1 / F2 / F3 / F4 四个变异全部 `exactRed`** |

第二轮逐条实测:

```
F1: total=15 passed=14 failed=1   RED: test_entryWiring_matchesManifest_bidirectionally
F2: total=15 passed=14 failed=1   RED: test_entryWiring_matchesManifest_bidirectionally
F3: total=15 passed=13 failed=2   RED: test_classManifest_libraryAndTestConsumersAreRealReferences
                                      test_testDrivenKind_isClosedSet_notDefault
F4: total=15 passed=13 failed=2   RED: test_classManifest_chainKindEntriesAreActuallyWired
                                      test_testDrivenKind_isClosedSet_notDefault
══ 证伪轮通过:4 个变异全部恰如预期 ══
```

**保留的 XML 原件**(`--keep-logs`,可复核):

| 变异 | XML | 结果 |
|---|---|---|
| F1 | `unity/Logs/falsify-F1-removeEcozoneWiring.xml` | `15 / 14 / 1` |
| F2 | `unity/Logs/falsify-F2-removeEmergencyWiringFromBuildGate.xml` | `15 / 14 / 1` |
| F3 | `unity/Logs/falsify-F3-revertEcozoneKindToTestDriven.xml` | `15 / 13 / 2` |
| F4 | `unity/Logs/falsify-F4-podTypeScannerTestDrivenToChain.xml` | `15 / 13 / 2` |

> ⚠️ **首轮为何不过、以及处置**(留痕,不掩盖):
> F3 多红了一条。**没有把实得抄进预期**,而是先逐条论证「它为什么**应该**红」
> (见 §3.1),论证成立后才改 `expectRed` 并留 `_first_run_note`。
> 这与 C-1 轮 §6.0 是同一个坑的第二次出现 —— 值得记一笔:**写 `expectRed` 时
> 容易只想到「新加的那条断言」,漏掉既有的自洽检查也会一起红。**

### 3.4 还原

- 驱动: `──── 还原:SHA-256 逐字节校验通过 ────`(`restore_all()`)
- 驱动: `✅ 指纹未变 —— 受保护路径:unity/Assets/AddressableAssetsData, unity/Assets/Audio`
- 独立复核(不依赖驱动):两文件 sha256 与轮前备份 `/tmp/c2falsify/` **逐位相同**
  ```
  d021d2f5…  AssemblyGates.cs
  13f5762b…  gate_wiring_manifest_test.cs
  ```
- 语义复核:`grep -c "EcozoneIntegerGates.RunAll" AssemblyGates.cs` = **2**(两入口各一)

---

## 四、回归

### 4.1 门测试

| 项 | 值 |
|---|---|
| 批次 | C-2 接线轮 · `GateWiringManifestTest` |
| 结果 | **`15 / 15 / 0`**(原 14 + 新增 1) |
| XML | `unity/Logs/c2-gatewiring.xml` |

### 4.2 全量 EditMode(带基线溯源三元组)

| 字段 | 值 |
|---|---|
| 截至时刻 | **`2026-10-21 01:50 CST`**(XML `mtime` 实测,非凭记忆) |
| 批次 | C-2 接线轮 · **全量 EditMode** |
| commit | **`33188fd`**(工作树含本批未提交改动) |
| XML | `unity/Logs/c2-full-editmode.xml` |
| 结果 | **`4086 / 4050 / 0 / 36`** |
| 对照基线 | `4085 / 4049 / 0 / 36` @ `2026-10-21 01:03 CST` · C-1 实现轮 · `f6cdaaf` |

**对账**:

```
Δtotal  = 4086 − 4085 = +1
Δpassed = 4050 − 4049 = +1
failed     0 → 0
skipped   36 → 36
```

**等式 `Δtotal = Δpassed = 新增条数 = 1` 成立。**

**差集实测(等式之外的独立判据)**:

```
本次唯一测试名 4086   基线唯一测试名 4085
新增 1                消失 0

新增 1 条 = test_testDrivenKind_isClosedSet_notDefault
```

> **「消失 0」排除**「新增 N 同时坏 M、净增恰好对得上」的掩盖形态。
> ⚠️ **本批零运行时改动**(全部改动是 Editor 侧门 + 测试件),但**未跑 PlayMode** ——
> 如实登记,不据此声称 PlayMode 不受影响。

---

## 五、⚠️ 未跑项(禁借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **全量 EditMode 回归** | ✅ **`4086 / 4050 / 0 / 36`** | 见 §4.2 |
| **守卫本身的反证**(把 `RunAll` 里的 `scriptCompilationFailed` 分支改成恒 false ⇒ 须红) | **NOT RUN** | 该守卫的判据依赖「编译失败」这一环境态,batch 下无法稳定构造。**不用「15/15 全绿」冒充「守卫已验证」。** |
| **PlayMode** | **NOT RUN** | 本批零运行时改动,但「零运行时改动 ⇒ PlayMode 不受影响」是**判断不是实测**。 |
| IL2CPP 三格(ADR-012) | **NOT RUN** | 本机仅 Mono |
| 真实构建(`OnPreprocessBuild` 端到端) | **NOT RUN** | 未跑 `unity build`;本批只证明了「接线存在且删之会红」,未证明「构建时它真的跑通」 |

> 🔴 **最后一条要特别说明**:本批的判据是**源文本级**(`GateChainSource` 解析 `AssemblyGates.cs`),
> 它证明的是「这两行调用在源码里,且删掉会红」。**它不证明 `OnPreprocessBuild` 真的执行成功。**
> 端到端验证须跑一次真构建 —— 归下一轮,此处如实登记为未跑。

---

> **不经用户指令不 commit。**
