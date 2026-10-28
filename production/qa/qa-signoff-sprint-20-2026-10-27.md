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
两套件零红零回归。**但存在 8 条须处置的条件**(2 条治理级 + 3 条证据完备性 + 1 条覆盖率口径 + 2 条覆盖率表事实错误),
故不判 `APPROVED`。
> 🔁 **条件数订正(2026-10-27)**:首版写「6 条」;写 C-3 加注时核表核出 **C-7 / C-8** 两条新缺陷
> ⇒ 现为 **8 条(C-1…C-8)**。详见 §Conditions 与 §原文不删·就地加注。
>
> ✅ **处置进展(2026-10-27)**:8 条中**已闭 4 条** —— C-3(加注)· C-6(收口提交)· C-7(补 `S20-2a` 行)·
> C-8(头部改实测值);**仍开 4 条** —— C-1 · C-2(EXTERNAL)· C-4 · C-5。
>
> 🔁 **2026-10-28 订正(原文上句不删)**:上述「仍开 4 条」是 **2026-10-27 时点**的快照,
> **现已失效**。此后 C-1(事实消解)· C-4(补评三份原件)· C-5(措辞撤回)**相继关闭**,
> 关闭痕迹见各条段末的 🔁/✅ 注记。**现仍开 = 1 条:C-2**(EXTERNAL,`TD-ci-01`)。
>
> **逐条现状**(以各段正文为准,不设机械判据):
>
> | 条件 | 现状 | 关闭痕迹所在 |
> |---|---|---|
> | C-1 | ✅ 已闭(由事实消解,非由裁定) | §C-1 末「✅ C-1 关闭(2026-10-27 …)」 |
> | **C-2** | **🔴 仍开(EXTERNAL)** | 段内无关闭注记;`TD-ci-01` 凭证须用户侧申请 |
> | C-3 | ✅ 已闭 | 节标题内「✅ **已处置(2026-10-27)**」 |
> | C-4 | ✅ 已闭(**2026-10-27** 关闭;2026-10-28 补段内指针) | 两节:`:558`「C-4 关闭「评审动作面」」+ `:585`「C-4 关闭「缺陷处置面」」;`§C-4` 段内只有指针(`:487`) |
> | C-5 | ✅ 已闭 | §C-5 末「✅ C-5 关闭(2026-10-27 …)」 |
> | C-6 | ✅ 已闭 | §C-6 内「该状态已…清零」 |
> | C-7 | ✅ 已闭 | §C-7 内补 `S20-2a` 行 |
> | C-8 | ✅ 已闭 | §C-8 内头部改实测值 |
>
> ⚠️ **刻意不提供机械复算命令** —— 本注初稿试过一条 awk 粗筛(「段内是否出现关闭措辞」),
> 实测**对 C-2 误报「有关闭注记」**(它的 `f` 标志跨节不重置,扫进了 C-3 的标题),
> 且对 C-3/C-6/C-7/C-8 漏报。**一条会说谎的判据比没有判据更坏** ⇒ 删除,改为上表逐条点名。
> 本条**存在性**可证伪(改上表任一行 ⇒ 与段内正文不符即可查);**有效性**靠人工核段内正文。
>
> 🔚 **收口裁定(2026-10-27 · 用户)**:**C-1 / C-4 不转** —— 不做结转承接、不补评审,**本 sprint 就此完成**。
> ⚠️ 这是**风险接受**,不是缺陷修复:4 件未完成 story 的去向与三件承重件的评审缺位**均未消解**,
> 只是被明确接受。详见 §Conditions 各条末尾的 🔚 裁定行与 §收口裁定登记。
> ⚠️ **verdict 维持 `APPROVED WITH CONDITIONS`** —— 关闭 4 条口径/事实类条件**不改变**判定,
> 因为仍开的 C-4(三件承重件无独立评审)与 C-1(4 件显式承接)属**实质面**,不是文档面。

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
  🔁 **2026-10-27 订正(C-5)**:该自述**已撤回** —— 本件是**修复证据件,未评审**。
  本行原判「无独立评审原件」**不变且更明确**:确实没有。
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
  → ✅ **2026-10-27 已改名** 为 `production/sprints/sprint-20-collection-chain-plan.md`(用户「6 裁决」)。

> 🔚 **原收口裁定(2026-10-27 · 用户):不转** —— 本条**不处置**,sprint-20 就此完成。
> **后果如实登记**(裁定 ≠ 问题消失):
> - **4 件 story 未完成,且未获 sprint-21 承接承诺** —— `S20-8`(`ready-for-dev`)·
>   `S20-13` / `S20-14` / `S20-15`(`backlog`)。它们在 `sprint-status.yaml` 中**保持原状态不动**
>   (不改 `removed`、不改 `done`),⇒ 下次 sprint 规划轮**必须**重新发现并决定去向。
> - **链的终点仍一件未动**:`S20-14`(Drop\* 三支写者)是 `S20-15`(foraging-006 novelty 接线)的
>   唯一前置,而 `AC-17-01` 的解除挂起**依赖这条链** ⇒ 该 AC 保持不勾。
> - ~~**链计划文件改名未裁**~~ —— ✅ **2026-10-27 已裁并执行**(见上 §C-1 签核要求 ②)。

