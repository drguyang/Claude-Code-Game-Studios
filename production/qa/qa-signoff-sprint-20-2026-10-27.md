# QA Sign-off Report — Sprint 20

**Date**: `2026-10-27`
**Sprint**: 20 — clinic 链尾 + A-2 机制起步 + 判别力债清偿(+ 2026-10-26 追加「采集链路解锁链」)
**Engine**: Unity 6.3 LTS(6000.3.24f1)
**QA Plan**: `production/qa/qa-plan-sprint-20-2026-10-25.md`
**Smoke**: `production/qa/smoke-2026-10-27-sprint20.md`(**Verdict: PASS**)
**Sprint file**: `production/sprints/sprint-20.md`(§九 收口判定)
**Retro**: `production/retrospectives/retro-sprint-20-2026-10-27.md`
**被验的树**: `51193cf` + 8 改 / 15 未跟踪(`git status --porcelain | wc -l` ⇒ **23**)——
**该状态已于 2026-10-27 收口提交轮清零**(见 C-6):`0a64faf` + `34c75e9` + `d69d4b0`。
回归数 `5413/5378/0/35` 跑于提交前的同一棵树(生产码 mtime 全部早于 `10:51` 的测试时刻)

---

## Verdict: ⚠️ **APPROVED WITH CONDITIONS**

无 S1 / S2 bug(`production/qa/bugs/` 仅 `README.md`);计划内 7 件全 `done` 且各有实测载体;
两套件零红零回归。**但存在 6 条须处置的条件**(2 条治理级 + 2 条证据完备性 + 1 条覆盖率口径 + 1 条提交完整性),
故不判 `APPROVED`。

---

## Test Coverage Summary

### 计划内 7 件(S20-1…S20-7)

| 件 | 类型 | 自动化载体(实测条数) | 结果 | 证伪轮 |
|---|---|---|---|---|
| **S20-1** clinic-004 交付边界 | Integration | `EditMode/ClinicMachine/clinic_env_delivery_test.cs`(**9**) | ✅ PASS | ✅ F1–F3 全红 |
| **S20-2** clinic-005 纯度重放 + Memoize | Integration | `EditMode/ClinicMachine/clinic_purity_replay_test.cs`(**20**) | ✅ PASS | ✅ F7 + 11/11 |
| **S20-2a** TD-building-01 两半全补 | Integration | `EditMode/ModularBuilding/world_end_to_end_test.cs`(**8**)· `demolish_refund_from_catalog_test.cs`(**12**) | ✅ PASS | ✅ 四支全红 |
| **S20-3** foraging-001 品种闭包 + 装载校验 | Logic | `Unit/Foraging/foraging_data_load_test`(**46**)· `foraging_bake_wiring_test`(**6**) | ✅ PASS | ✅ 第四轮 7/7 |
| **S20-4** processing-001 配方子集 + Admissible | Logic | `EditMode/Processing/admissible_predicate_test.cs`(**29**) | ✅ PASS | ✅ M1–M8 + P1 = 8/8 |
| **S20-5** 判别力收紧轮 | Logic(推断) | `EditMode/ClinicMachine/` **11 件** | ✅ PASS | ✅ M0b 变异坐实 + M1 反证 7 红 |
| **S20-6** TD-prescription-07 材质分类器 | Logic(推断) | `EditMode/SkeuomorphicUI/world_billboard_test.cs`(**9**)· `dentch_dose_form_test.cs`(**23**) | ✅ PASS | ✅ M2 / M2b 双反证各 2 红 |
| **S20-7** 治理盘点轮 | Config/Data(推断) | 无(零新代码;留痕载体 = 债册 §S20-7 轮) | ✅ PASS | 不适用(纸面件) |

### 追加批 9 件(S20-8…S20-16)

