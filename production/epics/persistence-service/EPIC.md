# Epic: 7a 持久化服务

> **Layer**: Foundation
> **GDD**: design/gdd/persistence-service.md
> **Architecture Module**: L3 契约程序集(`Sim.Codec`: `ISaveCodec` / `ISaveService` / `SaveHeader`)
> **Status**: **In Progress**(1/2 stories —— 002 为 ADR-029 实现轮新增,2026-10-02)
> **Stories**: 2 stories

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 存储抽象层（二进制 codec 接口 + 校验骨架） | Logic | **Complete ✅ 2026-09-30** | ADR-010 |
| 002 | 载荷编码契约 —— `IPayloadEncoder` + `IBlobSink` + `EncodeBoxed` 分派 | Logic | Ready | ADR-029 |

## Overview

7a 持久化服务是全案唯一「不产生任何游戏事实」的系统。它把三条逻辑流（病史/病例/世界）连同存档头变成字节写到盘上，再读回来还原成模拟态。

**核心交付**：
- `SaveHeader` — 存档头部结构
- `ISaveCodec` / `SaveCodec` — 按字段名/tag 编码
- `ISaveService` / `SaveService` — 低层原语（SaveBytes/LoadBytes）+ SHA256 校验和 + 原子写 + 双档 bak

**测试**：7/7 Passed · 全量 1447/1474 Passed 0 Failed

## Next Step

**Epic 全部 1 个 story 已完成**。下一步 = 推进其他 epic 或处理跨系统待办。
