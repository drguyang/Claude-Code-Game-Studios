# 实现证据 —— 关卡工具 story-006(C1 / C2 / C2' / CI 接线)

> **对象**:`production/epics/level-tool/story-006-c1-c2-c2prime-and-ci.md`
> **日期**:2026-10-29
> **三元组**:截至 **2026-10-29 07:04 CST** · 批次 **story-006 实现轮** · commit **`5eba3a8e` + 工作树**
> **本件性质**:story 收口的**报告原件**(承 `coding-standards.md` §Review Evidence Standards)。

---

## §一 交付面

| 类 | 件 | 行数 | 作用 |
|---|---|---|---|
| 新增 | `Editor.Tools.Level/LatticeEngineParams.cs` | 106 | 引擎侧参数(已量化)+ `CharacterController` 探针 + **away-from-zero** 量化 |
| 新增 | `Editor.Tools.Level/WalkabilityParityCheck.cs` | 75 | **C2**(硬失败;`slopeLimit` / `stepOffset` **各一条独立判据**) |
| 新增 | `Editor.Tools.Level/ColliderFootprintCheck.cs` | 171 | **C2'**(硬失败;高 collider / 可走过例外 / 足迹一致性) |
| 新增 | `Editor.Tools.Level/VisualLayerSource.cs` | 73 | C1 的**作者侧**视觉层声明(逐格整数毫米) |
| 新增 | `Editor.Tools.Level/LayerDriftCheck.cs` | 103 | **C1**(告警;**双向**比对) |
| 新增 | `Editor.Tools.Level/LevelCiRunner.cs` | 174 | CI 入口(**0 / 1 / 2 三态退出码**) |
| 新增 | `tools/level/run_ci.sh` | 75 | CI 单命令(含工程独占检查) |
| 新增 | `Tests/EditMode/LevelTool/consistency_c1_c2_c2prime_test.cs` | — | **20 条** `[Test]` |
| 改动 | `LevelConsistencyCheck.cs` | — | `CheckReport` 增**第三态 `NotRun`**;接三条检查;`UnimplementedChecks` 3 → **0** |
| 改动 | `LevelSource.cs` | — | 增可选 `VisualLayer` / `ModuleFootprints`(缺省 `null` = 未声明) |
| 改动 | `docs/architecture/adr-015` / `adr-016` | — | AC-6 的两处回指注记 |
| 改动 | 3 个既有夹具 | — | 条数判据随 `UnimplementedChecks` 归零同步(见 §六 · 2) |

---

## §二 AC 逐条判定

