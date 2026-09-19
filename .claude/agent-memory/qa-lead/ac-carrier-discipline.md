---
name: ac-carrier-discipline
description: 本项目 AC 复审的两条首要检查 —— 每条 AC 须有具名载体,且判据输入不得依赖未定项;草稿全 BLOCKING 零 ADVISORY = 红旗
metadata:
  type: feedback
---

复审任何 GDD 的 §Acceptance Criteria 时,**先跑这两道检查,再看内容**:

1. **载体检查** —— 每条 AC 必须能说出一个具名载体(EditMode 断言 / PlayMode 用例 / asmdef 引用集白名单 /
   Roslyn 分析器 / 构建期 preprocessor / 场景资产扫描 / 文本数据 lint / CI 命名 job / 手工走查+签核 / spike 报告件)。
   说不出载体的 = 不是 AC,是设计意图。**载体的存在性同样要查** —— 本仓 `.github/workflows/` 与 `tests/`
   长期不存在,凡依赖它们的 AC 都是「判据已定、载体未建」(汇总先例:`input-system.md` §Dependencies 五)。
2. **输入项检查** —— 判据的输入若依赖未定项(最低目标硬件 / 未完成的 spike / 无 GDD 的下游系统 / 未裁的 OQ),
   标 BLOCKING 只会造出**永久无法签核的阻塞**。立法先例 = `AC-3-B1b`(硬件实测延迟)、`AC-3-F1a` 四轮
   「主语画错一格」(把不出诊箱的 20 写成出诊箱)。

**Why**:用户明确点名要防「以 BLOCKING 之名行不可签核之实」的历史教训;这是本项目 AC 的头号缺陷类,
已在 `input-system.md` 与 `docs/consistency-failures.md` 各留一次病历。

**How to apply**:给 AC 复审结论时,对**每一条**都报「载体 = ?」;发现判据输入依赖未定项时**主动降 ADVISORY
但说明发版前回升为 BLOCKING**(拆分不降级,承 D-B 裁定)。草稿若 15 条全 BLOCKING、零 ADVISORY,
先当红旗处理 —— BLOCKING 通胀会稀释门的意义。

**另一条高频病灶**:主语悬空/错格 —— 断言对象是**未成文系统的界面**(39/20/7b/48 均无 GDD)时,
AC 无从签核;须把主语改成「已有 `design/ux/[screen].md` 的界面」并把未成文者登记 OQ。

**第三条高频病灶(2026-09-18 系统 10 复审确认)**:两类「假可测」——
① **强度不足**:对枚举/三值结果断言「逐位相同」是平凡真,真正押注的派生整数值
(`eff_mag` / `drug_potency`)反而无断言;复审时对每个「逐位相同/一致」须问「对的是什么值?」。
② **跨文档单侧对拍**:AC 声称「对拍」另一系统的义务(如 10 对拍 3 侧的舍入模式),但本系统
代码路径根本不执行该义务 ⇒ 不可执行;正确写法是把单测放义务归属侧,本侧只断言收到的类型/值。
另:10/11 同款载荷须有构建期交叉校验 AC(11 有 AC-11-07,10 缺对应 = 登记缺失)。

**第四条病灶(2026-09-19 系统 5 复审确认)**,两条:
① **门级标注缺失** —— 表格只给证据类型 `[A]/[L]/[I]` 而不给 BLOCKING/ADVISORY 时,静默失败项
(如「检索 `Time.deltaTime` 须为零」)与边界单测项同权重,QA 无法排序;复审时须按
`coding-standards.md` 的 gate-level 表补标。
② **AC 表的 markdown 完整性也是可解析性判据** —— `**THEN`` 这类未闭合反引号会吞掉后续样式,
破坏任何按 `GIVEN/WHEN/THEN` 关键字解析 AC 的工具(`/story-readiness`、`/gate-check` 均可能);
逐条核 AC 时顺手 grep 未闭合的内联码。
另:「检索 X」式判据是**第三类假可测**——语义型 grep 不可机械执行(注释 / 测试代码 / JSON 键名皆
假阳性,「墙钟」「乘法路径」无固定字符串),正确载体 = asmdef 引用集白名单断言或递归反射扫描
(仿 `PresentationDtoGuard` / ADR-017 §二),复审时对每个「检索…」问「用什么工具跑?」。

相关:[[fix-all-review-findings]] · [[rules-first-numbers-later]] · [[user-owns-balance-values]]
