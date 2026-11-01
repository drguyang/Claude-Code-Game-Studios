# sprint-22 收口判定复评(2026-11-01 下午)

| 字段 | 值 |
|---|---|
| **对象** | `production/sprints/sprint-22.md` §收口判定(2026-11-01 上午版)的 **NOT PASS** |
| **复评轮** | 2026-11-01 · 用户指令「复评 sprint-22 收口判定」 |
| **三元组** | 截至 `2026-11-01 12:14:36 CST` · 批次 `S22-9 第三批` · `a956f0c1` |
| **复评结论** | **Exit 达成 ⇒ sprint-22 判定改 PASS**(原文判定不删,见 sprint-22.md 复评节) |

---

## 一、原判定(照抄口径,不重述推理)

> 「8/8 卡收口 · 判定 = ⚠️ NOT PASS(**路线图 Exit 判据未达 —— 1 条**)· 其余面全部达标」

NOT PASS 的**唯一原因**(原判定 §三):接通侧零生产 —— 三条 grep 证明没有任何生产路径
会让 `HarvestOrchestrator.Resolve` 的采前闸被求值。原判定同时给出两条解除路径,
**甲案(补接线)**由用户指令「做到底」/「继续做 ④」/「接 BootRoot 调用点」逐段授权执行。

## 二、修复落点(甲案执行 · 两个 commit)

| commit | 内容 | 证据原件 |
|---|---|---|
| `cd935295` | ④ `HarvestInteractionDriver`(闸门求值点住 driver,公开面零 `HarvestGate` 形参)+ ⑤ `HarvestGateAllowGate`(「传 Allow 即红」机械门,接链 + 门链手写清单登记) | `s22-9-quality-distribution-and-constants-2026-11-01.md` §8 |
| `a956f0c1` | BootRoot 调用点:E 按下沿 → `HarvestInteractionBridge` → 4 选择器 → **半径裁剪** → 真闸门(`CarryLoadSolver.CanCarry` + `NodeRemaining.CanHarvest`)→ `Resolve`;同批修模态夹具的 E 路由碰撞 | 同上 §10–11 |

## 三、逐条验证(原判定 §三 复算命令重跑 + 补充)

### 3.1 原判定三条命令,今日实跑

```bash
grep -rn "\.Resolve(" unity/Assets --include=*.cs | grep -v "/Tests/"     # 现 3 命中,HarvestOrchestrator 者恰 1
grep -rn "\.Harvest\b"  unity/Assets --include=*.cs | grep -v "/Tests/"     # 仍零命中 —— 命名伪象,见 3.2
grep -rn "Harvest\|Gather" unity/Assets --include=*.inputactions             # 仍零命中 —— 口径差,见 3.3
```

- ① 三命中中两处无关(`EmergencyIntegerGates.cs:261` 是注释;`FocusNavigationBridge.cs:234`
  的 `_neighborResolver.Resolve` 是另一类型的方法)。**`HarvestOrchestrator.Resolve` 的生产调用点
  = `HarvestInteractionDriver.cs:254` 恰 1 处**。
- ② **不是「无消费方」**—— 是字段改名后的 grep 失配,见 3.2 的新发起链命令。
- ③ 无专用采集 action —— E(Interact)复用,见 3.3。

### 3.2 新发起链(替代 ② 的判据 —— 同一把尺子的新形状)

```bash
grep -rn "TryHarvest\|HarvestDriver\|ClaimInteract" unity/Assets --include=*.cs | grep -v "/Tests/"
# BootRoot.cs:593/602(桥装配 + claim 钩子)· HarvestInteractionBridge.cs:65/71(claim → TryHarvest)
# · HarvestInteractionDriver.cs:189(公开入口)→ :254(Resolve)· CompositionRoot.cs:1628(services 挂载)
```

**闸门求值在生产路径上的证据链**:
1. 类型面:driver 公开面零 `HarvestGate` 形参(`test_driverSurface_neverTakesHarvestGate` 反射断言);
2. 源面:`HarvestGateAllowGate.RunAll` 接进 `AssemblyGates.RunAll`(门链手写清单双向差集钉住,
   `GateWiringManifestTest` 15/15);
