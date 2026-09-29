# Story 001: 存储抽象层（二进制 codec 接口 + 校验骨架）

> **Epic**: 7a 持久化服务
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2h
> **Manifest Version**: 2026-09-29
> **Last Updated**: 2026-09-29

## Context

**GDD**: `design/gdd/persistence-service.md`
**Requirement**: AC-7a-01(快照=优化非真相) · AC-7a-02(世界流唯一裁判) · AC-7a-03(黄金字节对拍) · AC-7a-04(全二进制codec) · AC-7a-05(SHA256校验) · AC-7a-06(原子写+双档) · AC-7a-18(存档与事件流零float)

**ADR Governing Implementation**: ADR-010(main: save format + persistence contract) · ADR-005(次: deterministic sim) · ADR-006(次: fixed-point boundary)
**ADR Decision Summary**: 全二进制 codec（禁 JSON / PlayerPrefs）; SHA256 校验和 + 双档 bak; 原子写 + 后台线程写盘; Fix 经自定义编码器按字段名编码显式小端; 主线程只序列化不写盘。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-010 Engine Knowledge Risk MEDIUM —— 原子写 / 后台线程 / 哈希须实测; 刻意只用 BCL 稳定 API, 不用 UnityEngine.Hash128。

**Control Manifest Rules (this layer)**:
- Required: 全二进制 codec; Fix._raw long 显式小端写出; 三流每条事件按字段名/tag 编码（禁位置打包）; SHA256 覆盖全部字节; 双档 bak 机制; 主线程序列化 / 后台线程写盘
- Forbidden: JSON / PlayerPrefs / BinaryFormatter / JsonUtility / BitConverter / UnityEngine.Hash128 / File.Replace / 安装目录写盘
- Guardrail: 7a 是三流载体不是第二真源; 快照损坏不改变游戏事实; 后台线程禁调 Unity API

---

## Acceptance Criteria

*From GDD `design/gdd/persistence-service.md`, scoped to this story:*

- [ ] **AC-7a-01**: 快照损坏/缺失/跨版本不可读 ⇒ 三逻辑流完整重放，指定病人 outcome / 掉落清单 / id 高水位 == 直接重放世界流所得（BLOCKING）
- [ ] **AC-7a-02**: 快照内容与三逻辑流不一致 ⇒ 各游戏事实以世界流/事件流为准（BLOCKING）
- [ ] **AC-7a-03**: 同一具名存档夹具，编辑器(Mono)与 IL2CPP 玩家上各序列化一次 ⇒ 字节流逐位一致（BLOCKING · 执行点 = ADR-012）
- [ ] **AC-7a-04**: 一条三流事件写盘再读回 ⇒ 字段值逐位往返不变；三次序列化同一状态 ⇒ 三次字节流逐位一致；IL 扫描零 BinaryFormatter/JsonUtility/PlayerPrefs（BLOCKING）
- [ ] **AC-7a-05**: 翻转存档中任一非校验和字节 ⇒ 校验和验证失败（BLOCKING）
- [ ] **AC-7a-06**: 写 checkpoint ⇒ tmp → final 原子替换（final 存在则先 final → bak）（BLOCKING）
- [ ] **AC-7a-18**: grep 7a 侧全部源码 ⇒ 零 float/double 类型用于存档或事件流（BLOCKING）
