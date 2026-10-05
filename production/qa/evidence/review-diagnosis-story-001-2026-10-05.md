# 评审原件 — diagnosis-system story-001(程序集边界与 VitalsDto 只读门面)

- **日期**: 2026-10-05
- **形态**: 双代理单轮评审(结构侧 + QA 侧,并行;评审只做一轮,修复后不复评)
- **对象**: `DiagnosisVitalsFacade.cs` · `DiagnosisGrowthExit.cs` · `DiagnosisBoundaryGates.cs` ·
  `AssemblyGates.cs`(接线两处)· `boundary_guard_test.cs`(25 条)
- **判定**: 结构侧 **CHANGES REQUIRED**(0 🔴 / 3 🟡 / 8 🔵)· QA 侧 **APPROVED**(0 🔴 / 2 🟡 收口前置 / 5 🔵)
  —— 均无 BLOCKING;本件按 Review Evidence Standards 留档为修复轮依据(原判定 → 修复落点 → 验证命令)

---

## 一、原判定(修复前)

### 结构侧(CHANGES REQUIRED)

| # | 级 | 判定 |
|---|---|---|
| S-1 | 🟡 | G-1 双层均漏 `UnityEngine.Mathf`(IL `LibmTypes` 与源正则均不含)—— G-4 禁 double 下作者有动机选 float 版 `Mathf`,漏它 = 族禁被邀请式绕行击穿;同装配 `Camera/CameraRig.cs:107` 已用 `Mathf.Sin` 证明可达 |
| S-2 | 🟡 | `VisitTypeRefs` 缺 `IModifierType` 修饰符侧遍历(b5 Required-2 已修之漏被重新引入)—— 携 `modreq(Sim.*)` 的 IL 可静默绿;头注「与 b5 逐行对齐」为假宣称 |
| S-3 | 🟡 | 时钟名单不全:`DateTimeOffset.Now` / `Stopwatch` / `Environment.TickCount` 两层全绿违 D-CLK 自述判据 |
| S-4 | 🔵 | 深度护栏注释称「超限不静默红行可见」,实现是静默 `return` |
| S-5 | 🔵 | 产物缺失时叠报第二条 `[D-0] 扫描键 0 命中`(真因在上一条,误导排障);源层两处扫描面错误用 `[D-TREF]` tag(IL 对应面是 `[D-0]`) |
| S-6 | 🔵 | AC-8-1 运行面 sink/哈希段结构性恒真(sink 从未交给 8)—— 须标注结构占位,防 story-done 引作已生效证据 |
| S-7 | 🔵 | 逃逸谓词前瞻性:前缀外合法消费者引用 8 公开 API 会红 —— 属设计张力,头注须写明「消费者必须住前缀」预期纪律 |
| S-8 | 🔵 | `IVitalsQuery` 无成员集锁(`GetVitalsV2` 扩员可绕 D-FACADE) |
| S-9 | 🔵 | BuildGate `out _` 吞 warnings —— 留口径注释 |
| S-10/S-11 | 🔵 | D-11 词面键局限归 11 轮回补;`typeof(Fix)`/按名反射构造性绕行登记 |

✅ 结构侧同时确认无误:接线无遮蔽/warnings 转发正确、三路假绿全拒、Cecil 零 `Resolve` 操作数全覆盖、
负例断言逐字对齐、契约 7 参保真、ADR-025/029 零触碰。

### QA 侧(APPROVED,附收口前置)

| # | 级 | 判定 |
|---|---|---|
| Q-1 | 🟡 | 全量 EditMode 回归未复跑(既有基线 019d 早于本批)—— 收口前须复跑,预期 0 failed |
| Q-2 | 🟡 | AC 勾选须括注 NOT-RUN 残余(AC-8-3 四输出归 002/003 · AC-8-6 定表归 003/004 · AC-8-4 11 侧归 prescription)—— 防「默示全判」 |
| Q-3 | 🔵 | story QA 行 66 的 `PatientState` 夹具实以 `RecipeDataSet` 承判据,story 未括注 |
| Q-4 | 🔵 | 铁律③ AC 字面「反射断言」vs 实现(IL+源,更强)—— Completion Notes 括注判据等价性 |
| Q-5 | 🔵 | 三处生产面单测空集可假绿(单 `--test=` 过滤跑)—— 补 `Is.Not.Empty` 自证 |
| Q-6 | 🔵 | 门谓词漏测分支:VerdictField Fix 字段分支 / 源层 IEventSink 正例 / 源层 D-FIX 正例 |

