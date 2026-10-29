# T1 · S7 —— 证伪轮(S12-4 · BLOCKING)

- **对象**:`ADR-035 §Migration Plan` 的 **S7**
- **日期**:2026-10-30
- **纪律**:变异与跑测**在同一 bash 命令内**完成(改 → 跑 → 还原);还原判据 = **SHA-256 逐字节**;
  窗口内**未编辑任何文件**。

---

## 一、本轮覆盖的是哪一块

ADR 的 S7 行原文列了三条(M1 注入 `lethal=true` 行 ⇒ binder throw;M2 `injury_id` 指向致死行 ⇒ throw;
M3 新 Kind 插中间 ⇒ 旧存档解码错)。**这三条在 S3 / S5 已经跑过** —— 照抄重跑一遍只会多三行「绿」,
不会多一条信息。所以本轮做两件事:

| | 内容 |
|---|---|
| **① 结清 S7 行的字面三条** | 见 §二 覆盖表:逐条指向既有轮次的实测记录 |
| **② 补上从未被证伪的那一层** | **S9 的烘焙 / 门链** —— S3 打绑定器、S4 打视图、S5 打编解码,**S9 一条变异都没做过** ⇒ 见 §三 的六条 |

> ⚠️ **S7 行的 M1 与 M2 是同一个判据**(两条都走 `lethal == true`,一条从「表里 Inject」进,
> 一条从「`injury_id` 指向」进)。把它们当两条独立变异记账会**虚增覆盖**。本件按**一条**计。

---

## 二、T1 形状链的证伪覆盖表(authority:各轮证据原件)

| 层 | 判据 | 变异数 | 实测 | 原件 |
|---|---|---|---|---|
| **绑定器** `CostTableBinder` | N-1 / kind 闸 / V-10 告警 / N-4′ canary / R4 / 逐行未启用 | **6** | 每组**恰好**红 2 条(3 组) | `t1-s3-cost-table-binder-2026-10-30.md` §五 |
| **视图** `CostTable53`(L3) | N-1 / kind 闸 / §⑥ 过量闸 / 单射 | **4** | **恰好 4 红** | `t1-s4-cost-table53-view-2026-10-30.md` §四 |
| **编解码 / 登记** | 解码分派 / 编码分派 / `DefaultPathMarker` 数字 / 枚举插中间 | **5** | 2 + 2 + 1 + 2 红;M5(路由)**0 红 = 无夹具**,守卫在 CI | `t1-s5-treatment-harm-onset-2026-10-30.md` §三 |
| **烘焙 / 构建门** `CostTable53Bake` · `CostTableCookedWriter` · `CostTableGate` | 见 §三 | **6** | 全部**恰好** | 本件 |
| **构建门真的会被构建执行** | 探针轮(非变异) | 5 次构建 | 基线绿 → 探针红 → V-5/V-6/V-7 各红 → 还原绿 | `t1-s9-build-path-2026-10-30.md` §三 |

**合计 21 条变异 + 5 次探针构建。**

> ⚠️ **覆盖表不主张「全链都被证伪」**。两处已如实登记的缺口:
> ① S5 的 **M5(路由)0 红** —— 本地无夹具,只有 CI 的「重生成后一致性」步兜;
> ② 本件只打 **S9 新增** 的六条,**未重打** S3/S4/S5 已打过的判据(重打 = 重复记账,不是加密)。

---

## 三、本轮六条变异(全部**恰好**命中)

改前哈希:`CostTableCookedWriter.cs` `98c68de1…` · `CostTable53Bake.cs` `45cfbc2c…` ·
`CostTableGate.cs` `561dacf3…`(= S9 基线哈希,还原后一致)。

| # | 变异 | 停用的性质 | 预判红 | **实测红(恰好)** |
|---|---|---|---|---|
| **M1** | `w.Write(rows[i].InjuryId)` → `w.Write(0)` | 产物里是**解析后的整数外键**(ADR-014「玩家构建零解析器」) | 1 | **1**:`test_injuryId_isResolvedToRegistryInteger` |
| **M2** | 从 `DeriveConfigVersion` 的源集里删掉注册表 | `ConfigVersion` **覆盖两个源**(否则改注册表对存档头比对不可见) | 1 | **1**:`test_configVersion_coversRegistryToo` |
| **M3** | `rows.Sort(...)` 改成 `if (rows.Count < 0) rows.Sort(...)`(永不排序) | 行序按 `(harm_kind, band)` **规范化**,与输入顺序无关 | 1 | **1**:`test_rowOrder_isCanonicalNotInputOrder` |
| **M4** | 空表 `throw` | **空表合法**(S2 惰性形态;与 `DelayTable` 的「空即抛」刻意不同) | 2 | **2**:`test_realSkeleton_bakesEmptyTable` · `test_gate_realRepo_isGreen` |
| **M5** | `catch (BakeValidationException)` 改成直接 `return errs` | 门把**校验失败暴露成错误集**(不吞) | 3 | **3**:`test_gate_lethalRow_isRed_andNamesN1` · `test_gate_rowsWithoutEnabledKind_isRed` · `test_gate_overdoseEnabled_isRed` |
| **M6** | 缺源分支 `return errs` → `throw` | 门**永不抛**(返回判据,不把「门没跑成」和「门判红」混成一种形状) | 1 | **1**:`test_gate_missingSources_returnsErrorNotThrow` |

**6 条变异 / 9 条红 —— 与预判集合逐一相同,不多不少。**
每次变异后 `sha256sum` 逐字节还原;`FALSIFY-` 残留检查 **0**;`git status` 干净。

> ⚠️ **M4 的预判是 2 条而不是 1 条**,理由写在没有被事后改过的脚本里:
> 空表一抛,「真实仓的空骨架能过门」也必然红。**先写预判再跑**,不是跑完倒推。

---

## 四、本件**不**主张

| 项 | 状态 |
|---|---|
| T1 全链「已被证伪」 | ❌ §二 的覆盖表有两处已知缺口(S5 路由 0 红;S3/S4/S5 未重打) |
| 证伪轮能替代「真跑」 | ❌ 它证明的是**判据有判别力**,不是**材料齐了**;T2/T3 仍卡上游债 |
| 变异能覆盖**未写出来的**判据 | ❌ 变异只能打已有守卫;缺守卫的地方它**看不见**(S5 M5 就是活例) |
| 本轮重跑了全量回归 | ❌ **没有** —— 本轮净改动为零(六次变异全部逐字节还原,`git status` 干净),故不重跑;上一次全量 = S9 批 `6029/5992/0/37` |
