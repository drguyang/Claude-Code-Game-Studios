# Story 007: 数据边界收尾(设置壳不缓存 · 元件库唯一出口 · 墨龄数据路径)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-010` (墨龄 tick 纯函数: 墨龄 = 当前tick − 落笔tick,禁止墙钟)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-005: 确定性模拟 (secondary)
**ADR Decision Summary**: 42 只渲染不持状态(§9 C3);设置界面壳不缓存他系统状态;元件库唯一出口;墨龄 = ITickProvider 纯函数(禁止墙钟);变体来源闭合 + 会话内稳定

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-005 Knowledge Risk MEDIUM —— ITickProvider 纯函数接口;但本件刻意不用 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 设置壳不缓存他系统状态(音量 / mono 等);元件库唯一出口;墨龄 = ITickProvider 纯函数(tick 差)
- Forbidden: 42 持有设置值;42 直连 9 的模拟;内联变体;freehand(零 freehand · 变体来源闭合 · 会话内稳定)
- Guardrail: 墨龄计算禁墙钟(只经 ITickProvider)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-G1**: 元件库唯一出口:组件树内不存在内联变体;变体必须经元件库注册表
- [ ] **AC-42-G2**: 设置壳不缓存他系统状态(音量 / mono / 任何游戏量);设置壳只持有 UI 呈现态(开关 / 滑块位置)
- [ ] **AC-42-G6**: 墨龄数据路径:零 freehand;变体来源闭合;会话内稳定;墨龄 = 纯函数 tick 差(当前 tick − 落笔 tick,只经 ITickProvider,禁止墙钟)

---

## Implementation Notes

*Derived from ADR-013 / ADR-005 Implementation Guidelines:*

1. **设置壳状态**: 只持有 UI 呈现态(开关 / 滑块位置);不持有他系统状态(音量 / mono 等)
2. **元件库唯一出口**: 组件树内不存在内联变体;变体必须经元件库注册表
3. **墨龄计算**: 墨龄 = 当前 tick − 落笔 tick;只经 ITickProvider(禁止墙钟 / Time.time)
4. **变体来源闭合**: 变体来源 = 元件库注册表;会话内稳定
5. **零 freehand**: 墨迹系统零 freehand(自由绘制);所有墨迹来自变体库
6. **符号级禁令**: AC-42-G1 lint 检查内联变体;AC-42-G6 断言墨龄计算只经 ITickProvider

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 数据边界守卫(DTO Guard · 符号禁令)
- Story 001: 拟物元件库基础(本 story 依赖其元件库注册表)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-G1**:
- Given: 组件树
- When: lint 扫描
- Then: 不存在内联变体;变体必须经元件库注册表
- Edge cases: 合法变体(经元件库注册的变体)

**AC-42-G2**:
- Given: 设置壳类型树
- When: 检查字段 / 属性
- Then: 不持有他系统状态(音量 / mono / 任何游戏量);只持有 UI 呈现态(开关 / 滑块位置)
- Edge cases: 设置壳缓存 AudioMixer 引用(引用本身合法,但不持有音量值)

**AC-42-G6**:
- Given: 墨龄计算
- When: 检查数据路径
- Then: 墨龄 = 当前 tick − 落笔 tick;只经 ITickProvider(禁止墙钟)
- Edge cases: Session 内墨龄稳定;跨 Session 墨龄重置

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skeuomorphic-ui/data_boundary_final_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (元件库基础必须就绪), Story 004 (数据边界守卫必须就绪)
- Unlocks: Story 008 (无血条替代反馈)
