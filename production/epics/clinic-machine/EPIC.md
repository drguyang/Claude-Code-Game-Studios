# Epic: 医馆即机器

> **Layer**: Core
> **GDD**: design/gdd/clinic-machine.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 边界交付(Sim.Contracts)
> **Status**: Ready
> **Stories**: 5 stories — see table below

## Overview

医馆即机器(24)把「医馆」从叙事空间升格为**模拟机器**:它只拥有三件事 —— **房间析出**(从系统 23 的只读 `structure_at(cell)` 把家具格析出为整数格房间)、**CONTEXT_TABLE**(环境上下文与速度乘子 `K_speed` 的上交,`K_CONTEXT_MAX = max(K_speed)` 交系统 1)、**乘子输出**(F-24-1 `EnvMod` 与 F-24-2 `EquipMod`,交付 21a 运行时输入与 9 的恢复率)。全案铁律:**24 无自有状态机** —— 房间/乘子是布局的纯函数,同一布局逐位重放相同(AC-24-04);**EnvMod 出 24 时永不 clamp**(唯一 clamp 在 21a F1,D-21-31 配对约束 AC-24-01/01b);房间连通 = 4 邻接整数格判据(ABS(dx)+ABS(dy)==1),禁物理禁 NavMesh(ADR-015);断开 ≥2 个家具簇 ⇒ 整盘 ROOM_NONE(EC-24-07)。烘焙表 `clinicmachine_room.json` / CONTEXT / ADJ 走 ADR-014 两阶段管线(**不归 ADR-022 关卡工具**),O-24-5 四项构建期断言(ADJ 对称性 / 溢出界 / W 域 / 子句类型闭集)为硬门。已冻结常量(2026-09-25):`TIER_MAX=3` · `EQUIP_MOD_CAP=1/4` · `ENV_MIN/MAX=∓1/2` · `K_speed={1, 7/8, 3/4}` · W 域 `[−1/4,+1/4]`;其余逐值调参归用户数值轮。P0 只做单房间评分,`ROOM_INTEROP`(F-24-4)推 P1a。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-015: 世界几何(手工烘焙固定世界 · 单一整数格) | 医馆房间/建造槽位/地形共用同一 `WorldPos` 整数格;房间连通 = 整数格邻接规则,非物理非 NavMesh | LOW |
| ADR-005: 确定性模拟与状态同步模型 | 24 全部数学在整数定点域(Q16.16);纯函数可由事件流重放 | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 唯一解析入口;乘子边界不中转 float;单一舍入 ROUND_HALF_AWAY_FROM_ZERO | MEDIUM |
| ADR-014: 数据管线与 JSON 解析器 | ROOM/CONTEXT/ADJ/FURN 表作者态 JSON → 构建期烘 `*.cooked`;Fix 字段 JSON 写字符串;构建期 schema 断言 | MEDIUM |
| ADR-025: 契约程序集清单与命名 | 24 的 sim 侧住 `Sim`(门 A,引用集恰 = {BCL, Sim.Contracts});`IClinicEnvQuery`/`ClinicEnvDto` 住 `Sim.Contracts` | LOW |
| ADR-016: AI 架构(分层确定性) | 24 无状态机 = 派生量纯函数(TR-clinic-002 的 Memoize 纪律与 16 的三源不变量同源) | MEDIUM |
| ADR-013: 拟物 UI 框架 | ClinicEnvDto 受 `PresentationDtoGuard` 递归扫描;AC-24-09(呈现侧)移交 42 | HIGH |