> ✅ **C-1 关闭(2026-10-27 · 由事实消解,非由裁定)**
>
> **触发**:用户同日下达全链实现指令 `S20-8 → S20-13 → S20-14 → S20-15`(串行,后件依赖前件)。
> **结果**:四件**全部落地并转 `done`**,含 S20-15 的**强制证伪轮**(首轮 7/9 → 修 1 条夹具假绿
> + 修 2 条 spec 靶点 → 复跑 **9/9 恰如预期**,`exit 0`)。
>
> | 件 | 完成日 | 评审原件 |
> |---|---|---|
> | `S20-8` processing-002 F1 调用契约 | 2026-10-27 | `review-s20-8-2026-10-27.md` |
> | `S20-13` processing-003 Craft 落流 | 2026-10-27 | `review-s20-13-2026-10-27.md` |
> | `S20-14` inventory-005 事务模板 + Drop\* 三支写者 | 2026-10-27 | `review-s20-14-2026-10-27.md` |
> | `S20-15` foraging-006 novelty 接线 | 2026-10-27 | `review-s20-15-2026-10-27.md` |
>
> **复算**:
> ```bash
> python3 -c "import yaml,collections;d=yaml.safe_load(open('production/sprint-status.yaml'));print(len(d['stories']),collections.Counter(s['status'] for s in d['stories']))"
> # 实跑:17 Counter({'done': 17})
> ```
>
> **连带关闭的两条后果**:
> - ✅ 「链的终点仍一件未动」→ **已推翻**:S20-14 落地后 `AC-17-01` **已解除挂起并勾选**
>   (其前置 `inventory-items/story-005` 亦为 `Complete`)。
> - ✅ 「下次规划轮必须重新发现这 4 件」→ **不再需要**,它们已 `done`。
> - ⏳ 「链计划文件改名未裁」→ **仍开**,归本报告 §Conditions 之外的独立裁项(用户「6 裁决」)。
>
> ⚠️ **但「容量超支」这条不变,且更严重**:追加批 8 件**全部真的做了**,超出 1 件预留的口径
> 比原判断**更重**。该重估义务仍归下次 sprint 规划轮。
>
> ⚠️ **C-1 的关闭不追认原裁定** —— 原裁定「不转」在当时成立(事实确实未完成);
> 现在是**新事实**让它失效,不是原判断错了。

### C-2(治理级 · 须跟踪)`AC-18-15` 的 EXTERNAL 阻塞未解 — 🔁 **2026-10-27 阻塞面收窄,仍不勾**

- **事实**:`S20-9` 的 `AC-18-15` **不勾**,理由 = **BLOCKED-BY-ADR-012**(三格矩阵不存在 +
  F7 spike 未跑,不写桩)。
- **性质**:这是**裁定结果,不是遗漏**。ADR-012 的三格全免费常驻矩阵至今不存在,
  `TD-ci-01` 为 EXTERNAL 债。
- **签核要求**:`AC-18-15` 在 ADR-012 矩阵落地前**任何件都不得声称已满足**;
  且该 AC 的 **Seq 发号维**已另立 `TD-processing-07`(`P0 面 = Y`),两笔须同读。

> 🔁 **2026-10-27 处置(用户下达「2 完成三格矩阵」)**:阻塞面**收窄一格**,`AC-18-15` 仍**不勾**。
>
> **做了什么**:用户裁定「**落两格 + 第三格登记未达**」(第三格 ARM64 经 2026-10-17 探针实测
> **目标不存在** —— 46 个 `BuildTarget` 零个含 arm;`StandaloneLinux64` 是唯一受支持的 Linux 目标)。
> 据此新立 `tools/determinism-matrix/run_matrix.py` 并**实跑通过**:
>
> | 项 | 实测 |
> |---|---|
> | 格 1 Mono | `EditMode 53/53/0/0` · `PlayMode 3/3/0/0` |
> | 格 2 IL2CPP-x64 | `StandaloneLinux64 3/3/0/0`(构建并运行 player) |
> | 双格 19 条 vs 金标准 | 均**逐位相同** |
> | 双格彼此 | **逐位相同** |
> | 后端标记相异 | `Mono` vs `IL2CPP` ✅ |
> | ProjectSettings 还原 | **SHA-256 逐字节 = 基线**,`git status` 零输出 |
>
> ⇒ 五条判据全过,`exit 0`。证据原件:
> `production/qa/evidence/ac18-15-two-cell-matrix-2026-10-27.md`。
>
> **⚠️ 但 `AC-18-15` 仍不勾,理由两条(缺一都够)**:
> 1. **判据面差异** —— 两格矩阵测的是 **item-database 的 19 条黄金哈希**(`AC-21a-28/29` 面),
>    **不是** processing 的三出参。processing 侧目前**零黄金夹具**
>    (`grep -rln "golden\|Golden" unity/Assets/Tests/EditMode/Processing/` ⇒ 零命中),
>    其 PlayMode 件 `recipe_settlement_solver_test.cs` 是**同后端两入口对拍**,非跨后端。
>    ⇒ `AC-18-15` 缺的那一环 = **processing 侧可进 player 的黄金夹具**,归 processing 实现轮。
> 2. **ADR-012 §Validation 的判据是三格齐 + 常驻 + 版本化刷新协议** —— 三样缺两样半,
>    「两格可重跑」是把缺口**缩小**,不是**填平**。
>
> **⚠️ 也不得读成「C-2 已解除」** —— `TD-ci-01`(CI 凭证)仍 **EXTERNAL**,
> ARM64 格仍**长期未达**,两者都不在本 sprint 能力面内。
> **`AC-18-15` 在任何件里都**不得**被声称已满足。**

### C-3(S4 · 覆盖率口径)smoke 覆盖率表的 6 条 `EXPECTED` 混了两种语义 — ✅ **已处置(2026-10-27)**

- **事实**(**写 C-3 时点**):smoke 报告原 `Summary` 记 `COVERED 10 · EXPECTED 6 · MISSING 0`。
  但 6 条 `EXPECTED` 里:
  - **4 条**是「未开工」(S20-8 / 13 / 14 / 15)—— 它们**没有应有而未有的证据**;
  - **2 条**是「smoke 执行时刻尚未落地」(S20-4 / S20-7)—— 二者均于 **10-27 当日补上**,
    现均有载体(`admissible_predicate_test.cs` 29 条 / 债册留痕)。