| 件 | 状态 | 载体 | 结果 |
|---|---|---|---|
| **S20-9** processing-004 单炉空闲判定 | done | `EditMode/Processing/furnace_timing_no_process_state_test.cs`(**31**) | ✅ PASS(AC 4/5) |
| **S20-10** inventory-001 InventoryOf 折叠 | done | `EditMode/Inventory/inventory_fold_test.cs`(**28**) | ✅ PASS(AC 2/4) |
| **S20-11** inventory-003 堆叠/容器/折重 | done | `EditMode/Inventory/stacking_container_rules_test.cs`(**33**) | ✅ PASS(AC 5/6) |
| **S20-12** inventory-004 CanCarry/载重 | done | `EditMode/Inventory/carry_load_adjudication_test.cs`(**54**) | ✅ PASS(AC 6/6) |
| **S20-16** foraging-003 三事件 + 身份铸造 | done | `EditMode/Foraging/harvest_orchestrator_test.cs`(**21**) | ✅ PASS(AC 2/4) |
| **S20-8** processing-002 F1 调用契约 | `ready-for-dev` | — | **未开工**(无证据,非失败) |
| **S20-13** processing-003 Craft 落流 | `backlog` | — | **未开工** |
| **S20-14** inventory-005 事务模板 + Drop\* 写者 | `backlog` | — | **未开工** |
| **S20-15** foraging-006 novelty 接线 | `backlog` | — | **未开工** |

**汇总**:计划内 **7 PASS · 0 FAIL**;追加批 **5 PASS · 4 未开工**。
**0 MISSING**(未开工件不计 MISSING —— 它们没有「应有而未有的证据」,是「尚未开始」)。

### 条数复算命令与实跑输出(承 `coding-standards.md` §数字须带可跑复算命令)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET, collections
r = ET.parse('unity/Logs/s20-7-full.xml').getroot()
c = collections.Counter()
for t in r.iter('test-case'):
    p = (t.get('fullname') or '').split('.')
    for i in range(len(p) - 1, 0, -1):
        if p[i].endswith('Test') or p[i].endswith('_test'):
            c[p[i]] += 1; break
for k in ('ClinicEnvDeliveryTest','ClinicPurityReplayTest','foraging_data_load_test',
          'foraging_bake_wiring_test','AdmissiblePredicateTest',
          'FurnaceTimingNoProcessStateTest','InventoryFoldTest',
          'StackingContainerRulesTest','CarryLoadAdjudicationTest','HarvestOrchestratorTest'):
    print(f'{k}: {c.get(k,0)}')
PY
```

实跑输出(`2026-10-27` QA 签核轮):

```
ClinicEnvDeliveryTest: 9
ClinicPurityReplayTest: 20
foraging_data_load_test: 46
foraging_bake_wiring_test: 6
AdmissiblePredicateTest: 29
FurnaceTimingNoProcessStateTest: 31
InventoryFoldTest: 28
StackingContainerRulesTest: 33
CarryLoadAdjudicationTest: 54
HarvestOrchestratorTest: 21
```

⇒ 与各 story `## Completion Notes` 自述条数**逐项相同**,零差异。

---

## 全名级差集结论

**sprint-20 区间的回归基线链**(每一步都带三元组):

| 时点 | 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|---|
| sprint-20 起点 | EditMode | `4867/4832/0/35` | `2026-10-25 08:27 CST` | `qa-full` | `4d468a5` |
| smoke 修复轮 | EditMode | `5385/5350/0/35` | `2026-10-27 01:13–01:20 CST` | `smoke-fix` | `5bdd226`+2 改动 |
| smoke 修复轮 | PlayMode | `204/204/0/0` | `2026-10-27 01:16 CST` | `smoke-fix` | `5bdd226`+2 改动 |
| S20-4 修复轮 | EditMode | `5414/5379/0/35` | `2026-10-27 10:07 CST` | S20-4 评审修复轮(全量) | `51193cf` |
| **S20-7 收口轮** | **EditMode** | **`5413/5378/0/35`** | **`2026-10-27 10:51 CST`** | **S20-7 全量** | **`51193cf` + 工作树** |

**末次全名级差集**(`s20-4-fix-full.xml` → `s20-7-full.xml`):

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
def names(p):
    return {t.get('fullname') for t in ET.parse(p).getroot().iter('test-case')}
a = names('unity/Logs/s20-4-fix-full.xml'); b = names('unity/Logs/s20-7-full.xml')
print('基线', len(a), '| 本次', len(b), '| 新增', len(b-a), '| 消失', len(a-b))
for n in sorted(a-b): print('  -', n)
PY
```

实跑输出:

```
基线 5414 | 本次 5413 | 新增 0 | 消失 1
  - DaYiJingCheng.Tests.MedConsequences.MedconsDelayedEmitTest.test_ac2_bake_cookedBytes_deterministic_sameSourceTwice
