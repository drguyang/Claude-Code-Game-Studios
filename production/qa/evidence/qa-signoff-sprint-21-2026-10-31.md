# QA 签核报告 —— Sprint 21「P0 扫尾 · 债清 13 卡」

**日期**:2026-10-31
**签核人**:qa-lead(本会话代行;单人项目,全部角色由同一人签核)
**Sprint 文件**:`production/sprints/sprint-21.md`
**Smoke 报告**:`production/qa/smoke-2026-10-31.md`
**QA 计划**:`production/qa/qa-plan-sprint-21-2026-10-31.md`

---

## 一、实测基线(本轮实跑,非转述)

**基线溯源三元组**:截至时刻 `2026-10-31 12:28:14 – 12:31:12 CST` · 批次 `#21 批(收口后补)` · commit `57ed954a` + 未提交
（收口轮 `10:29:42–10:32:41 CST` · `bfca5781` 为 `6221/6184`;本轮 `+1` = 新增 `#21` 夹具）

| 套件 | 退出码 | XML | 对照基线 | 结论 |
|---|---|---|---|---|
| 全量 EditMode | **0** | `total=6222 passed=6185 failed=0 inconclusive=0 skipped=37` | 收口轮 `6221/6184/0/0/37` | **+1/+1 = 新增 1 测,恰等** |
| 全量 PlayMode | 2 | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | 收口轮 `238/234/0/2/2` | **逐位一致**;`exit=2` 伴 `failed=0`,非新失败 |
| 静态面 | — | `error CS` = **0** | — | 无编译面污染 |
| smoke 运行器 | **3** | `[INCOMPLETE]` 22 条:自动 PASS **10** · FAIL 0 · 未跑 **0** · 具名人工 3 · **无载体 9** | 无(首份) | **如实记为 INCOMPLETE,不记 PASS** |
| ↳ 订正 | — | 首版跑的是不带 `--results` 的模式,把 7 条有谓词的条目记成「未跑」;补跑后 7 条 **PASS**。**总数与 exit 不变(仍 3)**,但「未跑 7」是错的 | — | 见 `smoke-2026-10-31.md` §二 的 🔁 订正块 |
| 债册 | **0** | `debtcount.py --check` | — | 一致 |

---

## 二、DoD 逐条判定

