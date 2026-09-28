# Story 005: 发现体验走查与两层世界一致性([L] / EXTERNAL)

> **Epic**: 世界与生态区
> **Status**: Ready
> **Layer**: Feature
> **Type**: Visual-Feel
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(§D 组 AC-6-24 发现体验 [L] 走查 · AC-6-25/26 EXTERNAL(挂关卡工具 CI 与数值轮)· §Rules 架空 4 生态区的支柱五叙事约束 · P0 = 一个医馆 + 一个小场景的范围声明)
**Requirement**: TR-worldeco-001(定义派生态 → 视觉层与逻辑层「同一世界」的一致性由工具 C1 告警/加载期校验兜底)· TR-worldeco-008/010(体验侧验证:发现与激活对玩家可感、可读)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-022(主): 关卡工具(C1 两层漂移 = 告警,C2 可走性同源 = 硬失败);ADR-015: 两层世界
**ADR Decision Summary**: 确定性整数逻辑层 + 纯视觉层(Terrain,float)—— 运行期只加载逻辑层,视觉层采样禁入 sim;两层同源于关卡工具的一次导出,C1 检查在工具 CI 中存在(告警级),漂移只影响表现不退化为构建失败。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(走查本体)/ MEDIUM(工具 CI 面,ADR-022 Engine Risk)
**Engine Notes**: 走查 = 图形界面人工执行(承 CLAUDE.md「只有必须图形界面才请用户操作」);工具 CI 为纯 .NET 编辑期,不进构建。

**Control Manifest Rules (this layer)**:
- Required: 走查样本 = P0 实装范围(医馆 + 一个小场景 + 4 生态区边缘各一处);发现反馈经既有 42 通道(无新 UI 栈);逻辑↔视觉抽样对照表(每生态区 ≥ 3 个采样格)
- Forbidden: 为走查临时在场景预摆 gameplay 对象(违 ADR-023 ②);把视觉层高度图采样回逻辑判定(违 ADR-015 两层纪律)
- Guardrail: 支柱五 —— 4 生态区为架空地貌,走查检查叙事一致性(名称/地貌/气候档与 `world-eco` 设定不矛盾),不做史实核查

---

## Acceptance Criteria

*From GDD `design/gdd/world-and-ecozones.md`, scoped to this story:*

- [ ] **[L]** 发现体验走查(AC-6-24 `[A][L]`):玩家走近 POI 时「被发现」有可感反馈(经 42 的拟物通道,非弹字),且跳过 Discovered 直达 Resolved 的 POI 不产生半状态表现;走查记录 SIGN-OFF
- [ ] 两层一致性抽样:每生态区 ≥ 3 采样格,肉眼核对「逻辑层可走性标注 ↔ 视觉层地形可读性」不矛盾(承 ADR-022 C1 告警的人工确认面,AC-6-25 EXTERNAL 的走查半边)
- [ ] 工具 CI 产物核对:C1–C6 检查在关卡工具流水线中存在且报告可读(C2/C4/C5/C6 硬失败项以注入缺陷的负面构建验证,AC-6-26 EXTERNAL)
- [ ] P0 范围守门:实装场景 = 医馆 + 一个小场景;走查报告点名任何越范围的开放世界内容(范围债显式记账,不新增)
- [ ] 帧率无关的激活体验:快速转身/Zoom 移动(跨格率上界)下无「世界闪烁空块」超过 1 tick 可见窗(手感项,目测 SIGN-OFF;数值化判据归实现期)

---

## Implementation Notes

1. 走查脚本化优先(承「Scripted Spikes」记忆):激活切换与发现事件可由调试菜单一键回放格序列驱动,只留「可感/可读」判断给人。
2. 采样格清单 = 常量夹具(生态区 × 3),存 `production/qa/evidence/world-ecozones/` 走查模板。
3. C1–C6 负面验证在工具 CI(注入自交多边形 / 断链导航格),6 的 epic 只核报告存在。
4. 数值轮(EXTERNAL AC-6-26)未解冻项:LATTICE_SIZE / K_speed 的**体验合理性**不在本 story 判定,只验机制与一致性。

---

## Out of Scope

- [Story 001–004]: 全部 sim 机制(本 story 零 sim 改动)
- 关卡工具的实现本体(ADR-022 Tooling epic)
- 开放世界全图(明令 P1a)
- 42 的发现反馈元件实现(归 skeuomorphic-ui epic;此处只联调走查)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 调试格序列播放器:接近一个 POI | 执行 | 反馈出现且不依赖颜色单通道(承无障碍件) |
| TC-2 | 跳级转移的 POI | 观察 | 无 Discovered 半态表现 |
| TC-3 | 注入自交多边形的工具构建 | CI | 硬失败(C4),6 运行期不受影响(数据未出货) |
| TC-4 | 高速跨格(事件率 = tick 上限) | 观察 chunk 流式 | 无超 1 tick 空块闪烁 |

**Edge cases**: 读档落点恰在 chunk 边界(格锚点 + 性格内偏移后激活是否即刻正确);4 生态区交界处发现门的区归属显示。

---

## Test Evidence

**Story Type**: Visual-Feel
**Required evidence**: `production/qa/evidence/world-ecozones/story-005-discovery-walkthrough-[date].md`(走查记录 + 截图 + 签核)+ 工具 CI 报告链接
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 003 / 004(发现与激活机制稳定)· ADR-022 关卡工具 CI(外部产物)· 42 的发现反馈通道(外部 epic)
**Unlocks**: 本 epic Definition of Done([L] SIGN-OFF 入账,EXTERNAL 项以 CI 产物或数值轮解冻为凭)

---

## Completion Notes