| AC | 判定 | 判据 / 载体 |
|---|---|---|
| **AC-1** C2 硬失败(`\|engine − baked\| ≤ 1 量子`,**两个量各一条**) | ✅ | `test_ac1_slopeLimit_zeroOneTwoQuantum_boundaries` · `test_ac1_stepOffset_…` · `test_ac1_slopeDriftOnly_reportsSlopeNotStep`(两量不合并)· `test_ac1_noEngineParams_isNotRun_notGreenNotRed`(三态)。证伪 **F1 / F2 / F3 / F15** 全红 |
| **AC-2** C2' 硬失败(高 collider 无声明 ⇒ 抛;**有声明 ⇒ 不抛**;足迹不一致 ⇒ 抛) | ✅ | `test_ac2_highCollider_withoutDeclaration_…` · `…_withDeclaration_isClean`(例外路径可达)· `…_footprintMismatch_…` · `…_heightEqualToStepOffset_isClean`(严格 `>` 的边界)· `…_noCatalog_footprintHalfIsNotRun`。证伪 **F4–F7** 全红 |
| **AC-3** C1 告警(注入漂移 ⇒ **报出**;**不**使构建失败) | ✅ | `test_ac3_logicSolidVisualEmpty_warnsAndStaysGreen`(方向①)· `…_visualSolidLogicEmpty_warns`(方向②)· `…_noDrift_noWarning`(「恒报」的杀手)· `…_noVisualLayer_isNotRun`。证伪 **F8 / F9 / F10** 全红 |
| **AC-4** CI 接线(一条命令跑完;硬失败非零退出;告警零退出) | ✅ | `tools/level/run_ci.sh` + `LevelCiRunner.RunBatch`。**端到端实跑见 §四**:干净 ⇒ **0**;注入缺陷 ⇒ **1** 且报文点名 `C2 slopeLimit`;夹具另证链坏 ⇒ **2** |
| **AC-5** `UnimplementedChecks` 长度 == 0 | ✅(**口径见 §六 · 1**) | `test_ac5_unimplementedChecks_isEmpty` |
| **AC-6** 两处 ADR 回指到具体检查 | ✅ | `adr-015-world-geometry-fixed-world-lattice.md` §Validation(C1/C2/C2' → 三个文件 + CI 入口)· `adr-016-ai-architecture.md` §Validation(C3 → 判据 / 采样器 / CI) |

---

## §三 证伪轮(S12-4 · 15 条变异,**全部 RED**)

> 每条 = 改一处生产码 → 跑**一条**夹具 → 还原 → **SHA-256 逐字节**校验。
> 驱动器:`/home/gu/.claude/jobs/181888b1/tmp/falsify_story006.py`(临时件,不随仓库出).

| ID | 变异 | 夹具 | 结果 |
|---|---|---|---|
| F1 | 停用 `slopeLimit` 判据 | AC-1 slope 边界 | **RED** |
| F2 | 停用 `stepOffset` 判据 | AC-1 step 边界 | **RED** |
| F3 | 阈值 `≤1` 放宽为 `≤2` | AC-1 slope 边界 | **RED** |
| F4 | C2' `>` 改为 `>=` | 等高边界 | **RED** |
| F5 | 删「可走过」例外(恒抛) | 有声明 ⇒ 不红 | **RED** |
| F6 | 删足迹比对 | 足迹不一致 | **RED** |
| F7 | 缺目录 ⇒ 静默跳过(不记 NotRun) | C2' 未跑登记 | **RED** |
| F8 | 停用 C1 方向① | 逻辑有/视觉空 | **RED** |
| F9 | 停用 C1 方向② | 视觉有/逻辑空 | **RED** |
| F10 | C1 恒报 | 无漂移基线 | **RED** |
| F11 | `Run` 退出码恒 0 | 注入缺陷 ⇒ 1 | **RED** |
| F12 | 「链坏」与「有违例」同码 | null 源 ⇒ 2 | **RED** |
| F13 | `Quantize` 换 `Math.Round`(ties-to-even) | 半量子取整 | **首轮 GREEN(假存活)→ 修夹具 → RED**,见 §五 |
| F14 | 探针对调两个字段 | 探针字段独立性 | **RED** |
| F15 | 不登记 `NotRun` | 无引擎输入 ⇒ 未跑 | **RED** |

**终态 15 / 15 红,零假存活。** 全部 15 条的还原均 `before == after`(无 `RESTORE-FAIL`)。

---

## §四 端到端 CI 探针(**AC-4 的核心证据**)

「命令存在」不等于「命令会红」,故实跑两次:

| 轮 | 操作 | 退出码 | 关键输出 |
|---|---|---|---|
| 干净 | `bash tools/level/run_ci.sh` | **0** | `✓ 通过(退出码 0)`;同时打印 `○ 未跑 2 条(C1 / C2')` |
| 注入 | `P0ClinicLevel.SlopeLimitDegX100` `4500 → 3000`,再跑同一命令 | **1** | `✗ 硬失败 1 条: C2 slopeLimit 不同源:引擎侧 4500(0.01 度)≠ 烘焙侧 3000,偏差 1500 量子 > 1` · `○ 未跑 2 条` |

**还原**:`SHA-256 d34be19b…8be90` 前后**逐字节一致**(`RESTORE=OK`)。

> ⚠️ **这次比对的是「烘焙值 vs 引擎默认」** —— 今日 `PlayerController.ApplyControllerParameters`
> 尚未写 `slopeLimit` / `stepOffset`(那两条归系统 1,债册 `TD-player-01` 的未解半边),
> 所以探针读的是 `new GameObject + CharacterController` 的 Unity 默认值(45° / 0.3 m)。
> 这是**当下真实的两侧**,不是伪造:一旦有人只改一边,CI 立刻红。
> 系统 1 开始写这两个值时,`RunBatch` 应改为从玩家构造代码取同一份配置(已写在方法注里)。

---

## §五 本轮抓到的**假绿**及修法

| 变异 | 首轮 | 成因 | 处置 |
|---|---|---|---|
| **F13** | **GREEN** | 夹具用 `0.0125f × 1000` 期望 13,但 `0.0125f` 转 `double` 是 `0.012500000186…`,乘 1000 = **`12.500000186`**,**不在中点** ⇒ `Math.Round` 同样给 13 ⇒ 变异与原文同结果,**该夹具对舍入模式零判别力** | **修夹具,不改实现**:改用 `0.5f × 1`(二进制精确中点)⇒ away 得 1、ties-to-even 得 **0**,必然可分。复跑 **RED** |

> **教训(与本仓 `project-falsification-probe-pitfalls` 的「边界夹具不整除」同型)**:
> 边界夹具的输入必须**离线按目标精度算过**它是否真的落在中点;写成「看起来像 0.5」是不够的。

---

## §六 诚实边界(本 story 证明不了的事)

1. **AC-5 的「0」读作「七项都有实现」,不读作「七项在生产上都跑出了结论」。**
   今日真正跑出结论的是 **C2**(引擎侧有真对象)与 **C3**;**C1 / C2' 在生产上必然进 `NotRun`** ——
   全库零视觉层资产(`World.unity` 仅 `Directional Light` + `Ground`)、零模块 prefab。
   落地方式 = `CheckReport.NotRun` **第三态**(与 `Violations` / `Warnings` 类型可分),
   CI 每次都会把它打印出来,**不静默跳过**。
2. **三处旧夹具随判据迁移改了形态**(原文保留 + 加注):
   - `level_tool_consistency_test`:断言 **3 → 0**;
   - `nav_chunk_slicing_test::test_ac6_…leavingThree`:宾语消失 ⇒ 转 **`Assert.Ignore`**(计入 skipped +1);
   - `level_source_generalization_test::test_ac4_…`:清单为空 ⇒ 转 **`Assert.Ignore`**(skipped +1)。
   ⚠️ **两条 skip 不是「跳过失败」,是「无宾语」** —— 已把这一点写进两处报文。
3. **C2' 的 prefab 侧未跑** —— 本 story 只做到「声明之间互相一致」;「声明的 collider 高度 == prefab 实际」须待 23 素材轮(今日全库零 prefab)。
4. **C1 的「不矛盾」仍是布尔判据**(视觉格有/无实体),不是「视觉高度够不够看出是张桌子」—— 后者是美术面。
5. **CI 在真实 CI runner 上的行为未验** —— 本 story 只证明**本机**一条命令会红/会绿。真实 runner 上还缺 Unity 许可与缓存(债册 `TD-medcons-09` / `TD-clinic-16` 同触发点)。
6. **`slopeLimit` / `stepOffset` 的数值合理性** —— 归用户数值轮;本 story 只检「两侧一致」。

---

## §七 本轮我自己的错误(如实登记)

| # | 错 | 后果 | 修 |
|---|---|---|---|
| 1 | 首版 `LevelConsistencyCheck` 写成 `if (TryCheck(...)) violations.AddRange(...)` | **C2 恒绿**(有违例时一条都不加)。被夹具当场抓到(3 条红) | 改为不看返回值、只取列表 |
| 2 | 证伪驱动器首轮 `-testFilter` **漏了类名** | 15 条全部 `total=0` 且退出码 0 ⇒ 整轮「全绿」其实是**一条没跑** | 补类名;**并给驱动器加 `FILTER-MISS(未跑到)` 判据**,与绿分开 |
| 3 | F13 夹具边界不落在中点 | 变异假存活 | 见 §五 |

> ⚠️ 第 2 条值得进 `coding-standards.md`:现有那条「判跑过看 `total > 0`,不看退出码」
> 只覆盖了**人读 XML** 的场景;**自动驱动器**也会踩同一个坑,且它会把 `total=0` 记成绿。

---

## §八 回归

| 套件 | 结果 | 对照基线 |
|---|---|---|
| EditMode | **`5842 / 5805 / 0 / 37`** | 接线轮 `5822 / 5787 / 0 / 35` ⇒ **total +20 = 新增 `[Test]` 恰 20**;passed +18(20 − 2 skip);skipped +2(两条 `Assert.Ignore`);**failed 0 → 0** |
| PlayMode | **`238 / 234 / 0 / 2 / 2`** | 与既有基线**逐项相同**(2 Inconclusive + 2 Skipped) |

> ⚠️ **PlayMode 的进程退出码是 2**(Unity:「One or more tests failed」),而 **XML 里 `failed = 0`**。
> 成因 = 那 2 条 **Inconclusive** 被 Unity 计入退出码。**与本轮改动无关**(基线同样是 `238/234/0/2/2`)。
> 按本仓判据「判跑过看 `total > 0` 与 XML 计数」,本轮记 **绿**;退出码的这处不一致**登记在此**,不隐瞒。
