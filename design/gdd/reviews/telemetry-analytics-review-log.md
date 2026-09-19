# 遥测与分析(系统 51)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## 结案追记(2026-09-18 · 用户裁定)—— ✅ **Approved**

用户裁定接受修订、**免二轮 → Approved**(承 37 / 42 先例)。**免二轮 = 显式风险接受,不是「已核对」**。
重开触发条件四者任一(全量见 `telemetry-analytics.md` 文末结案注):
① `OQ-51-1`/`4`/`5`/`7` 用户裁定落地后被实现证伪;② F1–F7 任一公式在实现期被证伪
(如 `AC-51-B3` 对拍不吻合);③ `TR-randomevents-024` covered 状态被撤销;④ 51 出现越界需求。

## Review — 2026-09-18 — **首轮评审结案:NEEDS REVISION → 1 BLOCKING + 3 Recommended 全部落盘 → 待用户裁定是否 Approved**
Scope signal: **M**
Verdict path: **首轮 `/design-review`(lean · 独立会话,评审者独立于撰写上下文)** ——
`production/review-mode.txt` 不存在 ⇒ 默认 `lean`(全部阶段,不委托 specialist agent)。
判 **NEEDS REVISION**(1 BLOCKING + 3 Recommended,全**文档级规格漏洞**,零机制重裁、零新真源)。
评审对全文**无架构异议** —— 架构姿态被确认:只读消费三流、零埋点、零出厂、P0 最小切面,
与 ADR-019 及上位架构(ADR-005/008/009 全序键 · R8 纯函数 · AC-19-01 零新埋点)自洽。

**1 BLOCKING + 3 Recommended 全部落盘**:
- **R1(BLOCKING)** F1 恒等式**不可满足** —— F2 `Scorable_single` 谓词 `|D| = 1` 与「无病」行
  (`D = ∅`)相斥 ⇒ `AC-51-B5` 断言 `M[无病][未落笔]` 入矩阵按当前文本**不可能成立**
  (「无病且判无病」与「误开方」两整类病例被谓词排除在矩阵外)。
  **修(F2 谓词实际修复,非仅加注)**:`|D| = 1` → **`|D| ≤ 1`**(对称哨兵,保住「无病」行;
  `|J| ≤ 1` 已对称保住「未落笔」列)—— 补「⚠️ 对称哨兵(`≤ 1` 而非 `= 1`)」注 +
  「与 F1 的槽位一致性」注(结果集取法)+ F1 恒等式第三项显式计入矩阵桶 +
  `AC-51-B5` 断言「无病」行亦非恒空(同源对拍覆盖)。
- **R2(Recommended)** `J(c)` = 全序最后一条,但全序键 `(Tick, StreamPriority, Patient, Seq)`
  不含 `case_id`,跨病例共病(ADR-008 §六「病例流不折叠」)时单凭键**无法定位** ⇒ 未钉死 ⇒
  静默实现依赖。**修**:Formulas「⚠️ 全序最后一条的折叠回写(R1 自举)」注 ——
  逐条推进**先经 `case_id` 落进对应 `c` 的槽位**(写槽覆盖 ⇒ 最后一条自然胜出),
  **不存在**「先选键再在键内找最后一条」的双层实现;`AC-51-B3` 补判定点。
- **R3(Recommended)** `ΔLevel` 的 `Level_end` 取法未钉死(读流终点 vs 玩家当前值)。
  取现值会把指标变成「玩家状态」的函数 ⇒ 违 R8(纯函数)。**修**:F4 公式 + 边界注 ——
  `Level_end` = **读流终点**(= 折叠终点)的成长事件携带值,**绝不读玩家当前 / 存档现值**;
  `Level_start` = 窗口起点前最后一个已知值;`Level` 携带点 = 成长事件(§Dependencies 补 30 行)。
- **R4(Recommended)** ① Q16.16 标量导出 `(num << 16) / den` 在 C# 是**截断**除法,
  文档断言含 `ROUND_HALF_AWAY_FROM_ZERO`(ADR-006 唯一舍入)但**未显示舍入步骤** ⇒
  两个实现者可能得不同数(违 R11 / `AC-51-C1` 逐位相同);② F5 半开窗口径 / F7 上中位数口径
  散文与公式并存,作者仍可能实现错版本。**修**:① 共同记号、`AC-51-D5`、工作例、`AC-51-C1`
  全部显式包 `ROUND_HALF_AWAY_FROM_ZERO(...)`(工作例订正 `44236 → 44237` —— 舍入前
  `44236.8`,截断得 `44236`、显式舍入得 `44237`,恰为分歧点示范);② F5 补半开窗注 +
  `AC-51-B12` 判据内嵌窗口口径;F7 散文补「实现一律引用定义公式」注。

**登记进裁决堆(冻结纪律)**:机制与数值**零改动**;输入依赖与数值旋钮仍归用户
(`OQ-51-1` / `4` / `5` / `7` / `9` 及 W/S/t_0/GAP_THRESHOLD/TELEMETRY_MIN_SAMPLE/
GROWTH_WINDOW,各注其闭合时点 —— `OQ-51-4` 补「两条路径对应 `AC-51-D8` 两分支」注)。

**附带核验(评审口径,均通过)**:`TR-randomevents-024` 状态 = **covered**
(`adr:` = ADR-019,2026-09-15 翻转)⇒ `AC-19-08` 成立;`difficulty_curve_window_density`
在 `entities.yaml:631` 已登记(51 侧引用 + 同源注补写);`game-concept.md:196/:615` 引用成立;
`diagnosis-system.md:746` 槽位语义成立;`skill-system.md` **未登记**「成长事件携带 `Level`」义务
(维持 `AC-51-D2` 的条件性)⇒ §Dependencies 上游表补 **30 行**。

**未被动摇**:文件头 Status **In Review**(本轮阻断均为文档级规格漏洞,非设计错误;
是否 Approved 由用户裁定)。**无 TR / 实体变更**(51 的指标全部落在既有 `Kind` 上 ⇒
`AC-19-01` 零新埋点成立;TR-randomevents-024 早已 covered,51 无新增 TR)。

Prior verdict resolved: **First review** —— 本条目为首轮。