✅ QA 侧同时确认:25/25 通过、红路径逐字真实(全负例为必红式,无假红断言)、fullGate 五道空集红闸
(编译失败 / 产物缺失 / matched==0 / declCount==0 / 源根缺失)、诚实边界与 story 逐条一致(禁借绿达标)。

---

## 二、修复落点

| # | 修复 | 落点 |
|---|---|---|
| S-1 | `LibmTypes` += `UnityEngine.Mathf`;源正则改 `(?:Math\|MathF\|Mathf)`;注释登记 | `DiagnosisBoundaryGates.cs`(LibmTypes · CheckSourceText D-G1) |
| S-2 | `VisitTypeRefs` 补 `IModifierType.ModifierType` 分支;头注「逐行对齐」补差异登记 | 同上(VisitTypeRefs · 头注) |
| S-3 | `ClockTypes` += `System.DateTimeOffset` / `System.Diagnostics.Stopwatch`;`VerdictMethod` 特判 `Environment.TickCount(64)`;源正则补三族 | 同上(ClockTypes · VerdictMethod · D-CLK 正则) |
| S-4 | 超限改落红 `[D-0]`(承 PresentationDtoGuard 纪律),注释同步 | 同上(VisitTypeRefs depth>48) |
| S-5 | 缺失时不再叠报扫描键红(`!File.Exists` 短路);源层两处 `[D-TREF]` → `[D-0]` | 同上(RunAll · CheckSourceFiles) |
| S-6 | 测试头注 + 方法注补「结构占位,勿引作哈希判据已生效」 | `boundary_guard_test.cs`(test_ac81_runtime) |
| S-7 | 头注登记「消费者必须住前缀」预期纪律 + 构造性绕行登记 | `DiagnosisBoundaryGates.cs`(扫描键节) |
| S-8 | `IVitalsQuery` 成员集锁 `== {GetVitals}`(与 EmitGrowth 锁同构) | `boundary_guard_test.cs`(test_ac82_facade_shapeAndNullGuard) |
| S-9 | BuildGate `out _` 补口径注释(启用 WARN 时须同步转发) | `AssemblyGates.cs:1060` 前 |
| S-10/11 | 登记入门头注 + 本件残余节(非本批缺陷) | 头注 |
| Q-1 | 全量复跑(见 §三) | `unity/Logs/full-diag-001.xml` |
| Q-2 | AC 勾选逐条括注 NOT-RUN 残余 | story-001 AC 节 |
| Q-3 | story QA cases 括注 `RecipeDataSet` 替代 | story-001 :66 |
| Q-4 | Completion Notes 写明铁律③ 判据等价性(IL+源 ⊃ 反射) | story-001 Completion Notes |
| Q-5 | 三处补 `Is.Not.Empty` 扫描面自证 | `boundary_guard_test.cs`(ac83/ac86c/ac84) |
| Q-6 | 补负例:`Fix.One` 字段分支(IL)· 源层非注释 `IEventSink` 行 · 源层 `FixParse` 行 · `Mathf.Sqrt`(IL+源)· `DateTimeOffset`/`Stopwatch`/`TickCount`(IL+源) | `boundary_guard_test.cs`(EOF 夹具 + 三处源负例) |

## 三、验证命令(可证伪)

```bash
# 过滤跑(本 story 全部 25 条):
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s001-diag-fix.xml
# ⇒ 25 total / 25 passed / 0 failed  (2026-10-05 09:54Z)

# 全量回归(QA Q-1):
unity test unity --mode EditMode --output unity/Logs/full-diag-001.xml
# ⇒ 2561 total / 2515 passed / 0 failed / 45 skipped / 1 inconclusive
#   增量 115 = patient-ai s003(+53) + s004(+37) + diagnosis(+25);skip +2 = s004 的 2 个 [Ignore]
#   根 result=Skipped:Ignored 与基线 full-editmode-019d.xml 同态(43→45 个既有 Ignore),非本批回归
```

## 四、残余 NOT-RUN(禁借绿 —— 归属轮登记)

1. **AC-8-1 全流程脚本**(查体+落笔+2 改写)→ story 005/006;哈希对照段现为结构占位
2. **AC-8-1 跨 epic 空流基线**(disease story 002)→ CI 层(Implementation Note 4)
3. **AC-8-3 四输出**(display_词/把握度/四态)→ story 002/003
4. **AC-8-6 定表命中断言 + G-4 FMA 收缩 IL2CPP 实测** → story 003/004(AC-8-F5 矩阵)
5. **AC-8-4 11 输入契约反射半边** → prescription-medication epic story 003(epic 未开工)
6. **D-11 词面键回补**(若 11 侧以非 prescri 词面命名) → prescription 轮
7. **`typeof(Fix)` / 按名反射构造性绕行** → 11 / 005 轮复查项