- **后果**:该表若被下一轮当作「S20-4 无测试」的证据,会**系统性低估覆盖率**。
- **签核要求**:smoke 报告的覆盖率表**就地加注**,区分「未开工」与「执行时点未落地」两种 `EXPECTED`。
- **✅ 处置**:已就地加注(原表不删),加注含 ① 两种语义的拆分表 ② 收口轮现状 ③ 修正口径 ④ 可复算命令。
  ⚠️ **口径随 C-7 补行而变**:C-3 处置时写的是「有效 COVERED **12**」(`COVERED 10` + 2 条转绿);
  C-7 补 `S20-2a` 后基数变 11 ⇒ 当时为「有效 COVERED 13 · 未开工 4 · MISSING 0」。
  🔁 **2026-10-27 三度订正**:「未开工 4」(`S20-8`/`13`/`14`/`15`)同日**全部落地** ⇒
  **现为 `COVERED 16 · EXPECTED 1 · MISSING 0`**(仅剩 `S20-7` 治理轮,零新代码)。
  复算:
  ```bash
  grep -c '^| S20-' production/qa/smoke-2026-10-27-sprint20.md   # 17
  grep -n 'Summary.*COVERED' production/qa/smoke-2026-10-27-sprint20.md   # COVERED 16 · EXPECTED 1
  ```
  ⚠️ **本条的价值不因「4 件做完了」而消失** —— 它拆出的是**语义**问题
  (「未开工」与「执行时点未落地」混在一顶帽子下),那个问题在任何时点都是真的。

### C-7(新立 · 2026-10-27 · S3)smoke 覆盖率表**整行缺席 `S20-2a`** — ✅ **已处置(2026-10-27)**

> **来源**:写 C-3 加注时核表核出来的,**不在 C-3 面内**。
> **处置**:用户 2026-10-27 裁定「**C-7 补行**」⇒ 已补入 `S20-2a` 行。

- **事实**:表内 `S20-*` 行原 = **16**;`production/sprint-status.yaml` 件数 = **17**(含 `S20-2a`)。
- **缺席件**:`S20-2a`(TD-building-01 两半全补)—— `status: done` @ `2026-10-25`,
  载体实测在盘:`ModularBuilding/world_end_to_end_test.cs`(**8**)· `demolish_refund_from_catalog_test.cs`(**12**)。
- **后果**:`MISSING 0` 这个断言**成立**(缺席 ≠ 无证据),但本报告「逐件核」这句**不成立** ——
  表内少一件,且少的是**有证据的那一件** ⇒ 覆盖率被**低估**,方向与 C-3 同。
- **✅ 处置**:已补 `S20-2a` 行;Summary `COVERED 10 → 11`;「Missing Test Evidence」节同步 `10 → 11`。
  复算:`grep -c '^| S20-' production/qa/smoke-2026-10-27-sprint20.md` ⇒ **17**;
  `grep -c '^  - id: "S20-' production/sprint-status.yaml` ⇒ **17** —— **两数相等**。

### C-8(新立 · 2026-10-27 · S3)smoke 报告头部的件数与状态分布**三方不一致** — ✅ **已处置(2026-10-27)**

> **来源**:同 C-7,写 C-3 加注时核出来的。
> **处置**:用户 2026-10-27 裁定「**C-8 改成实测值**」⇒ 头部已改为与三元组 commit 一致。

- **原头部写**(`smoke-2026-10-27-sprint20.md:4`):「**16 件**中 **8 件 done**、3 件 ready-for-dev、5 件 backlog」。
- **实测(本报告三元组 commit `5bdd226`)**:「**17 件**,`done 11` / `ready-for-dev 3` / `backlog 3`」。
- **原表内 16 行自算**:「`done 10` / `ready-for-dev 3` / `backlog 3`」。
- **三方两两不等** —— 头部的 `8/3/5` **既不匹配 yaml,也不匹配本表**。最接近的是 `ebe532e` 时刻
  (`17 件 / done 8 / ready 3 / backlog 6`),仍差 backlog 1 与总数 1 ⇒ **头部是陈旧值**。
- **复算**:
  ```bash
  git show 5bdd226:production/sprint-status.yaml | python3 -c "
  import sys, re, collections
  t = sys.stdin.read(); c = collections.Counter()
  for m in re.finditer(r'- id: \"(S20-[0-9a-z]+)\"', t):
      e = t.find('\n  - id:', m.end()); seg = t[m.start():e if e != -1 else len(t)]
      c[re.search(r'\n    status: (\S+)', seg).group(1)] += 1
  print(sum(c.values()), dict(c))"
  # 实跑:17 {'done': 11, 'ready-for-dev': 3, 'backlog': 3}
  ```
- **性质**:这是**现行态断言**而非历史陈述,按 control-manifest 二分**本可直接改**。
- **✅ 处置**:头部已改为 `17 件 / done 11 / ready 3 / backlog 3`。
  **补 `S20-2a` 后表内 17 行自算亦为 `done 11 / ready 3 / backlog 3` ⇒ 三方现已全部对齐**
  (头部 = yaml = 本表)。复算:`sed -n '4p' production/qa/smoke-2026-10-27-sprint20.md` +
  `grep '^| S20-' … | awk -F'|' '{print $4}' | sort | uniq -c` ⇒ `11 done / 3 ready-for-dev / 3 backlog`。

### C-4(S4 · 治理)S20-10 / 11 / 12 三件承重件无独立评审