```

⇒ **消失的 1 条 = S20-7 按用户裁定当场删除的夹具**(`TD-medcons-16` 结案:
该夹具对行序回归零判别力 —— 两次走同一 `Dictionary` 枚举)。**新增 0 ⇒ 零回归,且是逐测试全名一致,非总数巧合。**

**sprint 总增量**:`4867 → 5413` = **+546**(全部为 sprint-20 区间新增;PlayMode `202 → 204` = **+2**)。

---

## 逐件分类核账

### S20-1(clinic-004 交付边界 · Integration)
- **类型一致性**:✅ 与 QA plan 一致(plan 记 Integration)。
- **证据在盘且全绿**:✅ `clinic_env_delivery_test.cs` 9 条,全 Passed。
- **证伪轮**:✅ 命中三形态(定义域收窄 / 守卫停用 / 阈值边界),F1/F2/F3 全部转红。
- **`NOT-RUN` 是否被当成已满足**:✅ 未。`K_CONTEXT_MAX` 的「上交系统 1」一腿 = 装配轮 NOT-RUN,
  卡内已明写;`AC-24-09` 按 EXTERNAL 移交 42 epic,不冒充已满足。
- **残余**:F1 证伪发现 b2 门对「声明未使用」asmdef 引用盲 ⇒ 已立 `TD-gate-04`(P0 面 `N`)。
- **判**:PASS。

### S20-2(clinic-005 纯度重放 + Memoize · Integration)
- **证据在盘且全绿**:✅ `clinic_purity_replay_test.cs` 20 条。
- **证伪轮**:✅ Falsifiability Gate **11/11 转红零 NOT-RUN** —— F7(`contexts[]` 多元素排序)
  原为 P0 结构性不可证伪,用户裁定「修复,不留债」后经 `internal CollectContextIdsFromRows`
  + `Sim/AssemblyInfo.cs` 的 IVT 解耦,`Sort→Reverse` ⇒ RED=1 恰为靶测。
- **评审**:✅ `review-s20-2-clinic005-2026-10-26.md`(两位均 APPROVED WITH SUGGESTIONS,零 BLOCKING)
  + `review-s20-2-names-2026-10-26.md`。W1 / W4 / W5 已修,D1–D3 + Q1–Q5 全处置。
- **残余(已登记)**:W4 指出 `static readonly` 容器双向漏网(断言 3 只扫实例字段)——
  已在评审原件登记,未修;属 WARNING 非 BLOCKING。
- **判**:PASS。

### S20-2a(TD-building-01 两半全补 · Integration)
- **证据**:`impl-s20-2a-building01-2026-10-25.md`(证据原件)。
- **债面翻转**:✅ `TD-building-01` + `TD-itemdb-01` **双 closed**(三十五订,未闭 P0 面 62→60)。
- **评审**:⚠️ **无独立评审原件** —— 载体是证据件而非 `review-*`。
- **判**:PASS WITH NOTES(见 C-5)。

### S20-3(foraging-001 · Logic)
- **证据在盘且全绿**:✅ `foraging_data_load_test` 46 + `foraging_bake_wiring_test` 6 = 52 条。
- **人工走查**:✅ `production/qa/evidence/foraging-05c-2026-10-26.md` —— 语义判据 **20/20 通过**,
  §六 由 `dr_guyang` 以主创/医学顾问 + QA 复核**双重身份** Approved。
  ⚠️ **同一人兼任两角色,非第三方独立复核** —— 已在卡内如实标注。
- **证伪轮**:✅ 第四轮首跑 W1/W2 `red=0` 属**真假绿**(真实 `MAX_QUALITY=5` 而 `cap_table` 最大仅 2
  ⇒ 写死与真读逐位同构),已补跨表耦合夹具后 **7/7 转红**。这是证伪门按设计工作的正向证据。
- **走查中的重大发现**:15/20 词无用户审定出处(GDD 前提此前不成立)+ 2 处采后/离株语义
  ⇒ 用户当场逐词审定 20 词、改 2 词(`cinchona q5` / `yarrow q1`)并重烘 21a。
- **判**:PASS。

### S20-4(processing-001 配方子集 + Admissible · Logic)
- **证据在盘且全绿**:✅ `admissible_predicate_test.cs` **29** 条(24 实现 + 5 评审修复轮)。
- **证伪轮**:✅ **8/8 恰如预期**(原 M1–M6 + 评审轮 M7/M8 钉 AR1 两处 query 校验);
  **SHA-256 逐字节还原通过,零 `FALSIFY-` 残留**。
- **评审**:✅ `review-s20-4-2026-10-27.md`(原判 APPROVED WITH SUGGESTIONS 零 BLOCKING;
  5 项 Required 全修 + AR2 接口继承一并落地 ⇒ **APPROVED**)。
- **AC 记分诚实度(必须与「4/4」同读)**:
  - `AC-18-10` = **2/3 真 + 炉半边 Spy 桩**(真装配归 Story 004);
  - `AC-18-20` = **结构性证明**(两条路径零生产调用点,真接线归 42 / Story 002);
  - `AC-18-05` 扫描面已由 asmdef `includePlatforms` 派生覆盖全部进构建装配。
- **本 story 抓到三条假绿**(均有留痕 + 夹具修复):
  ① 行为等价掩盖结构分叉(`IsCandidate` 曾自写 `== None`,8 组合 iff 矩阵全绿);
  ② **空洞正控**(正控只在字面量上跑 `IndexOf`,从未调用被测循环);
  ③ fail-closed 自述对 `default(struct)` 不成立(`readonly struct` 绕构造器)。
- **判**:PASS WITH NOTES。

### S20-5(接受侧判别力收紧轮 · Logic 推断)
- **载体**:`EditMode/ClinicMachine/` **11 件**(`ls | grep -c '\.cs$'` 实测 = 11)。
- **证伪轮**:✅ M0b 变异坐实(评审推断成立)+ 六处收紧 + M1 反证 **7 红**。
- **债面**:`TD-clinic-02 / 08 / 09` 三条 `open → closed`。
- **评审**:⚠️ 无独立评审原件 —— 载体 = `impl-tddc-2026-10-25.md`(该件自述「即评审报告原件替代物」)。
- **判**:PASS WITH NOTES(见 C-5)。

### S20-6(TD-prescription-07 材质分支分类器 · Logic 推断)
- **载体**:`world_billboard_test.cs`(**9**)· `dentch_dose_form_test.cs`(**23**);`SkeuoMaterialBranch`
  在两者中均有引用(grep 实测)。
- **证伪轮**:✅ M2 / M2b 双反证各 **2 红**;词门零命中。
- **债面**:`TD-prescription-07` `open → closed`。
- **评审**:⚠️ 无独立评审原件(同 S20-5,载体 = `impl-tddc`)。
- **判**:PASS WITH NOTES。

### S20-7(治理盘点轮 · Config/Data 推断)
- **载体**:债册 §「2026-10-27 · S20-7 治理盘点轮」九个小节(§一 W6–W10 逐条去向 · §二 W9/W10 豁免
  判据 + 触发条件 · §三 `TD-persist-08` · §四 `TD-enemy-03` · §五 W8 结案 · §六 `TD-medcons-16` 结案 ·
  §七 medcons 19 条策略 · §八 C-2/C-5 加注 · §九 账务四十四订)。
- **用户三裁已执行**:① W6–W10 逐条定案(五条各有具名去向)· ② QA C-2/C-5 如实登记为已解除 ·
  ③ medcons「16 本轮修,19 条按触发」。
- **账务**:条目 125→127 · open 76→77 · closed 43→44 · N 23→24 · **未闭 P0 面 63→63(两位相抵,非未动)**
  · epic 分布 9/31→10/31。
- **`debtcount.py --check` = exit 0**(且本轮中途曾观测到 exit 1,证明该门会红)。
- **诚实度记录**:§五 记录了 W8 的**一次返工**(首版只改头注,方法 doc 仍「三类」,跑判据时才发现),
  并把教训写成「『一并改了』是动作自述,不是判据」。
- **判**:PASS。

### S20-9(processing-004 单炉空闲判定 · Logic)
- **证据在盘且全绿**:✅ `furnace_timing_no_process_state_test.cs` **31** 条。
- **证伪轮**:✅ 14 条变异(新增 M13 钉 W1 / M14 钉 B1),**第 4 轮 14/14 恰如预期**;
  SHA-256 逐字节还原 + `FALSIFY-` 零残留 + 受保护路径指纹未变。
- **评审**:✅ `review-s20-9-2026-10-26.md` —— **1 BLOCKING + 8 WARNING + 4 MINOR**,已全修。
  **B1(BLOCKING)**:`IgnitionAdmission` 的 `outputs == null` 短路 = **fail-open**,绕过 AC-18-12
  唯一的时点拒绝门 ⇒ 改抛 `ArgumentNullException`。
- **AC 4/5**:`AC-18-15` **不勾**(EXTERNAL · BLOCKED-BY-ADR-012:三格矩阵不存在 + F7 spike 未跑,不写桩)。
- **卡面自述订正**:Implementation Note 1 写的「同 foraging Story 004 前缀纪律,共用扫描基建」**两条都不成立**
  —— foraging story-004 `Status` 仍是 `[ ] Not yet created`,`Sim/Foraging/` 四文件中零扫描器,
  `InventoryFold.OrderByFullOrderKey` 是 `private`。处置 = 未造该基建。**这是第五形态假绿(自述顶替实作)。**
- **判**:PASS WITH NOTES。

### S20-10 / S20-11 / S20-12(inventory 三件 · Logic)
- **证据在盘且全绿**:28 / 33 / 54 条,全 Passed。
- **证伪轮**:S20-10 **14/14** · S20-11 **14/14** · S20-12 **五轮演进 `10/13 → 13/13 → 15/16 → 16/17 → 17/17`**。
  S20-12 的五轮里**两次真假绿**(M5 夹具只断异常类型 / M16 漏了「提前返回」属性),均已修并复跑。
- **评审**:❌ **三件均无独立评审原件** —— S20-10 / S20-11 卡内明写「本轮未走评审」,
  S20-12 对此零陈述。见 **C-4**。
- **AC 记分**:S20-10 **2/4**(AC-20-03 / AC-20-16 结构性不可勾)· S20-11 **5/6**
  (P0 门 ①② BLOCKED-BY Story 010)· S20-12 **6/6** 但带四条限制。
- **S20-12 的四条限制**(必须与「6/6」同读):① `AC-20-21` 是**弱门**(领取次数无上界 ⇒ 对任何有限 cap
  几乎恒绿);② **构建期执行 = NOT-RUN**(门在 `AssemblyGates.RunAll` 的调用只有源码级证据);
  ③ `singleUnitWeightTable` 夹具判别面受限;④ `AC-20-08` 的 IL 面只在 Mono 后端。
- **回归口径警示**:S20-12 的增量是 **+54 不是 +141**(基线 `f3-full.xml` 测于 14:36,不含
  `089006b`/`ebe532e`/`40d9cfa`/`80eeab2` 四笔带入的 87 条)。
- **判**:PASS WITH NOTES。

### S20-16(foraging-003 三事件 + 身份铸造 · Logic)
- **证据在盘且全绿**:✅ `harvest_orchestrator_test.cs` **21** 条。
- **证伪轮**:✅ `sim_contracts_audit` 证伪 **13 条全红**。
- **评审**:✅ `review-foraging-003-2026-10-26.md` —— **CHANGES REQUIRED,BLOCKING 2**,
  两条均经编排方独立复核确认成立:
  **B-1**「任何前置失败都在任何写入之前抛出」**为假**(`Validate` 不核 `Novelty`/`XpGain`,
  这两项校验在第 ④ 步即最后一次写入之后,而第 ② 步已 `Append` ⇒ **写了一半可观测**);
  **B-2** 事件序与 story 相反(`ResourceHarvested` 先于其出生事件 `DropSpawned` 落流)。
  **两条均已修并经 M10/M11/M12/M13 独立变异证伪。**
- **AC 2/4**:`AC-17-01` 不勾(BLOCKED-BY 20 写者,由 S20-14 解锁)· R11 半边。
- **交付性质(必须同读)**:形状完备 + 自足夹具,**不是**可运行的采集链路
  (零生产调用点 / novelty 无生产者,后者即 S20-15 要修的)。
- **判**:PASS WITH NOTES。

---

## Bugs Found

**无已立案 bug。**

```bash
ls -la production/qa/bugs/ && ls production/qa/bugs/BUG-*.md 2>&1 | tail -1
```

实跑输出:

```
README.md          ← 目录内唯一文件
ls: 无法访问 'production/qa/bugs/BUG-*.md': 没有那个文件或目录
```

**但本轮的缺陷没有走 bug 通道** —— 全部走了债册(`+18` 条),包括两条 BLOCKING 级评审判定
(S20-9 的 B1 fail-open / S20-16 的 B-1/B-2)。这是既有处置口径(技术债 ≠ bug),
不阻塞签核,但**须知晓:BLOCKING 级缺陷在本项目不进 bug 目录**。

---

## Conditions

### C-1(治理级 · 须处置)追加批 4 件未完成,须显式结转

- **事实**:`production/sprint-status.yaml` 实测 —— `done 13 / ready-for-dev 1 / backlog 3`。
  未完成 4 件 = `S20-8`(ready-for-dev)· `S20-13` / `S20-14` / `S20-15`(backlog)。
- **性质**:这 4 件不是「计划内做不完」,是 **2026-10-26 用户裁定「甲:按链路顺序做到底」后
  追加的 8 件链**的剩余部分(`processing-001` 即 S20-4 不重复计)。
- **风险**:`S20-14`(inventory-005 事务模板 + **Drop\* 三支写者**)是链的**关键件**,
  也是 `S20-15`(foraging-006 novelty 接线)的唯一前置。链的终点**一件未动**。
- **签核要求**:① 4 件在 sprint-21 计划中**显式承接**(不得默认滚存);
  ② `sprint-21-candidate-collection-chain-plan.md` 的文件名(自称 sprint-21 而实际归属 sprint-20,
  文件内 `:103` 已自陈)**须裁定改名**,否则下轮 grep 找不到链的载体。

### C-2(治理级 · 须跟踪)`AC-18-15` 的 EXTERNAL 阻塞未解

- **事实**:`S20-9` 的 `AC-18-15` **不勾**,理由 = **BLOCKED-BY-ADR-012**(三格矩阵不存在 +
  F7 spike 未跑,不写桩)。
- **性质**:这是**裁定结果,不是遗漏**。ADR-012 的三格全免费常驻矩阵至今不存在,
  `TD-ci-01` 为 EXTERNAL 债。
- **签核要求**:`AC-18-15` 在 ADR-012 矩阵落地前**任何件都不得声称已满足**;
  且该 AC 的 **Seq 发号维**已另立 `TD-processing-07`(`P0 面 = Y`),两笔须同读。

### C-3(S4 · 覆盖率口径)smoke 覆盖率表的 6 条 `EXPECTED` 混了两种语义

- **事实**:`smoke-2026-10-27-sprint20.md:134` 记 `COVERED 10 · EXPECTED 6 · MISSING 0`。
  但 6 条 `EXPECTED` 里:
  - **4 条**是「未开工」(S20-8 / 13 / 14 / 15)—— 它们**没有应有而未有的证据**;
  - **2 条**是「smoke 执行时刻尚未落地」(S20-4 / S20-7)—— 二者均于 **10-27 当日补上**,
    现均有载体(`admissible_predicate_test.cs` 29 条 / 债册留痕)。
- **后果**:该表若被下一轮当作「S20-4 无测试」的证据,会**系统性低估覆盖率**。
- **签核要求**:smoke 报告的覆盖率表**就地加注**,区分「未开工」与「执行时点未落地」两种 `EXPECTED`。

### C-4(S4 · 治理)S20-10 / 11 / 12 三件承重件无独立评审

- **事实**:`production/qa/evidence/` 下**零** `review-*` 原件覆盖这三件;
  `story-001-inventoryof-fold-rebuild.md:262-264` 与 `story-003-stacking-container-weight-fold.md:547-548`
  明写「本轮**未走评审**」;`story-004-carryload-and-cap-adjudication.md` 对此**零陈述**。
- **性质**:三件都是链上的**承重件** —— `inventory-001` 的 fold 口径会向后传导到 003/004/005
  (卡内 §风险 表已自认「`inventory-001` 的 fold 口径错 ⇒ 003/004/005 全错」)。
- **对照**:同类件(`foraging-003`)的既有处置是「评审 → 修复 → 收口」三步,本轮跳过第一步。
- **签核要求**:三件在 sprint-21 **补评**(评**当下**代码,得**新**判定;
  按 `.claude/docs/review-workflow.md` §不可追补,**不追认**原判定),落 `review-*` 原件。

### C-5(S4 · 证据可复验性)S20-2a / 5 / 6 的载体是证据件而非评审件

- **事实**:三件均为债册工作项(`TD-building-01` / `TD-clinic-02/08/09` / `TD-prescription-07`),
  载体 = `impl-s20-2a-building01-2026-10-25.md` 与 `impl-tddc-2026-10-25.md`。
  后者自述「本件即『评审报告原件』替代物」。
- **性质**:`coding-standards.md` §Review Evidence Standards 的触发条件是「产出 **BLOCKING** 级评审判定」;
  这两件**零 BLOCKING**,故严格说**不触发**原件义务。但「证据件自任评审替代物」这一形态
  与「对账件不可替代原件」的既有裁量**同型**,须点名。
- **签核要求**:后续同类件的证据件**不得自称「评审原件替代物」** —— 若要主张已评审,
  跑一次 `/code-review` 并落 `review-*`;若未评审,如实写「未评审」。

### C-6(S4 · 提交完整性)收口提交未落,「被验的树」不可复现 — ✅ **已处置(2026-10-27 收口提交轮)**

- **事实(登记时)**:本报告写作时 `git status --porcelain` = **23 条**(**8 改 + 15 未跟踪**),
  含 S20-4 / S20-7 的**生产码**(`AdmissiblePredicate.cs` / `ProcessingCandidateSet.cs` /
  `ISingleFurnaceQuery.cs` / `ISkillLevelQuery.cs` / `EventBudget.cs` / `PrescribeFlow.cs`)
  与全部证据件 + 本报告 + retro + §九 本身。
- **后果(登记时)**:本报告写的「被验的树 = `51193cf` + 8 改 / 15 未跟踪」**在下次追溯时不可复现** ——
  未跟踪文件不在 git 历史里,无法 `git show` 还原。
- **签核要求(登记时)**:收口提交**落盘后**,把 commit 回填到本报告头部与 `sprint-20.md` §9.1。
- **✅ 处置(用户裁定「提交收口」)**:拆三笔提交落盘 ——
  `0a64faf`(S20-4 本体)· `34c75e9`(S20-7 治理盘点)· `d69d4b0`(收口四件套,本报告所在的那一笔)。
  复算:`git log --oneline 51193cf..HEAD` ⇒ **3 笔**;`git status --porcelain | wc -l` ⇒ **0**。
  ⇒ **「被验的树」自此可复现**,C-6 关闭。
  ⚠️ 三笔的哈希曾因 message 缺 `Co-Authored-By` 尾行而经 `commit-tree` 重建(旧 `779ff18` / `7cf8521` /
  `a0197f1`,**tree 逐位相同**),订正留痕见 `sprint-20.md` §9.3 ⑥。

---

## 未及核查项(禁借绿 —— 如实列出)

| # | 项 | 为什么没核 | 影响 |
|---|---|---|---|
| 1 | **未独立复跑 Unity 全量测试** | 重跑需引擎独占 ~2 分钟 + 多次调用;改为**独立复算 XML**(计数 + 全名级差集 + 条数),两套件结论与 smoke 自述逐项相同 | 采信的是 **XML 实体内容**,不是 agent 自述。若 XML 本身造假,本报告不能发现 |
| 2 | **证伪轮的「变异 → 红集」未逐轮重演** | 每轮需改生产码 + 跑测试 + SHA-256 还原;按各证据件自述累计 **约 105 轮**(S20-1 F1–F3=3 · S20-2=11 · S20-2a=4 · S20-3=7 · S20-4=8 · S20-9=14 · S20-10=14 · S20-11=14 · S20-12=17 · S20-16=13),远超预算 | 采信证据件的**表格记录**。已核:轮数、基准哈希值、还原方式、`git checkout` 禁令遵守情况均齐。⚠️ 该合计是**按件自述相加**,非逐轮实测 —— 不得当作「已验证 105 轮」 |
| 3 | **S20-10 / 11 / 12 的 AC 语义层逐条覆盖度** | 需逐条比对 GDD 与测试断言体;且三件无评审件可交叉验证 | 只核到「载体存在且条数吻合」;**语义层判定归 C-4 的补评** |
| 4 | **IL2CPP 跨平台逐位一致性** | 本机仅 x64;ARM64 目标不存在;CI 缺凭证(`TD-ci-01` EXTERNAL) | 矩阵级,**不得声称跨平台逐位一致**;`AC-18-15` 直接受影响(C-2) |
| 5 | **人工可玩性 smoke** | `tests/smoke/critical-paths.md` 自述「当前是清单,不是可执行门」,五组「当前可跑?」全 ❌ | 22 条一律 **N/A 不是 PASS**。已立 `TD-smoke-01`。**本 sprint 的可玩面未被任何自动化判据覆盖** |
| 6 | **S20-5 / 6 / 7 的 Type 为推断** | 三件在 `sprint-status.yaml` 里指向 `docs/tech-debt-register.md`,债册条目格式不含 `Type` 字段 | 已在 QA plan 同注;不影响判定,但**类型一致性**一项对这三件不成立 |
| 7 | **账本树 `tests/unit|integration/[system]/`** | 本 sprint 全部载体落在 `unity/Assets/Tests/`(主体编译树) | 不构成证据缺口(承 2026-10-15 注:两棵树都可能承载活测试,编译树是主体) |

---

## 溯源与复算命令

| 项 | 出处 |
|---|---|
| 件状态真源 | `production/sprint-status.yaml` |
| 测试结果 | `unity/Logs/s20-7-full.xml`(最新全量)· `s20-4-fix-full.xml`(前基线)· `smokefix-{editmode,playmode}-full.xml` |
| 债册复算 | `tools/debtcount/debtcount.py`(`--print` / `--check`) |
| 覆盖率 | `production/qa/smoke-2026-10-27-sprint20.md` §Test Coverage |
| 证据标准 | `.claude/docs/coding-standards.md` §Review Evidence Standards |

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 17 件状态分布 —— 实跑:{'done': 13, 'ready-for-dev': 1, 'backlog': 3}
python3 -c "
import re, collections
t = open('production/sprint-status.yaml', encoding='utf-8').read()
c = collections.Counter()
for m in re.finditer(r'- id: \"(S20-[0-9a-z]+)\"', t):
    e = t.find('\n  - id:', m.end()); seg = t[m.start():e if e != -1 else len(t)]
    c[re.search(r'\n    status: (\S+)', seg).group(1)] += 1
print(dict(c), '| 总件数', sum(c.values()))"

# ② 两套件计数 —— 实跑:5413 5378 0 0 35 / 204 204 0 0 0
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/s20-7-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=next(ET.parse('unity/Logs/smokefix-playmode-full.xml').iter('test-run'));print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"

# ③ bug 目录 —— 实跑:仅 README.md
ls production/qa/bugs/

# ④ 债册复算 —— 实跑:条目 127 / 未闭 P0 面 63 / 10/31;--check exit 0
python3 tools/debtcount/debtcount.py --print
python3 tools/debtcount/debtcount.py --check; echo "check exit=$?"

# ⑤ 三件无评审件(C-4)—— 实跑:零命中
ls production/qa/evidence/ | grep -E "inventory|s20-1[012]"

# ⑥ 收口提交完整性(C-6)—— 实跑:23 条(8 改 + 15 未跟踪)
git status --porcelain | wc -l
```

---

## 原文不删 · 就地加注

本报告为首版,无前序版本。判定基于 `2026-10-27` 的实测;**未跑项已在 §未及核查项 逐条列出**,
不得读作「已全部核查」。

`APPROVED WITH CONDITIONS` 表示**交付本身合格**(计划内 7 件全绿、零回归、零 S1/S2 bug),
**不表示 sprint-20 已可收口** —— 收口仍待 C-1…C-6 处置。其中:
- **C-1 / C-2 / C-4 / C-6 是治理与完整性条件**,可在 sprint-21 规划轮 + 收口提交中处置;
- **C-3 / C-5 是文档订正**,成本低;
- **C-2 的 EXTERNAL 阻塞不在本 sprint 能力面内**(依赖 ADR-012 三格矩阵)。

⇒ 若要转 `APPROVED`,须先闭 **C-4**(三件补评)+ **C-1**(4 件显式承接)+ **C-3**(smoke 覆盖率表加注)。
**C-6 已于 2026-10-27 收口提交轮关闭**(三笔提交落盘,工作树清零)。