| # | DoD 条目 | 判定 | 依据 |
|---|---|---|---|
| 1 | 开工前置 QA 计划已落 | ✅ | `qa-plan-sprint-21-2026-10-31.md`(2026-10-31 06:57) |
| 2 | Must Have 8 件全部完成 | ✅ | S21-1…S21-8 全部收口;**S21-3 内 3 条债留开且逐条具名**(见 F2) |
| 3 | 每件有证伪轮记录,`FALSIFY-` 零残留 | ✅ | 9 件有证伪轮记录(**逐卡条数见 `sprint-21.md` 各自的 ✅ 块,本件不另算总数**);4 件 N/A(纯文书 / 零产品码)。全仓 `grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0** |
| 4 | 回归三元组两套件全量零失败 | ✅ | 见 §一 |
| 5 | 债册 `--check` exit 0;`未闭 P0 面` = 0 | ✅ | 脚本实跑 exit 0;`未闭 P0 面 = 0` |
| 6 | 挂标账面 16 → 13,每笔附复算 | ✅ | `S21-8` 三条逐条复跑;**复核结果为「标记无需改」**(规划期已订正)—— 账面 13,证据在 §S21-8 |
| 7 | smoke-check `PASS WITH WARNINGS` | ✅ | `smoke-2026-10-31.md` §六,理由逐条 |
| 8 | QA 签核 | ✅ | 本件 |
| 9 | 收口判定与收口提交同日 | ✅ | 判定 `2026-10-31` / 提交 `2026-10-31` |

---

## 三、发现的问题(带严重度)

### F1 · **S3(既存债,非本 sprint 引入)** — 12 条 smoke 关键路径无载体

`run_smoke.py` 的 22 条里 12 条 `NO-CARRIER`(拾取 / 急救 / 病例结案 / 建造 / 疫情 / 诊断 /
采药 / 存档重放逐位 / 性能 / 跨构建)。逐条理由由脚本打印并归属既有债。
**本 sprint 未消解它们,也不得被本报告的 APPROVED 覆盖。**

### F2 · **S3(既存债)** — `S21-3` 的 6 条里 3 条留开

`TD-processing-03 / -04 / -06` 留开,各自具名理由(须 ADR-008 裁同键 `Craft` 语义 /
`BusyActorCount` 零生产调用方 / 真装配往返需独立卡)。**留开 = 不记绿。**

### F3 · **S3(机制债,维持 partial)** — `TD-s13-01`

`S21-11` 只补强了「增量落盘」的**机制**;其**有效性**仍为前瞻性判据,须下次真实重评实测。
**不得**读作「已解决」。

### F4 · **S2 → S3(签核后改判:零输入是 S5 裁定的推论,不是缺陷)** — `N-5`

`S21-10` 新增的 `N-5`(代价表不得指向终局型伤情)在**生产数据上今天不拦任何东西**
—— 注册表 15 行 `terminal_state` 全 `false`。

🔁 **2026-10-31 改判(首版曾把它写成「应给它输入」,那是错的)**:
GDD `disease-simulation.md:843`(S5 裁定)明定「昏迷 ⇔ `position_agg ≥ COMA_THRESHOLD ∧ 存活`,
**是状态投影,不进 D 集、无曲线**」⇒ **`INJ_COMA` 不得登记成注册表行**
⇒ 注册表**结构上**不存在终局型候选。**把 `INJ_COMA` 登记进来去喂 N-5 = 违反 S5 + 造第二表示**,
不是修复。⇒ N-5 是 **canary**(同 `N-4′`),它红的那天说明 **S5 先被破**。

**仍成立的两点**:
① 属性**作者自陈** —— 标错 `false` 不会被拦;N-5 保证「必须显式回答」,不保证答对。
② **间接路径仍开**(药害伤情恶化 → 9 结算成昏迷)⇒ `ADR-035 O-1` 不因本 sprint 消解。

### F5 · **S3 → ✅ 已闭(签核后补测)** — `S21-6` 的失败路径

「构建失败时 `PerformanceTestRun{Info,Settings}.json` 残留」一句原为**源码依据**
(`TestRunBuilder.cs:41-44` 的 `OnPostprocessBuild → Cleanup()`),**收口时未实测**。

**2026-10-31 补测(探针轮,三件套齐)**:
① 探针串 `[PROBE-F5]` 出现在构建日志;
② 栈帧落在 `DaYiJingCheng.EditorTools.Gates.AssemblyGates/BuildGate:OnPreprocessBuild`
(`AssemblyGates.cs:1437`);
③ 构建**因它失败**:`Build Finished, Result: Failure`,CLI 报 `Result: Failed;错误数 = 3`,exit=1。
④ **观测结果**:`unity/Assets/Resources/PerformanceTestRunInfo.json`(**3136 B**)+
`PerformanceTestRunSettings.json`(**23 B**)确实留在盘上;`.gitignore:93-94` 命中,
反例(同目录放非匹配文件)**未被忽略** ⇒ 规则未过宽。
⑤ **顺带新发现**:失败构建**还会**留下 URP `m_RuntimeSettings` 的剥离派生改动
(与 2026-10-21 同形)—— 已写进 `coding-standards.md` §工作树卫生。
还原:`AssemblyGates.cs` SHA-256 `0f713002…5b68` 逐字节一致,`PROBE-F5` 残留 0;探针产物已清。

---

## 四、覆盖汇总

| 判定 | 卡数 |
|---|---|
| COVERED | 9 |
| N/A(簿记 / 纯文书 / 零产品码) | 4(S21-1 / -8 / -12 / -13) |
| **MISSING** | **0** |

---

## 五、Verdict: **APPROVED WITH CONDITIONS**

**批准理由**:① 自动化两套件零失败且增量与新增测数**恰等**;② Must Have 8/8 收口,
每件都带可复算的证伪记录与 SHA-256 还原;③ 债册 `--check` exit 0、`未闭 P0 面 = 0`。

**条件(conditions,不满足即本签核失效)**:

- **C1** —— 本签核**不覆盖 §三的 F1–F4**(F5 已于签核后补测关闭,见 §三 F5)。下一轮读到这份 APPROVED 的人,
  须同读 `smoke-2026-10-31.md` §五 与本节。
- **C2** —— `F4`(N-5 零输入可达)**必须在下次有人登记终局型伤情时立即复核**;
  届时若标 `terminal_state = false`,门不会红 —— **人须先查**。
- **C3** —— `F2` / `F3` 的留开项在**首次被当作「已完成」引用**前必须回到各自债条复核(承 R1 复算纪律)。

---

## 六、可复算

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/debtcount/debtcount.py --check; echo "exit=$?"       # 期望 0
python3 tests/smoke/run_smoke.py; echo "exit=$?"                   # 期望 3(INCOMPLETE)
grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l            # 期望 0
cd unity && python3 - <<'PY'
import xml.etree.ElementTree as ET
for tag,p in (('EditMode','Logs/s21-10-final-em.xml'),('PlayMode','Logs/s21-10-final-pm.xml')):
    r=ET.parse(p).getroot()
    print(tag,r.get('total'),r.get('passed'),r.get('failed'))
PY
```