- **事实**:`production/qa/evidence/` 下**零** `review-*` 原件覆盖这三件;
  `story-001-inventoryof-fold-rebuild.md:262-264` 与 `story-003-stacking-container-weight-fold.md:547-548`
  明写「本轮**未走评审**」;`story-004-carryload-and-cap-adjudication.md` 对此**零陈述**。
- **性质**:三件都是链上的**承重件** —— `inventory-001` 的 fold 口径会向后传导到 003/004/005
  (卡内 §风险 表已自认「`inventory-001` 的 fold 口径错 ⇒ 003/004/005 全错」)。
- **对照**:同类件(`foraging-003`)的既有处置是「评审 → 修复 → 收口」三步,本轮跳过第一步。
- **签核要求**:三件在 sprint-21 **补评**(评**当下**代码,得**新**判定;
  按 `.claude/docs/review-workflow.md` §不可追补,**不追认**原判定),落 `review-*` 原件。

> ✅ **本条已关闭 —— 见下文「C-4 关闭」两节**(`评审动作面` + `缺陷处置面`)。
> 🔁 **2026-10-28 就地加注(原文不删)**:此处原无任何关闭指向,读者读到本段会以为 C-4 仍开。
> 补一行**指针**即可,不重复正文 —— 关闭证据在两节里,其中评审动作面一节带三份原件的逐条判定与复算命令。

### C-5(S4 · 证据可复验性)S20-2a / 5 / 6 的载体是证据件而非评审件

- **事实**:三件均为债册工作项(`TD-building-01` / `TD-clinic-02/08/09` / `TD-prescription-07`),
  载体 = `impl-s20-2a-building01-2026-10-25.md` 与 `impl-tddc-2026-10-25.md`。
  后者自述「本件即『评审报告原件』替代物」。
- **性质**:`coding-standards.md` §Review Evidence Standards 的触发条件是「产出 **BLOCKING** 级评审判定」;
  这两件**零 BLOCKING**,故严格说**不触发**原件义务。但「证据件自任评审替代物」这一形态
  与「对账件不可替代原件」的既有裁量**同型**,须点名。
- **签核要求**:后续同类件的证据件**不得自称「评审原件替代物」** —— 若要主张已评审,
  跑一次 `/code-review` 并落 `review-*`;若未评审,如实写「未评审」。

> ✅ **C-5 关闭(2026-10-27 · 用户下达「3 修复」后)**
>
> **修的是措辞形态,不是补评审。** 全仓扫「自称评审原件 / 其替代物」的措辞,共 **5 件**
> (含 C-5 原文没点名的 3 件同型 —— `impl-s20-1` / `impl-s20-2` / `impl-clinic-name-tables`),
> 逐件就地加注(`原文不删`,按 control-manifest 二分作为**现行态断言**处理):
>
> | # | 文件 | 原措辞 | 现况 |
> |---|---|---|---|
> | 1 | `impl-tddc-2026-10-25.md:6` | 「本件即『评审报告原件』替代物」 | **撤回自称**;明写「本件 = 修复证据件,**未评审**」 |
> | 2 | `impl-s20-1-clinic004-2026-10-25.md:5` | 「证据原件 = 本文件(承 …「评审报告原件落盘」)」 | 改为「这是实现证据,不是评审件」;**真评审件另在** `review-s20-1-clinic004-2026-10-25.md` |
> | 3 | `impl-s20-2-clinic005-2026-10-25.md:5` | 同上 | 同上;**真评审件另在** `review-s20-2-clinic005-2026-10-26.md` + `review-s20-2-names-2026-10-26.md` |
> | 4 | `impl-clinic-name-tables-2026-10-26.md:6` | 同上 | 同上;**真评审件另在** `review-s20-2-names-2026-10-26.md` |
> | 5 | `impl-s20-2a-building01-2026-10-25.md:5` | 「证据原件 = 本文件(承 §Review Evidence Standards)」 | 改为「实现证据」;**如实:本轮未走 `/code-review`,未评审** |
>
> **核心区分**(这几件的价值就在这条):
> - **有真评审件的**(2 / 3 / 4)—— 本件只需**停止冒充**,指出真的在哪。**评审义务本来就已履行**。
> - **真的没有评审**的(1 / 5)—— 本件须**如实说「未评审」**,不得用「替代物」这个措辞
>   制造一份不存在的义务的假履行记录。
>
> 复算:
> ```bash
> grep -rn "证据原件 = 本文件" production/qa/evidence/ | wc -l      # 实得 4(表内 2/3/4/5)
> grep -n "原件义务" production/qa/evidence/impl-tddc-2026-10-25.md  # 实得 :6(表内 1,措辞不同)
> grep -rn "评审报告原件.*替代物\|评审原件替代物" production/qa/evidence/   # 实得 2(原文 + 撤回注记,见下)
> ```
> ⚠️ **首句只数到 4 不是漏了一件** —— 表内第 1 件用的是「**原件义务**:本件即…替代物」,
> 措辞与前四件不同,须单独 grep。**两处合计 5**。
>
> > 🔁 **2026-10-27 就地订正(原文不删)** —— 上面第 3 条复算原写「零命中」并挂了
> > `| grep -v "订正\|不是"`,**那是假判据**:被排掉的是**订正注记**,而真正该被排掉的
> > **原文行** `impl-tddc-2026-10-25.md:6` 既不含「订正」也不含「不是」⇒ 过滤器排不掉它。
> > **实测输出 = 2 条**,都是 `impl-tddc-2026-10-25.md` ——
> > `:6` 是**原文**(按纪律保留),`:11` 是紧跟着的**撤回注记**(「本件**不是**『评审报告原件』,
> > 也**不是**它的替代物」)。即:C-5 的修复**形态是对的**,错的是我给它配的复算过滤器。
> >
> > **这恰恰是 C-5 要治的病本身**:用语料过滤把一条真实的旧措辞藏起来,再把结果写成「零命中」。
> > 正确判据不是「零残留」(按「原文不删」纪律,原文行**本就该留着**),而是
> > **原文行必须紧跟一条撤回注记**:
> > ```bash
> > grep -rn "评审报告原件.*替代物\|评审原件替代物" production/qa/evidence/
> > # 实得 2,同文件两行: :6 原文(保留)· :11 撤回注记 —— 恰好成对
> > sed -n '11p' production/qa/evidence/impl-tddc-2026-10-25.md
> > # 实得:> **错在哪**:本件**不是「评审报告原件」**,也**不是**它的替代物。两者的义务不同 ——
> > ```
>
> ⚠️ **本条不改变任何 story 的评审状态**:2/3/4 本来就有评审;1/5 本来就**没有** ——
> 修的是**记录的说法**,不是**事实**。**已有的仍已有,没有的仍没有。**