**Engine Risk**: **MEDIUM**(主体为纯整数算术与烘焙断言,LOW;抬到 MEDIUM 的是 ADR-013 DTO 守卫的实现细节与 ADR-005/012 的跨平台逐位实测前置 —— 后者归黄金夹具矩阵,不阻塞本 epic 的机制实现)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-clinic-*`,共 10 条)。
> 计数(2026-09-28 实测):**10 条 = 5 covered + 4 partial + 1 gap(TR-clinic-007 房间连通算法无 ADR,由本 epic story-002 兑现机制层)**;U 项不阻塞实现轮。
> 🔁 **2026-10-29 回填轮**:`TR-clinic-006` partial → covered(原 note 的依据 `OQ-24-5 未裁` 已失效,
> 该 OQ 于 2026-10-07 裁 `n_max = 64`)⇒ **当前 6 covered + 3 partial + 1 gap**。
> ⚠️ **同表另有 2 行的状态疑似陈旧但本轮未动**(不代改,须各自核):
> `TR-clinic-007`(`gap`,措辞说机制归 story-002,而 story-002 已 `Complete`)·
> `TR-clinic-009`(`gap`,交付边界归 story-004,而 story-004 已 `Complete`)。
> 本轮**没有**核它们的实际落点 ⇒ **不主张它们已 covered**。

| TR-ID | Requirement | Status |
|-------|-------------|--------|
| TR-clinic-001 | 医馆加成判定 = 纯整数 4 邻接(禁物理/禁连续坐标参与) | covered(ADR-015) |
| TR-clinic-002 | 24 无自有状态:加成输出 = 上游量的 Memoize 纯函数,失效键显式 | covered(ADR-016) |
| TR-clinic-003 | EnvMod(环境修正)在定点域内透传,边界不中转 float | partial(ADR-006+014) |
| TR-clinic-004 | CONTEXT_TABLE 经 ADR-014 烘焙管线出货;条目数硬上限 K_CONTEXT_MAX | covered(ADR-014) |
| TR-clinic-005 | ADJ_TABLE 对称性 = 加载期硬门(不对称即失败) | covered(ADR-014) |
| TR-clinic-006 | 加成叠乘的整数溢出断言(n_max 项上限内 Σ(weight×value) 不越界) | covered(ADR-006) —— 2026-10-29 由 partial 翻转 |
| TR-clinic-007 | 房间连通性析出算法与 ROOM_NONE 哨兵(围合判定) | gap —— 机制归 story-002 |
| TR-clinic-008 | 格分类三态:非医馆格 / 内部格 / 阈值格的归类判据 | partial(ADR-015) |
| TR-clinic-009 | 语义不泄漏:9 只收乘子,不收「医馆」语义概念 | gap —— 交付边界归 story-004 |
| TR-clinic-010 | 24 零呈现引用 · 零音频直接触发(输出仅整数乘子与格分类) | partial(ADR-025+018) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria AC-24-01…12 from `design/gdd/clinic-machine.md` are verified
- All Logic and Integration stories have passing test files(EditMode / PlayMode)
- AC-24-09([A] 呈现侧)由 42 的 epic 走查闭环,本 epic 只登记移交

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 烘焙表与 schema 构建门 | Config-Data | **Complete**(2026-10-29) | ADR-014/006 |
| 002 | 房间析出与连通规则 | Logic | **Complete** | ADR-015/005 |
| 003 | 乘子求值 F-24-1/2/3 | Logic | **Complete**(2026-10-21) | ADR-005/006 |
| 004 | 乘子交付边界与 ClinicEnvDto | Integration | **Complete**(2026-10-25) | ADR-025/013/006 |
| 005 | 纯函数重放与 Memoize 失效 | Integration | **Complete** | ADR-016/005 |
| 006 | 房间类型匹配(ROOM_TABLE 子句求值)+ AC-24-03 | Logic | **Complete**(2026-10-21) | ADR-015/005 |

> 🔁 **2026-10-29 收口轮订正(原文不删)**:story-001 转 `Complete`(AC 7/7 · 夹具类 42/42 ·
> 补证伪 8/8 转红)。
> ⚠️ **002–005 四行的 `Ready` 曾是陈旧值** —— 四份 story 文件的 `Status:` 早已是 `Complete`。
> **2026-10-29 同批一并订正**(用户指令「完成修复」):四行改为 `Complete`,理由由各自的
> `Status:` 行与其 Completion Notes 承载。复算:
> `for f in production/epics/clinic-machine/story-00*.md; do printf '%s ' "$(basename $f)"; grep -m1 '^> \*\*Status' "$f"; done`
> ⇒ **本 epic 6/6 全 `Complete`**。

> 🔁 **2026-10-21 新增 story-006** —— 立卡缘由:**`AC-24-03` 此前无主**(`grep -l AC-24-03 story-*.md` = 空;
> 001–005 五件全不认领),而「布局 → 房间类型」匹配全树零实现(`MatchRoom`/`RoomTypeOf`/`EvaluateClause` = 0)。
> story-003 的 6 条 AC **不含**匹配(其 `base_env` 由调用方喂)⇒ **S17-2 置 `In Review` 的理由把上游的无主义务
> 记到了 story-003 账上**。用户裁定:立本件认领 `AC-24-03`,story-003 转 `Complete`。
> 同批登记 **`OQ-24-7`**(多匹配裁决,暂定 = 声明序首个匹配)。
>
> ✅ **2026-10-21 实现轮(用户指令「story-006 实现」)**:story-006 转 `Complete`。
> 落点 `Sim/Clinic/ClinicRoomMatcher.cs`(新)+ `clinic_room_matcher_test.cs`(新,~~24 测~~ **32 测** —— 🔁 2026-10-21 QA 签核轮实测订正;24 为**建卡估计值被误记为实测值**);
> 证伪 7/7 恰如预期;回归 `4315/4279/0/36` 对基线 `4291/4255/0/36` ⇒ **+24/+24/0/0**,集合差「新增 24 · 消失 0」。
> 原件 `production/qa/evidence/impl-s17-6-room-matcher-2026-10-21.md`。
> ~~⚠️ **`OQ-24-7` 仍未裁** —— 哨兵 `MultiMatchPolicyIsProvisional => true` 钉住;裁定后须转 `false`
> + 改实现 + 哨兵测试**转红即提醒**,不得静默改绿。~~
> ✅ **2026-10-21 补齐轮:用户裁定「转正:声明序首个匹配」** —— 哨兵已转 `false`、哨兵测已翻转、
> 另配双向等价断言;`OQ-24-7` 结案。⚠️ 同轮订正:「声明序」在运行期 = **产物行序**
> (`ClinicMachineCookedWriter.cs:123` 按 `room_type` 序数排序 ⇒ `herb_room, surgery, ward`),
> 不是 JSON 手写序 —— 裁决内容不变,读者不得混读。
>
> ✅ **2026-10-21 补齐轮另两项**:① **全链真接线(含 23 侧)** —— `ClinicRoomMatchingService` +
> `CompositionRoot` 装配段 + `StructureInstanceRegistry.EnumerateInstances()` + e2e **11 测**;
> ② **Code Review 补做**(三路并行),原件 `review-s17-6-room-matcher-2026-10-21.md`。
> 证据 = `impl-s17-6-wiring-2026-10-21.md`。
> ⚠️ **仍未证**:`TD-building-01` 的 **`World` 半边**(e2e 用裸 `StructureInstanceRegistry`,
> `IsEffectivelyWalkable` 端到端未验)· `ModuleDefinition.Cost` + `DemolishChecker` 未做。
> ✅ **2026-10-21 补齐轮末:IL2CPP 腿已真跑通** —— Linux-x64-IL2CPP player 上 5/5 · 0 Inconclusive · 退出码 0
> (`unity/Logs/s17-6s-il2cpp-player.xml`)。~~cooked 产物不进 player ⇒ 第二段 Inconclusive~~ **该判断已被推翻**:
> cooked 产物**已经**随 `data-core` Addressables 组进 player,首版 3 条 Inconclusive 是**夹具设计错误**,已修。
> ⚠️ 仍只跑**一格**(ARM64 目标不存在 / Windows·Apple Silicon 归发版前)。
> ⚠️ 同轮暴露并修掉 4 个既有 PlayMode 文件的 `UnityEditor` 裸用(IL2CPP 腿自 2026-10-13 结构性坏),
> 已立 **`TD-testasm-01`**(仍缺机械门)。