3. 行为面:driver 夹具两条具名拒绝(满载 / 余量)经**真 Resolve** 且拒时零事件(证伪轮 M-A/M-B
   停任一闸 ⇒ 对应夹具红);
4. **真启动序**:PlayMode `modal_gate_wiring_test` 首轮一红证明全链在真烘焙数据上跑通
   (出生格 d∞=2 ≤ `r_interact`=3,E 被采集合法消费 —— 修复后夹具挪无药丛格,复跑 4/4)。

### 3.3 与甲案字面的两处口径差(如实登记,不掩饰)

| 差 | 甲案字面 | 落地形态 | 定性 |
|---|---|---|---|
| ② | grep `.Harvest` 应有命中 | 发起链走 `TryHarvest` / `services.HarvestDriver` | **纯命名差**;同一判据(有发起方)已由 3.2 命令满足 |
| ③ | 「3 的**采集 action**」 | 复用既有交互 action(E),经 4 的选择器按 Kind 路由 | **设计内**:4 的 GDD 即「交互意图 → 选择器 → Kind 路由」,采集是交互的一种;专用采集键是设计变更,不在 Exit(「`CanCarry` 采前闸接通」)语义内。若用户要专用键,另立卡 |

## 四、回归与账面(可复算)

| 面 | 值 | 复算 |
|---|---|---|
| EditMode | `6370 / 6333 / 0 / 37` @ 12:09:39 | `unity/Logs/s229-final-em3.xml`;等式 `6370 − 6364(§9) = 6` = 裁剪 2 + 桥 4 |
| PlayMode | `273 / 269 / 0 / 2 / 2` @ 12:14:36 | `unity/Logs/s229-final-pm3.xml`;`±0`(模态夹具挪格,无新测) |
| 债册 | `--check` exit **0**;未闭 P0 面 0(甲案未立 `TD-foraging-04` —— 乙案未走) | `python3 tools/debtcount/debtcount.py --check` |
| 证伪残留 | `grep -rn "FALSIFY-" unity/Assets --include=*.cs \| wc -l` ⇒ 0 | 同左 |

## 五、原判定「非绿面」七条的逐条现状

| # | 非绿面 | 复评现状 |
|---|---|---|
| 1 | **Exit 未达** | ✅ **结清**(本件 §三) |
| 2 | `AC-17-06` 只落谓词半 | ◑ **解除动作已发生**(签核件 C2 原文:「随发起路径一并验」)—— driver 夹具 `:176`/`:200` 两条具名拒绝 = 动作半的行为证据;AC 状态本身归 story 台账,本件不改 |
| 3 | 台账行收口轮才补 | 维持(历史事实,不因复评消失) |
| 4 | `[L]` 三条 EXTERNAL | 维持(42 UX Flag ③ 未交付) |
| 5 | smoke 无名 4 条(#8/#9/#18/#22) | 维持 —— #8 是**拾取**(掉落物拾取交互,非采集),#9 诊断呈现,#18/#22 归 `TD-ci-01`;本批不触碰 smoke json |
| 6 | `TD-skeuo-01` open | 维持 |
| 7 | 帐篷临时值 | 维持(数值轮) |

## 六、复评结论

**sprint-22 的路线图 Exit「`CanCarry` 采前闸接通」已达成** —— 按 §三.2 的四层证据链
(类型面 / 源面 / 行为面 / 真启动序),采前闸不再是「装着服务、没有发起方的孤岛」。

**判定:NOT PASS → PASS**。原判定的其余面(8/8 卡、回归零失败、债册不涨、smoke 无自动失败)
未被推翻;非绿面 2–7 照旧成立并逐条具名(§五),**不因判字变绿而被覆盖**。

⚠️ 本复评不主张:① smoke 22 条全绿(无名 4 条仍在);② 采集的**数值**已裁
(`seasonIndex=0` / `difficulty=Fix.One` / `AC-17-13` 归数值轮);③ 病人与药丛同场的
E 键路由已统一(候选集仍采集点单源,登记为已知边界,归 4 统一路由 story)。

复评原件:本件 · 判定落点:`production/sprints/sprint-22.md` §收口判定 复评节(原文不删)。