> 🔚 **原收口裁定(2026-10-27 · 用户):不转** —— 本条**不处置**(不补评),sprint-20 就此完成。
> **后果如实登记**(裁定 ≠ 问题消失):
> - **S20-10 / S20-11 / S20-12 三件承重件维持「无独立评审」状态**,`production/qa/evidence/`
>   下仍**零** `review-*` 原件覆盖它们。
> - 三件的向后传导面(**inventory 的 fold 口径**会传到 003/004/005)**未获评审背书** ——
>   后续若因该口径返工,返工成本不追溯到本 sprint。
> - `sprint-20.md` DoD 第 9 条「代码已评审」**维持部分成立**(6/10 有原件),不因本裁定改判。

> ✅ **C-4 关闭「评审动作面」(2026-10-27 · 用户下达「1 完成」后补评)**
>
> **三件全部交付独立评审原件**(评的是**当下**代码,得**新**判定;
> 按 `review-workflow.md` §不可追补,**不追认**原判定,不构成对原裁定的翻案):
>
> | 件 | 原件 | 判定 | BLOCKING / 核心发现 |
> |---|---|---|---|
> | `S20-10` inventory-001 | `review-s20-10-inventory-001-2026-10-27.md` | **CHANGES REQUIRED** | **B-1** `InventoryFold.Fold` **零生产调用点**(`grep -rn "InventoryFold\." unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 12 处全是注释);且 `R16`/`R17` 一旦出现建造事件即无条件报缺口 ⇒ 真实存档上 `IsComplete` **恒 false** ⇒ `RequireComplete()` **必抛**。**B-2** `ConflictingQualitySource` 分支**零夹具**(`grep -c` 测试件 = 0、spec = 0),而它在真实流上**可达**(重复采集 / 重放叠加 / 分段流拼接)。 |
> | `S20-11` inventory-003 | `review-s20-11-inventory-003-2026-10-27.md` | **CHANGES REQUIRED** | **B-1** `BuildPartQualityGate` **零调用点** ⇒ P0 范围门 ③ 的执法件不存在 = **第五形态假绿**(AC 记绿所依据的「不变量破坏 = 构建失败」在今天不成立)。另有 W-1(`StacksOf` 跨栈实例 id 记账失真)/ W-2(折重口径双实现)/ W-3(`InstanceWeight` 不可达 catch)。 |
> | `S20-12` inventory-004 | `review-s20-12-inventory-004-2026-10-27.md` | **APPROVED WITH SUGGESTIONS** | **W-1** 两条**同义反复断言**冒充证明(`carry_load_adjudication_test.cs:287-290` `long before = load; … EqualTo(before)` 中间零语句 · `:329-331` `long load = 10L; … LessThanOrEqualTo(cap)` 断的是测试自己写的常量)—— 与「自造断言顶替 AC 语义层」先例**完全同型**。W-2 `InventoryConstantsBinder` 零生产调用点。W-3 `CanCarry` 比较点未 `checked` 且溢出哨兵夹具缺席。 |
>
> **复算**:
> ```bash
> ls -1 production/qa/evidence/review-s20-1[012]-*.md | wc -l          # 3
> grep -c "ConflictingQualitySource" unity/Assets/Tests/EditMode/Inventory/*.cs   # 0(逐文件)
> grep -rn "InventoryFold\." unity/Assets --include=*.cs | grep -v /Tests/ | grep -v "^\s*//"   # 无人调用
> ```
>
> ⚠️ **本条的关闭范围 = 「有没有评审」,不是「代码是否通过」**:
> - 三份里 **2 份是 `CHANGES REQUIRED`** —— **评审动作完成**恰恰**证实了缺陷存在**。
> - 三条 BLOCKING/WARNING 级发现**全部是本 sprint 之前从未被任何人发现的**(fold 零接线 /
>   门零调用点 / 同义反复断言),⇒ 原来的「未评审风险」不是假想,是**真的**。
> - 这些发现**须另立工作项**进 sprint-21 候选或债册,**不得**因「原件齐了」就地结案。
>
> ⚠️ **不追认原裁定**:原裁定「不转」在当时成立(当时确实零原件);
> 现在是**用户新指令**改变了处置,不是原判断错了。

> ✅✅ **C-4 关闭「缺陷处置面」(2026-10-27 当日 · 用户裁定「不进21也不记债,现在完成」)**
>
> 🔁 **原文不删,就地加注**:上面那段末三行写着「这些发现**须另立工作项**进 sprint-21 候选或债册」
> —— **该处置已被用户推翻**。用户原话:「**不进21也不记债,现在完成**」
> ⇒ 三条发现**当场修完**,既未进 sprint-21,也未进债册
> (`grep -c "TD-inventory" docs/tech-debt-register.md` ⇒ **0**)。
>
> **处置结果**:
>
> | 原发现 | 处置 | 落点 | 反证 |
> |---|---|---|---|
> | `InventoryFold.Fold` 零生产调用点 | ✅ 建「装载库存实体」链 | `unity/Assets/Gameplay.Boot/InventoryLoadService.cs`(新建)+ `CompositionRoot.cs` 装配 + `CompositionRootServices.InventoryLoad` + `LoadSequencer.Load` 第 4 步 `afterRestore` 钩子 | 证伪 M1(把 fold 入参换成空数组)⇒ **9 条红** |
> | `BuildPartQualityGate` 零调用点 | ✅ 接上唯一强制点(判据面 = **装载期**) | 同上 `EnforceBuildPartQuality` | 证伪 M2(掏空调用)⇒ **2 条红** |
> | 两条同义反复断言 | ✅ 改为由被测件产出被断言值 | `carry_load_adjudication_test.cs` | — |
>
> 附带修:`StackingRules` 跨栈 `InstanceIds` 两条路径(S20-11 B-2)·
> `CarryLoad.CanCarry` 比较点缺 `checked`(S20-12 W-3)· `ConflictingQualitySource` / `qty<=0` 两条夹具(S20-10 B-2 / W-3)。
>
> **证伪轮**:5 条变异 **5/5 全红**、5 个被变异文件 **5/5 SHA-256 逐字节还原**。
> **全量回归**:`5552/5517/0/35`(`5529 + 23` 等式闭合,详见
> `smoke-2026-10-27-sprint20.md` §四度实测)。
>
> ⚠️ **诚实边界(禁借绿)**:
> - `IsComplete` 在**真实存档上仍恒 false**(R16/R17/R18 载荷缺位未补)—— 接线只兑现了「有人调它」。
> - **「玩家看得到库存」未接线**(20b 呈现侧零改动)。
> - `FoldWeight` / `StacksOf` / `Ratio` / `MinWeight` / `IsFullForAnyAddition` **仍零生产调用点**。
>   > 🔁 **2026-10-27 就地订正(原文不删)**:本行原把 **`CanCarry`** 也列进「零调用点」串 ——
>   > 那是**错的**,属把三个名字并成一串未逐个复算。实测 3 处真调用点:
>   > ```bash
>   > cd unity && grep -rn "CarryLoadSolver\.CanCarry(" Assets --include=*.cs | grep -v "^Assets/Tests/"
>   > # Assets/Editor.Tools.Gates/CarryCapExistenceGate.cs:168 · Assets/Sim/Processing/IgnitionAdmission.cs:137
>   > # Assets/Sim/ItemDatabase/InventoryTransaction.cs:220   ⇒ wc -l = 3
>   > ```
>   > ⚠️ **必带 `CarryLoadSolver.` 前缀** —— 裸 `grep "CanCarry"` 会把定义行 + 两处文档注释数进去(得 **6**)。
>   > `review-s20-12-inventory-004-2026-10-27.md:139` 原文即写「`CanCarry` 有真实生产调用点(3 处),✅ 已接线」。
>   > 空位改由同件 **M-1** 已登记的另一项 `IsFullForAnyAddition` 补上(同为 `CarryLoad.cs` 内零调用点的公开谓词)。
> - 门的判据面是**装载期**,不是 story 原文的「构建期」;「构建期 `RunAll` 是否真执行」仍 `NOT-RUN`(未做探针轮)。

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

### 2026-10-27 加注轮(写 C-3 时核出的连带)

**触发**:用户指令「补 C-3 的 smoke 覆盖率表加注」。写加注需要把表内 16 行逐行核一遍
⇒ 核出**两处此前未发现的缺陷**(C-7 / C-8),已追加为条件。

> ✅ **2026-10-27 处置**:用户裁定「**C-7 补行,C-8 改成实测值**」——两条**均已落地**。
> 补 `S20-2a` 后表内 17 行,与 yaml、与头部三方对齐(`done 11 / ready 3 / backlog 3`);
> `COVERED 10 → 11`。详见 §Conditions C-7 / C-8 的 ✅ 处置行。

**教训(与既有纪律同源)**:C-3 只要求「区分两种 `EXPECTED` 语义」,**核表是它的前置动作**。
若只按字面写加注而不核表,C-7 / C-8 会继续潜伏 —— 这与 `coding-standards.md`
§「某件有门」自述须给 `file:line` + 可跑命令的立规缘由**同型**:
**「按指令范围做完」不等于「指令指向的事实是对的」**。
本报告首版声称「sprint-20 全部 16 件,逐件核声明的证据路径」(`smoke:113`),
而 yaml 是 **17 件** —— 那句自述从写下的一刻起就不成立。

`APPROVED WITH CONDITIONS` 表示**交付本身合格**(计划内 7 件全绿、零回归、零 S1/S2 bug),
**不表示 sprint-20 已可收口** —— 收口仍待条件处置。

> 🔁 **2026-10-27 条件数订正**:首版为 **6 条(C-1…C-6)**;写 C-3 加注时**核表核出两条新缺陷**
> ⇒ 追加 **C-7 / C-8**,现为 **8 条(C-1…C-8)**。原文不删,此处改写计数。

其中:
- **C-1 / C-2 / C-4 是治理与完整性条件**,可在 sprint-21 规划轮处置;
- **C-3 / C-5 / C-7 / C-8 是文档与口径订正**,成本低;
- **C-2 的 EXTERNAL 阻塞不在本 sprint 能力面内**(依赖 ADR-012 三格矩阵)。

⇒ 若要转 `APPROVED`,须先闭 **C-4**(三件补评)+ **C-1**(4 件显式承接)。
**已闭 4 条**:C-3(smoke 就地加注)· C-6(收口提交落盘,工作树清零)·
**C-7 / C-8(2026-10-27 用户裁定「C-7 补行,C-8 改成实测值」,均已落地)**。
**仍开 4 条**:C-1 · C-2(EXTERNAL)· C-4 · C-5。

> 🔁 **2026-10-27 二次订正(用户下达「1 完成 / 3 修复 / 4 修复 / 5 更新 / 6 裁决」之后)**
>
> 上句「已闭 4 · 仍开 4」是**写那一刻**的事实,**现已过时**。现势:
>
> | 条 | 现状态 | 依据 |
> |---|---|---|
> | C-1 | ✅ **已闭(由事实消解,非由裁定)** | 追加批 4 件**全部落地**(9/9 `done`);见本文 §C-1 的 ✅ 块 |
> | C-2 | 🔁 **阻塞面收窄,仍不勾** | 两格矩阵已实跑绿;`AC-18-15` 仍缺 processing 侧黄金夹具 + ARM64 格 + CI 常驻;见本文 §C-2 的 🔁 块 |
> | C-3 | ✅ 已闭 | smoke 就地加注(三度订正后 `COVERED 16 · EXPECTED 1`) |
> | C-4 | ✅ **已闭「评审动作面」** | 三份 `review-s20-1[012]-*.md` 已交付;⚠️ 其中 2 份判 **CHANGES REQUIRED** |
> | C-5 | ✅ 已闭 | 5 处证据件措辞已订正;见本文 §C-5 的 ✅ 块 |
> | C-6 | ✅ 已闭 | 收口提交落盘,工作树清零 |
> | C-7 / C-8 | ✅ 已闭 | 补行 / 改实测值 |
>
> ⇒ **现势 = 已闭 7 条(C-1 / C-3 / C-4 / C-5 / C-6 / C-7 / C-8)· 收窄未闭 1 条(C-2)**。
> ⚠️ **但「已闭 7 条」不得读成「8 条全清」** —— C-4 的关闭只覆盖**「有没有评审」**,
> 而三份评审里有 **2 份判 `CHANGES REQUIRED`**,共 3 条 BLOCKING 级发现
> (`InventoryFold` 零接线 / `BuildPartQualityGate` 零调用点 / 同义反复断言)。
> **这些是新增的缺陷面,须另立工作项进 sprint-21 或债册。**

---

## 🔚 收口裁定(2026-10-27 · 用户)

> ⚠️ **本节记录的是「当时」的裁定,原文一字不改。**
> 其中 **C-1 / C-4 两行已被后续事实与用户新指令改变**:C-1 由事实消解(4 件全部落地)、
> C-4 由用户新指令「1 完成」而补评完毕。**行内 `~~删除线~~` 标出已被推翻的部分**,
> 依据见本文对应 §C-1 / §C-4 的 ✅ 块。**不追认原裁定**,只如实标注后续。

---

## 🔚 收口裁定(2026-10-27 · 用户)

**用户原话:「C-1 和 C-4 不转,本sprint完成」。**

| 条件 | 裁定 | 实质后果(**裁定 ≠ 问题消失**) |
|---|---|---|
| **C-1** 追加批 4 件未完成 | **不转**(不承接、不改状态) | ~~`S20-8` / `S20-13` / `S20-14` / `S20-15` 保持原状态留在 yaml;**链的终点仍一件未动**;`AC-17-01` 保持不勾~~ → ✅ **已被事实推翻**:四件**全部落地**(`S20-8`/`13`/`14`/`15` 均 `done`,`AC-17-01` 已解除)。见 §C-1 的 ✅ 块。⚠️ 链计划文件改名一项**已裁**(用户「6 裁决」= 改名不归档) |
| **C-4** 三件承重件无独立评审 | **不转**(不补评) | ~~S20-10 / 11 / 12 维持「无独立评审」;inventory fold 口径的向后传导**未获评审背书**~~ → ✅ **已被用户新指令改变**:三份评审原件**已交付**。⚠️ 但结果不是「背书」——**2 份判 `CHANGES REQUIRED`**,反而**证实**了缺陷存在。见 §C-4 的 ✅ 块 |
| **C-2** `AC-18-15` EXTERNAL | 未在裁定面内 | 🔁 **阻塞面已收窄**(两格矩阵实跑绿),**仍不勾** —— 缺 processing 侧黄金夹具 + ARM64 格(不可达)+ CI 常驻(EXTERNAL) |
| **C-5** 证据件自称评审替代物 | 未在裁定面内 | ✅ **已处置** —— 5 处证据件措辞已订正。见 §C-5 的 ✅ 块 |

### 本裁定的性质(须与「已处置」严格区分)

- **已闭的 C-3 / C-6 / C-7 / C-8 是「缺陷修复」** —— 改完之后,原缺陷**不再存在**。
- **C-1 / C-4 的「不转」是「风险接受」** —— 缺陷**原样存在**,只是决定不再投入处置。
  ⇒ 二者**不可混记为「8 条全清」**。真实状态是:**修好 4 条 · 接受 2 条 · 未动 2 条**。

> 🔁 **2026-10-27 订正**:上句是**当时**的真实状态。后续用户下达「1 完成 / 2 完成三格矩阵 /
> 3 修复 / 4 修复 / 5 更新 / 6 裁决」之后,**C-1 由事实消解、C-4 已补评、C-5 已修复**。
> **现势 = 已闭 7 条 · 收窄未闭 1 条(C-2)**。
> ⚠️ **但「风险接受」与「缺陷修复」的区分仍然成立,而且更重要了** ——
> C-4 的关闭**只关闭了「有没有评审」**,评审本身判定 2/3 件 `CHANGES REQUIRED`;
> C-1 的关闭也不追认原裁定。**关闭动作 ≠ 问题消失**,这条纪律不因条件数变化而松动。

### 对 verdict 的影响

本报告 verdict 维持 **`APPROVED WITH CONDITIONS`**,**不因本裁定升级为 `APPROVED`** ——
裁定关闭的是**处置义务**,不是**条件本身**。`APPROVED WITH CONDITIONS` 的语义正是
「交付合格,但带已知且具名的条件」,与「条件已被接受」并不矛盾。

🔁 **2026-10-27 二次订正**:条件数已从「修好 4 · 接受 2 · 未动 2」变为
**「已闭 7 · 收窄未闭 1(C-2)」**,但 **verdict 仍维持 `APPROVED WITH CONDITIONS`,不升级为 `APPROVED`**。
理由不变且**更硬**:C-4 闭的是「有没有评审」,而三份评审带出 **3 条 BLOCKING 级发现**
(`InventoryFold` 零生产调用点 / `BuildPartQualityGate` 零调用点 / 两条同义反复断言)。
**这些是新增的缺陷面,不能因为「条件表全绿」就消失** —— 它们须进 sprint-21 候选或债册。

**sprint-20 就此完成**(用户裁定)。残余项随 `sprint-status.yaml` 与本文档留档,
~~下次 sprint 规划轮**必须**重新发现 `S20-8/13/14/15` 并决定去向 —— 这是「不转」唯一未关闭的风险。~~
→ 🔁 **该风险已消失**:四件同日全部落地,无须下轮重新发现。
**但替代它的是更硬的一条** —— 上面那 3 条 BLOCKING 级发现。
⚠️ C-7 / C-8 是写 C-3 加注时**新发现**的 —— 它们的存在本身即「按指令范围做完 ≠ 指令指向的事实是对的」的实证。

---

## 🔁 收口后追加批覆盖(2026-10-28 · 原文不删,本节为补注)

> **为什么补这一节**:本报告是 **2026-10-27 收口时点**的签核。收口后用户又追加 **4 件**
> (`S20-17` / `S20-18` / `S20-19` / `S20-20`),它们**不在本报告的签核面内**
> (`grep -c "S20-1[789]\|S20-20"` 本文件 = **0**)。**不补这一节,本报告的
> `APPROVED WITH CONDITIONS` 会被读成覆盖了那 4 件。**

### 追加批的评审状态(本报告 C-4 的直接延伸)

| 件 | 独立评审原件 | Verdict | 与 C-4 的关系 |
|---|---|---|---|
| `S20-17` | `review-s20-17-2026-10-28.md` | APPROVED WITH SUGGESTIONS | 有原件 ✅ |
| `S20-18` | `review-s20-18-2026-10-28.md` | 原 CHANGES REQUIRED ⇒ 修复后 APPROVED | 有原件 ✅ |
| `S20-19` | `review-s20-19-2026-10-28.md` | CHANGES REQUIRED ⇒ A-5 修复后 APPROVED WITH SUGGESTIONS | 有原件 ✅ |
| `S20-20` | `review-s20-20-2026-10-28.md` | 原 CHANGES REQUIRED ⇒ 修复后 APPROVED | 有原件 ✅ |

> ⚠️ **四份均为事后补做**,评的是 **2026-10-28 当天树上的代码**,**不追认**各卡收口时点的判定。
> **C-4 的教训在追加批上没有再犯** —— 4/4 有独立评审原件,而收口批是 3 件承重件里 3 件无评审。

### 追加批补出的 5 条真缺陷(全部已修 + 已证)

| ID | 缺陷 | 严重度 |
|---|---|---|
| `S20-20 B-1` | `ApplyBakedInitial` 先置标志后跑本体 ⇒ 半应用 + 重试报文误导 | BLOCKING |
| `S20-20 B-2` | fail-closed 夹具骨架单格 ⇒「零落库」断言**恒真** | BLOCKING |
| `S20-20 S-1` | 导出门判 `== NoModule(-1)`,ADR-022 写 `≥ 1` ⇒ `module_id = 0` 漏过 | 须修 |
| `S20-18 B-1` | `Placed` 重放同号覆盖 ⇒ `Restore` 不碰占用表 ⇒ 旧足迹**永久阻塞** | BLOCKING |
| `S20-18 B-1b` | `StructureWriter.PeekNextId` 与注册表**双计数器**,两方向静默脱钩 | BLOCKING |

**证伪 4 条全 exact(M1/M2/M3/M4 各恰 1 条红)· 还原 SHA-256 逐字节 · 全仓 `FALSIFY-` 残留 0。**

### 对 verdict 的影响

**verdict 仍为 `APPROVED WITH CONDITIONS`,不升级。** 追加批**没有新增未闭条件** ——
它反而**消掉了 4 条**(上述缺陷已修)。但:
1. **挂标一条未勾**(`S20-17` 5 条 · `S20-18` 6 条 · `S20-19` 7 条 · `S20-20` 5 条)——
   与收口批同款形态,「条件表全绿」不等于「缺口已闭」;
2. 本报告 §C-2 的**收窄未闭 1 条仍在**,追加批未触及它;
3. `S20-18` 修复后仍留一条**未结语义问题**:schema 迁移期基线 id 段归属
   (**谁该赢**)需 ADR 级裁定 —— 该条**新增为候选条件**,登记在
   `review-s20-18-2026-10-28.md` §七-1 与 `sprint-20.md` §十三。

### 回归(三元组)

截至时刻 `2026-10-28 14:47 CST` · 批次 `S20-20 评审修复轮 · 全量` · commit `78d12421` + 未提交改动
· EditMode `5698/5663/0/0/35` · PlayMode `238/234/0/2/2`,**两套件零红**。
