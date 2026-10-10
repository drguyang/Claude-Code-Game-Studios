# 评审原件 — M2 接线轮阶段 2 · 批次 B+C+D(数据面 + 体征链 + 双写者)· 2026-10-10

> **对象**:批次 B(`RegistrySchema` 曲线字段 +215 行 / fixture `DIS_SYNTH_FIXTURE` id 9001)·
> 批次 C(三断点+主循环:`DiseaseVitalsService` 首个 `IVitalsQuery` 生产实装 / apply 拆两半 /
> 泵第二驱动口 / `ProgressionEvaluator` F1+Decay 接真)· 批次 D(`CaseOpenWriter` +
> `ResourceHarvestWriter` + b7 写者存在性门 + `AssemblyGates.RunAll` 接线)
> **流程**:三批依序实现(各批主会话亲核)→ 合批双代理恰一轮评审(均 APPROVE)→
> 2 处窄修 + **变异实测抓出双评审共误判(M13)并补真判别测试闭合** → 终态全量复跑绿 → 本原件
> **前置**:A/E 批已单独收口(`review-m2-stage2-ae-2026-10-09.md`);四裁定①合成 fixture ③
> CatchUp NOT-RUN(F 批登记)均承 sprint-04 #5

---

## 一、原判定(合批双代理评审 · 恰一轮 · 2026-10-10)

### 代码面(`lead-programmer`)**APPROVE**(0 BLOCKING + 6 登记)

核过零问题 15 项(独立 grep 闭合 11 写者+24 豁免=35 与 yaml author 数吻合 · 既有公共面逐位
兼容 diff 核 · b7 三红判据逐段读码确认真实现 · 门 A/装配边界/asmdef 全核)。

| # | 级 | 位置 | 缺陷 | 处置 |
|---|---|---|---|---|
| 码-1 | 中 | RegistrySchema R1-20…29 | GDD 校验 12(`R_rise>ε_MIN`)未进写入期(ε_MIN 值待定阻塞;求值侧 fail-loud 已兜非静默) | **登记**(数值轮定值后补 R1-30) |
| 码-2 | 低 | PlayerController.cs:272 vs 泵回推 | 玩家事件头 stamp 帧末 `CurrentTick` 与「边沿自有 tick」契约不同源 ⇒ multi-tick 帧内最多超前 steps−1(同帧自愈不跨帧) | **登记**(修法二选一:传 edgeTick / 注释降格;归接线轮) |
| 码-3 | 低 | DiseaseVitalsService 游标 | 流按 Tick 非降的不变量无断言无登记(CatchUp 回填可破) | **登记**(建议 Append 加单调断言) |
| 码-4 | 低 | AssemblyGates b4 | asmDirs/白名单缺 `Gameplay.Boot`(Boot 内 ToFloat 门不可见) | **登记**(gate 轮,与 C 批登记合并) |
| 码-5 | 低 | WriterExistenceGate | 扫描面缺 Sim.Contracts;测试只断言 ≥3 面(删 Input/UI 面不红) | **登记**(gate 轮) |
| 码-6 | 低 | 越界复核 | GDD 1 行注记不在 A+E 原件文件清单 | **本轮修**(A+E 原件补登归属,F6 修复) |

### 测试面(`qa-lead`)**APPROVED**(2 S4 · 零阻塞)

| # | 级 | 缺陷 | 处置 |
|---|---|---|---|
| 测-1 | S4 | B 时点 `m2b_disease.xml` 是命名空间过滤跑非全量(已被 C/D 全量重证) | 登记(措辞) |
| 测-2 | S4 | b7 对 24 豁免 Kind 零覆盖(设计可接受:豁免需带日期理由行 + 已写者禁豁免 + 空输入假绿三重防护) | 登记 |

判别力抽验:M10/M11/M13 推演核为真判(当时)· 禁 Fake 成色属实(真装配袋,零 Fake 自洽环,
锚点独立算出)· oracle 独立性属实(B 侧测试私有实现;C 侧期望值手算)· 假绿扫描干净 ·
复跑证据链裁决闭合(find -newermt 核实无文件晚于 D 全量跑)· E 批归因测在 3181 内绿。

