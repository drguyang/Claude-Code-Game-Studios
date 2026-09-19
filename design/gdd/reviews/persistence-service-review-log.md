# Persistence Service Review Log

> 系统 7a 持久化服务 —— 首轮 `/design-review` 记录。

## Review — 2026-09-17 — Verdict: NEEDS REVISION(已修订,待用户裁定接受)
Scope signal: M(依赖 5+ 契约 ADR 已收敛 · 公式 2 · 跨系统断言重)
Specialists: systems-designer · qa-lead · game-designer · unity-specialist · creative-director(合成)
Blocking items: 5 簇 | Recommended: 若干 | 已全部修订落盘
Summary: 首轮 full 评审判定 NEEDS REVISION(非 MAJOR)。文本完整度与义务收敛(ADR-010 §三
单一出处)扎实,13 规则 / 18 AC 骨架成立,无结构性返工;阻塞集中在五簇 ——
① 存档粒度 vs「失败不可撤销」(支柱级,玩家裁定「两个都留」:事件触发为主 + 定时兜底);
② 高水位口径(`max(∅)` 未定义 / 自相矛盾 · `ItemInstanceId` 扫描域偷窄);
③ AC 判据不可执行(忘词令载体悬空 · IL 扫描边界未点名 · 负断言违纪律);
④ 校验和覆盖自相矛盾(checksum 为头部末字段 ⇒ 头部逃过校验);
⑤ 引擎侧实现期实测登记(原子性 spike · 断电承诺 · in-flight 竞态 · wantsToQuit)。
修订新增 **规则十四(7b 手动槽契约)+ AC-7a-19/20**;`OQ-7a-3/4` 玩家裁定闭合。
Prior verdict resolved: First review(零地)

## Review — 2026-09-17 — Verdict: APPROVED(修订后 · 用户裁定接受,免复审)
Scope signal: M
Specialists: 同上(首轮 5 簇全部修订落盘,无新增复审)
Blocking items: 0 | Recommended: 0
Summary: 用户裁定「接受修订,直接 Approved」。修订落盘内容:
规则六 双触发(事件为主 + 定时兜底)· 规则十四(手动槽契约:只进不退 / 读档即锁 /
叙事事件上下文)· F-7a-3 空集约定 + `ItemInstanceId` 扫描域回三流并集 · F-7a-4 豁免口径
对齐 9 的 R3.6 · 校验和段置头部之首 · 判据可判定化(接口签名反射 / IL 扫描点名 / 崩溃注入
harness 归 ADR-012 · 负断言改正面)· 引擎实测登记(File.Move 原子性 spike · 断电缺口 ·
wantsToQuit · in-flight 守卫)· AC-7a-01/02/05/06/07/08/09/13/15/16/17 实质重写 · 新增
AC-7a-19/20。`OQ-7a-3/4` 玩家裁定闭合。
Prior verdict resolved: Yes(首轮 5 簇阻塞全部修订落盘)
