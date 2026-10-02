# Story 001: 存储抽象层（二进制 codec 接口 + 校验骨架）

> **Epic**: 7a 持久化服务
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

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

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `SaveHeader` — 存档头部结构（magic + checksum + version + worldSeed + configVersion + tick + snapshotOffset）
- `ISaveCodec` / `SaveCodec` — 按字段名/tag 编码接口与默认实现
- `ISaveService` / `SaveService` — 低层原语（SaveBytes/LoadBytes）+ SHA256 校验和 + 原子写 + 双档 bak
- `CodecWriter.WriteBytes` / `CodecReader.ReadBytes` — 新增原始字节读写方法
- 测试: 7 条单元测试（全部通过）
- 全量 EditMode: 1447/1474 Passed, 0 Failed

**Deviations**: 
- ADR-025 修订：`Sim.Codec` 引用集由「BCL only」改为「BCL + `Sim.Contracts`」
- `ISaveService` 接口只保留低层原语（SaveBytes/LoadBytes），高层方法（Checkpoint/SaveOnExit/Load）归后续 story

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/PersistenceService/save_service_test.cs` — 7 测全过

**Code Review**: unity-specialist 评审 3 BLOCKING 问题，全部修复：
- B-1: `Sim.Codec.asmdef` `noEngineReferences` 恢复为 `true`
- B-2: `SaveCodec.ReadEvent` 实现完整解码逻辑
- B-3: `SaveService` 移除 stub 方法，只保留低层原语

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