---

## 二、变异实测(3 发;抓出双评审共误判 1 处)

| 变异 | 结果 | 判决 |
|---|---|---|
| MUT-M10(泵次序反转:疾病 Step 先于玩家提交) | **恰红 1**(`diseaseStepRunsAfterPlayerEdge` 的 cellAlreadyCommitted) | 真判 ✓ |
| MUT-M13 初次(删游标 `if (e.Tick > tick) break`) | **全绿 = 逃逸** | **双评审共误判**:推演漏了 Decay 的 `Δ<0` 归零门 —— 剂量面被双层防护冗余吸收(等价变异);游标门独立判别面 = 未来 DiseaseOnset 不得提前建档,**原测试未盖** |
| MUT-M13b(补新测后重放删门) | **恰红 1**(`test_vitalsChain_futureOnset_notRegisteredBeforeItsTick`) | 判别面闭合 ✓ |
| 恢复复跑 | VitalsChain 13/13 绿 · 零 `MUT` 残留 | ✓ |

**补测试**:`vitals_chain_test.cs` +1 条 —— `SpawnNext(diseaseId, tick:2000)` 造未来 onset,
DriveTo(1000) 断言 `PatientCourseNotFoundException`(未建档 fail-loud),DriveTo(2000)
断言 `DoesNotThrow`(事件未永久漏读)。头注写明「双层防护等价变异」发现。

**教训登记**:纸面推演对「多层门冗余」结构性盲 —— 变异实测不可省(本会话第 3 次实证
纸面推演与实测不一致:前两次为阶段 1 尾 M3 逃逸、A 批黄金夹具共模)。

---

## 三、验证命令与实数

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
unity test unity --mode EditMode --output unity/Logs/editmode_full_bcd_final.xml   # 终态
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiseaseSimulation.VitalsChainTest" \
  --output unity/Logs/mut_m13b.xml   # 变异(MUT 系列同款)
```

| run | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| B 时点(DiseaseSimulation 过滤) | 116 | 116 | 0 | 0 | 测-1:过滤跑 |
| C 时点全量 | 3160 | 3113 | 0 | 46 | +35=B20+C15 |
| D 时点全量 | 3181 | 3134 | 0 | 46 | +21=D 批 |
| **终态全量(含补测)** | **3182** | **3135** | **0** | **46** | +1=游标门补测;failed=0 判绿 |
| MUT-M10 / MUT-M13 初 / MUT-M13b | 3 / 12 / 13 | 2 / 12 / 12 | **1 / 0 / 1** | 0 | 见 §二 |

⚠️ 全量慢性 exit 非零(既往同款)—— XML 为判据。跑前独占:仅 hub/Licensing,无锁文件。

---

## 四、判定链

**三批依序实现(B→C→D,各批主会话亲核:diff/XML/guid)→ 合批双代理恰一轮评审**
(代码面 APPROVE 0B+6L,独立 grep 闭合 35 支 · 测试面 APPROVED 2S4)→ **2 处窄修**(A+E 原件
补登 GDD 归属;游标门补真判别测试)→ **3 发变异**(M10 恰红 · M13 初次逃逸抓出双评审共误判 ·
M13b 补测后恰红闭合)→ **终态全量复跑绿**(3182/3135/0 红)→ **APPROVE 收口**。

**登记不修(8 项)**:码-1(ε_MIN/R1-30 → 数值轮)· 码-2(玩家事件 stamp 不同源 → 接线轮)·
码-3(游标单调断言)· 码-4/5(b4/b7 门面扩展 → gate 轮)· 测-1(B 数字措辞)·
测-2(b7 豁免率设计可接受)· C 批 GDD 缺口 8 项与 D 批豁免表 24 条(各归其轮,批内已登记)。

**未跑/未核声明**:PlayMode 本轮零跑(门④ 收口轮前置 NOT-RUN 承阶段 1 裁定);b7/b4/b6 门
未整菜单实跑(读实现+读测试+独立 grep 交叉);skipped 46 逐条未复 triage(与基线逐项一致);
码-2 的 multi-tick 帧发生率未运行期观测。
