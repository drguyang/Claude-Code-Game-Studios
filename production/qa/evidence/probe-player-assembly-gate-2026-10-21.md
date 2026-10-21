# PlayerAssemblyGate 探针轮 —— 构建期执行证据

**日期**:2026-10-21
**对象**:`unity/Assets/Editor.Tools.Gates/PlayerAssemblyGate.cs`(TD-testasm-01 机械门)
**要证**:`AssemblyGates.cs:150` 的 `errs.AddRange(PlayerAssemblyGate.RunAll("."))` 这行接线,
**在真实构建时确实被执行** —— 而非「源码里有这行」或「EditMode 测试断言它在」。

---

## 0. 为什么需要这一轮(判据差一层)

门链无错时**零输出** ⇒ `Build Finished, Result: Success.` 只证明 `errs.Count == 0`,
**无法区分**两种情形:

- ① 门真的跑了,扫过 `Assets/Tests/PlayMode/**/*.cs`,没发现问题 ⇒ 绿(**有效**);
- ② 门**根本没被调用**,`errs` 恒为空 ⇒ 绿(**假绿**)。

`s17-6s-il2cpp.log` 里确实有 `BuildGate:OnPreprocessBuild` 的栈帧,但那是 **15:45** 的构建,
而 `PlayerAssemblyGate.cs` **16:34** 才落盘 ⇒ **当前这棵树上的这个门从未被任何构建跑过**。
`gate_wiring_manifest_test` 的登记断言只证「接线在源码里且删掉会红」,不证构建期执行。

---

## 1. 预登记(写于跑之前)

原件:`production/qa/evidence/tmp-probe-pag-preregistration.md`(跑前落盘)。

| # | 判据 | 预期 |
|---|---|---|
| 1 | 构建结论 | `Build Finished, Result: Failure.` |
| 2 | 探针串出现在**构建日志** | 含 `[PROBE] PlayerAssemblyGate 探针轮` |
| 3 | 栈帧归属 | 探针串的 `Debug.LogError` 栈帧上有 `BuildGate:OnPreprocessBuild` |
| 4 | 失败原因 | 含 `BuildFailedException` / `[AssemblyGates] 构建前门失败` |
| 5 | utp 阶段 | `steps` 止于 `Preprocess Player` |

---

## 2. 变异(单点)

在 `PlayerAssemblyGate.RunAll` 的 `var errs = new List<string>();` 之后插入两行:

```csharp
errs.Add("[PROBE] PlayerAssemblyGate 探针轮 —— 无条件报错并提前返回(2026-10-21)");
return errs;
```

**为什么打在 `PlayerAssemblyGate.RunAll` 内部而不是 `BuildGate` 里**:打在 `BuildGate` 只能证
「`BuildGate` 跑了」(那件事 `s17-6s` 日志已经证过);打在门内部才能证**这个门本身**在构建期被执行。

**变异落地判据 = sha256 变化**(不由栈帧行号证明):

| | sha256 |
|---|---|
| 基线 | `d7a58e3d6efea82b9cdaab9d3a1fa5b8d28a3a366755f195fdb2adc6306487c7` |
| 变异后 | `da9d1ee8ec126ff3609884564086bfe8855afe0e195a196ce7286bf802620a2a` |

---

## 3. 探针轮结果(逐条对预登记)

命令:

```
cd unity && unity build "$PWD" --target StandaloneLinux64 \
  -o /tmp/probe-pag-player/pag-probe.x86_64 \
  -l unity/Logs/probe-pag-2026-10-21.log --no-tail
```

进程退出码 **6**(`unity build` 的构建失败码)。

| # | 判据 | 实得 | 判定 |
|---|---|---|---|
| 1 | 构建结论 | `Build Finished, Result: Failure.`(`:1119`) | ✅ |
| 2 | 探针串在日志 | `:1049` / `:1122` 两处出现 | ✅ |
| 3 | 栈帧归属 | `BuildGate:OnPreprocessBuild … (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1268)` | ✅ |
| 4 | 失败原因 | `BuildFailedException: [AssemblyGates] 构建前门失败 1 条(…)`(`:1063` / `:1123`) | ✅ |
| 5 | utp 阶段 | `"steps":[{"description":"Preprocess Player","duration":1424}]` —— 只有一步 | ✅ |

**5/5 EXACT。**

---

## 4. 归因(论证 vs 观测 —— 必须分开写)

| 命题 | 证据类型 |
|---|---|
| `BuildGate.OnPreprocessBuild` 在构建期被执行 | **观测** —— 栈帧 + 日志 |
| 探针串来自 `PlayerAssemblyGate.RunAll` 内部 | **观测** —— 变异只加在那一个方法里(sha256 可验) |
| `AssemblyGates.cs:150` 的接线行被执行 | **论证**(非观测) |

第三条为什么只能是论证:**栈帧给的是 `Debug.LogError` 行**(`AssemblyGates.cs:1268`),
不是接线行(`:150`),也不是变异行(`PlayerAssemblyGate.cs:105`)。
它成立的依据是两件事合起来:

1. 全仓 `grep "PlayerAssemblyGate.RunAll"` 在**非测试**代码里**唯一**命中 `AssemblyGates.cs:150`;
2. `BuildGate.OnPreprocessBuild` 的第一行就是 `errs.AddRange(AssemblyGates.RunAll())`,
   而 `RunAll()` 的**最后一个**动作是 `errs.AddRange(PlayerAssemblyGate.RunAll("."))`
   (`:150`),其后直接 `return errs;` —— 中间无分支、无提前返回。

⇒ 探针串出现在构建日志里,**必然**经过 `:150`。这是论证,不写成直接观测。

---

## 5. 干净对照轮

还原后跑同一命令(仅换日志与输出路径),证明「探针之外没有别的红」:

| 判据 | 实得 |
|---|---|
| 进程退出码 | **0** |
| 构建结论 | `Build Finished, Result: Success.`(`:8511`) |
| 探针串 | **0** 处 |
| `BuildGate:OnPreprocessBuild` 栈帧 | **6** 次(门链确实跑了) |
| `BuildFailedException` | **0** 处 |

⇒ 两轮唯一差异 = 探针那一行。**Failure 由探针造成,不是别的红。**

---

## 6. 还原

```bash
cp /tmp/PAG.cs.bak unity/Assets/Editor.Tools.Gates/PlayerAssemblyGate.cs
sha256sum -c /tmp/pag-baseline.sha256   # ⇒ 成功(exit 0)
cmp …/PlayerAssemblyGate.cs /tmp/PAG.cs.bak   # ⇒ 逐字节一致
git diff -- …/PlayerAssemblyGate.cs    # ⇒ 空
```

**还原判据 = SHA-256 逐字节,不是 grep 探针串**(探针串可以改名,哈希不行)。

---

## 7. 边界(禁借绿)

- 本轮证的是**门被执行**,不是**门的判据正确**。判据本身的判别力由
  `player_assembly_gate_test.cs`(11 测)与 `TD-testasm-01` 记录的实测误报面负责。
- 本轮只跑 **Linux-x64**(Mono 编辑器内置构建),不涉 IL2CPP 三格矩阵。
- 探针轮用的是 `--target StandaloneLinux64` 的内置构建(非 IL2CPP);门是
  `IPreprocessBuildWithReport`,与后端无关,但**未在 IL2CPP 后端下复跑**。
