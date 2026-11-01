# story-006 实施证据 — 呈现白名单与零状态播报(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | `production/epics/death-respawn/story-006-presentation-whitelist.md`(UI · 5h) |
| **轮次** | 用户指令「推进 death-respawn」· UX 前置 → 守卫面实施 → 证伪 → 回归 |
| **结论** | UX spec Approved + 守卫面 8 测全绿;[L] 两条 NOT-RUN 待桌面批(如实登记) |

---

## 1. UX 前置(story 硬前置的兑现)

- **`design/ux/death-transition-29.md` 一次成文 + `/ux-review` APPROVED**(0 BLOCKING ·
  2 ADVISORY;评审轮补齐 ΦE 降级态与 null handling)。
- 关键设计裁定(全部标注 GDD/ADR 出处,见 spec 内引):零交互全屏水墨叠层(**不进
  ModalId 闭集**)· 两段式相机(自建机位,VR 全禁)· 远端 = 局部标识(第二 QoS IsDowned)·
  `DeathTransitionDto` 全整数零代价数值 · 输入抑制走 1 的 MotorSuppressed(呈现层不持状态)·
  **无跳过键**(设计裁定,登记 spec Open Questions 供主创复核)。
- **AC-UX-09(降级态)** 为评审轮自审补齐:素材缺失/DTO 查无 ⇒ 纯黑兜底,节奏不变。

## 2. 落盘件

| 文件 | 内容 |
|---|---|
| `design/ux/death-transition-29.md`(新,Approved) | UX spec(§1 闭集边界声明:全屏过渡 ≠ 模态,零焦点) |
| `Sim.Contracts/DeathTransitionDto.cs`(新) | 只读投影 DTO:`{Phase ∈ {0,1,2}, IsLocal ∈ {0,1}}` 全整数;`PresentationDtoGuard` 递归扫描覆盖(与 VitalsDto 同格) |
| `Sim/DeathTransitionQuery.cs`(新) | 投影纯函数:倒序扫流 —— 最近 `PlayerDied` 锚定;死亡后无落格 = Downed、有 = Reviving;活人日常跨格零误报;**零写路径** |
| `Tests/EditMode/DeathRespawn/death_presentation_guard_test.cs`(新,8 测) | 见 §3 |
| story-006 卡 | Last Updated + 本证据引用 |

## 3. AC → 测试映射(8 测;自动化面)

| story AC | 测试 |
|---|---|
| AC-29-07(DTO 纯度) | `test_dto_recursiveScan_zeroDiseaseToken_zeroCostFields`(真守卫 Scan + 代价词面/全整数双面;**载体非空** —— DTO 有真字段) |
| AC-29-07(负控 F4) | `test_dtoScan_negativeControls_turnRed`(float 掉落数 DTO 红 + disease 词面 DTO 红,独立于正例) |
| AC-29-06(白名单正向 + 违例红) | `test_whitelist_closedSet_positive_andViolationRed`(闭集 {DTO, Query};GameOver/统计面板桩件 ∉ 白名单) |
| 投影判据 | `test_projection_none_thenDowned_thenReviving_throughRealChain`(真写者链:None → 真写 PlayerDied ⇒ Downed → 落格 ⇒ Reviving)+ `test_projection_aliveActorDailyCellEvents_phaseNone`(活人跨格零误报) |
| 参数守卫 | `test_projection_invalidArgs_throw` |
| 呈现不持状态 | `test_deathPresentation_zeroWritePath_sourceScan`(DTO/Query 剥注释:零 `.Append(` / 零 `IEventSink`) |
| UX Flag(硬前置) | `test_uxFlag_specExists_andApproved`(读真 spec:Approved 在档) |

**[L] 两条(AC-29-11/12)= NOT-RUN 待桌面批**(节奏走查 + 听测,人工执行体,禁自动化代跑;
story-006 实施注 5 口径)。**AC-29-11/12 不记绿**。

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A3** DTO 掺入 `readonly float DropCount`(统计面板最小形态;ctor 同批赋值) | `DeathTransitionDto.cs` | ✅ 恰红 `test_dto_recursiveScan_zeroDiseaseToken_zeroCostFields`(代价词面 + 非 int 双违例同门);RESTORE-OK `e182bf16c9f7` |
| **M-B** 投影 Downed 支失守(`if (deathTick < 0)` → `if (true)`) | `DeathTransitionQuery.cs` | ✅ 恰红 `test_projection_none_thenDowned_thenReviving_throughRealChain`(Downed 判据);RESTORE-OK `e8d6f3a09cdc` |
| **M-C** 源码长出 `IEventSink` 词面(using 别名) | `DeathTransitionQuery.cs` | ✅ 恰红 `test_deathPresentation_zeroWritePath_sourceScan`(词面门判别力);RESTORE-OK `e8d6f3a09cdc` |

⚠️ **M-A 两轮无效变异如实登记**:M-A1(`float DropCount`)撞 **CS8340**(readonly struct 不收
非 readonly 字段)、M-A2 补 readonly 撞 **CS0171**(ctor 须全字段赋值)—— 均编译失败无结果,
**不算反证**;M-A3(字段 + ctor 同批)才构成有效变异。这一对编译错误恰好实证了该 DTO 的
readonly 结构性(「顺手加个统计字段」不是无门槛的)。残留 `grep FALSIFY` 零命中。
日志:`unity/Logs/s006-fals{A,B,C}.{xml,log}`(A = A3)。

## 5. 回归(三元组 · 终值)

> **中间态如实登记**:首轮全量 EditMode(17:18:08)`failed=1` —— S23-1 的
> `test_noOtherProductionWriter_appendsPlayerDied` 允许集拦下新读侧
> `DeathTransitionQuery.cs`(读 `EventKind.PlayerDied` 投影相位,零 Append)。
> **门是对的**(该测判据 = 新引用须有意识登记理由);修法 = 入集 + 理由注,不松断言。
> 复跑终值本表。

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **17:21:33 CST**(EditMode)/ 17:22:12 CST(PlayMode) |
| **批次** | | story-006(UX spec + DTO + 投影查询 + 守卫夹具 8 测) |
| **commit** | | 本批提交(见 git log story-006) |

| 套件 | 结果 | 前次(R-4-W-6 终值) | 等式 |
|---|---|---|---|
| EditMode | `total=6434 passed=6397 failed=0 skipped=37` | `6426 / 6389 / 0 / 37`(16:53:58) | `6434 − 6426 = 8` ✅ |
| PlayMode | `total=273 passed=269 failed=0 skipped=2` | `273 / 269 / 0 / 2` | `±0` ✅ |

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s006-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s006-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

<!-- REGRESSION-RESULTS -->

## 6. 已知边界(不借绿)

- **42 侧视觉实现未落**:水墨晕染层 / 相机两段式 / 环境音渐入 = 表现资产批 + 42 元件装配
  (spec §5.3/§9);本卡交付 = sim 投影面 + 守卫面,视觉装配归 42 的运行期轮。
- **[L] 两条 NOT-RUN**:无 Unity 桌面环境;证据文档落 `production/qa/evidence/` 待桌面批
  (story QA-3 edge case 口径)。
- **无跳过键**为设计裁定(spec Open Questions 5),待主创走查复核;若改判须同步 spec §7。
