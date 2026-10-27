# README —— `tools/determinism-matrix/`

**立**:2026-10-27 · 承用户裁定「落两格 + 第三格登记未达」(C-2 / `AC-18-15`)

---

## 这工具解决什么

`AC-18-15`(processing 的跨平台逐位重放)自 sprint-20 起挂 **`EXTERNAL · BLOCKED-BY-ADR-012`**,
理由是「三格矩阵不存在」。ADR-012 §Validation 的「三格全绿」复选框因此一直未勾。

本工具把**可达的两格**从「桌面跑过一次」升级为**可重跑的本机判据**;
第三格(ARM64)按 2026-10-17 / 2026-10-27 两次用户裁定**登记为长期未达**,
**不降格、不换面、不改挂**。

> ⚠️ **`AC-18-15` 仍未勾,且本工具不改变这一点。** 理由见下 §「判据面的精确边界」。

---

## 跑法

```bash
# 只打印命令与前置检查(不跑 Unity、不写盘)
python3 tools/determinism-matrix/run_matrix.py --dry-run

# 两格全跑 + 对拍(默认)
python3 tools/determinism-matrix/run_matrix.py

# 只跑某一格
python3 tools/determinism-matrix/run_matrix.py --cell mono
python3 tools/determinism-matrix/run_matrix.py --cell il2cpp
```

落盘:`unity/Logs/determinism-matrix/`(gitignored 指定暂存区)。

**前置**:Unity batch **独占工程** —— 编辑器须关。本工具开跑前检查
`unity/Temp/UnityLockfile` 与活 batch 进程,发现占用即 **`exit 2`**(不抢、不杀)。
⚠️ 若你**自己**开了 batch,先确认归属再决定是否运行;**不要**让本工具替你去 kill。

---

## 两格是什么

| 格 | 后端 | 怎么跑 | 测集 |
|---|---|---|---|
| **1** | Linux-x64-**Mono** | Editor 进程内(EditionMode + PlayMode) | `GoldenHashV1` / `DeterminismGolden*` / `TelemetryGolden*` |
| **2** | Linux-x64-**IL2CPP** | 切 Standalone 后端 → `-runTests -testPlatform StandaloneLinux64`(构建并运行 player) | `DeterminismGoldenCrossplatform` |

判据(与 AC-29 / ADR-012 逐字对齐,**不自创**):

1. 每格测试套件零 `failed`;
2. 两格各自把 **19 条**场景哈希落盘(`AC29_OUT`);
3. 两格落盘件**逐位相同**(外部 diff,**不是**测试内断言 —— 防自指);
4. 两格落盘件都与**独立 Python 金标准**(`tests/unit/item_database/golden/golden-v1.txt`)相同;
5. 落盘件头自报的后端标记**确实是两个不同后端**(防「拿 Mono 结果冒充 IL2CPP」)。

> ⚠️ **判据 1 单独成立不构成矩阵绿。** 这正是 ADR-012 的原话:
> 「测试绿**但 diff 不同**等价于红」。工具因此把 1 只当作必要条件。

**后端归位**:格 2 跑完工具自动 `SetMono` 归位,并做 **SHA-256 逐字节校验**
(承本项目「还原判据 = SHA-256,不是 grep」纪律);不匹配即 `exit 3` 并拒绝继续。
⚠️ **不要用 `git checkout` 还原** —— 会抹掉未提交改动。

---

## 判据面的精确边界(**禁借绿**)

| 项 | 状态 | 说明 |
|---|---|---|
| 格 1 Mono | ✅ **已实跑通过** | `EditMode 53/53/0/0` · `PlayMode 3/3/0/0`(见 `production/qa/evidence/ac18-15-two-cell-matrix-2026-10-27.md`) |
| 格 2 IL2CPP-x64 | ✅ **已实跑通过** | `StandaloneLinux64 3/3/0/0` · 19/19 与金标准逐位相同 · 后端标记 `IL2CPP` |
| **格 3 ARM64-IL2CPP** | ⛔ **长期未达** | 2026-10-17 探针:46 个 `BuildTarget` 中**零个含 arm**;`StandaloneLinux64` 是唯一受支持的 Linux 目标。**目标不存在,不是没跑** |
| **CI 常驻性** | ⛔ **EXTERNAL** | 缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`(`TD-ci-01`)。**凭证须用户侧申请,agent 侧无任何路径可取得** |
| Windows-x64 / Apple Silicon | ⛔ 未跑 | ADR-012 §二:归**发版前必跑**,不进常驻 |

⇒ **本工具让 `AC-18-15` 从「矩阵不存在」降级为「只差不可达的第三格 + 常驻性」。**
**它不解除 `EXTERNAL`。** ADR-012 的「三格全绿」复选框在 pinned 工具链上
**结构上不可达成**,不会因本工具而勾选。

> ⚠️ **还有一条更硬的前置(`AC-18-15` 真正的缺口)**:本工具跑的是 **item-database 的
> 19 条黄金哈希**(`AC-21a-28/29` 面),**不是** processing 的三出参。
> processing 侧目前**零黄金夹具**(`grep -rln "golden\|Golden" unity/Assets/Tests/EditMode/Processing/` 零命中),
> 其 PlayMode 件 `recipe_settlement_solver_test.cs` 是**同后端两入口对拍**,非跨后端。
> ⇒ **要让 `AC-18-15` 可验,须先在 processing 侧建同型黄金夹具** —— 这条归 processing 实现轮,
> 本工具**不提供**。

**解锁条件(唯一)**:① 引擎升级后重跑 `BuildTargetProbeCli` 探针,若出现受支持的 ARM64 目标;
② 用户提供 CI 凭证并完成一次成功的 `workflow_dispatch`。

---

## 与 `smoke-check` 的分工

本工具跑的是**确定性判据面**(编辑期哈希 + 跨后端对拍),**不是全量回归**。
全量 EditMode/PlayMode 回归归 `/smoke-check`。两者数字**不可互相引用**。

## 与 `tools/falsify` 的分工

`falsify` 证的是「**停用某守卫 ⇒ 某夹具转红**」(判别力);
本工具证的是「**同一输入在两个后端产出逐位相同的结果**」(跨平台一致性)。
两者判据不同面,**互不替代**。

---

## 已知未做(如实登记)

- **未接 CI**:本工具是**本机**执行体。接 CI 需要凭证,**归 `TD-ci-01`**。
- **测集窄**:只覆盖确定性判据的三个 `TestFixture`,不覆盖全量。
- **金标准版本协议未演示**:ADR-012 §Validation 的「刷新协议演示」(公式变更 → `golden-v2` +
  变更日志 + 三格重签 + 单平台独签被 CI 拒绝)仍未做,该项**仍未勾**。
