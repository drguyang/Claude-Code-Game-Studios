# 模块化建造(系统 23)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## Review — 2026-09-17 — Verdict: MAJOR REVISION NEEDED → **用户接受修订并标记 Approved(免二轮验证)**
Scope signal: L
Specialists: game-designer · systems-designer · ai-programmer · unity-specialist · qa-lead(×5 报告,均并行)· creative-director(opus 综合)
Blocking items: 8 组 BLOCKING(B1–B8)| Recommended: 见八组内部
Summary: 首轮 `/design-review`(`full`)裁 **MAJOR REVISION NEEDED**。creative-director(opus)终裁:23 GDD 对着「过于乐观的契约图景」成文 —— **「引用了 sim 域内不存在的真相」** 同一失败模式在初始占用 / 实体占格 / ADR-016 引用 / 玩家走感四处复现。**8 组 BLOCKING 当日全部修订落盘**:B1 假引用(ADR-016 §三 声称的 27 读合成与 `patient-ai.md:477` 均不存在 —— 实查 ADR-016 零处 `EffectiveWalkable`,缝一真实源 = 6 的 GDD `O-6-10`;降级 13/27 为单边 ⚠️ + ADR-016 §五 补注 + 13 GDD 回填)· B2 玩家读合成 = 假边(玩家走感 = `CharacterController` collide-and-slide,整数格不服务玩家 ⇒ F-23-1/规则三/AC-23-03 移除玩家,`EffectiveWalkable` 仅 27/13,ADR-022 新增 C2' 足迹同源)· B3 初始占用无真源(「世界流 = Overlay 唯一真源」与开档既有家具矛盾 ⇒ **用户裁定烘焙初始占用** `Overlay(0) := BakedInitial`,派生态 ADR-015 §二 第二类源,ADR-022 导出契约)· B4 实体占格判定不可建(**用户裁定玩家 + 敌人**;13 结构性不可知走避让;判定式 = 量化格 + 宽容半径,禁 Physics)· B5 F-23-2 缺区域检查(`∀cell ∈ OccupiedCells: cell ∈ BuildSlotRegion` + `(0,0) ∈ OccupiedCells_local` 目录校验)· B6 F-23-3 舍入/范围缺陷(Q16.16 形式 `⌊cost×R⌋ = (cost×rawR) >> 16`、`⌈1/R⌉ = (65536+rawR−1)/rawR`;`⌊⌋` 注 ADR-006 舍入例外;**用户裁定 P0 R=1 全额返还**,代价走拟物轴 BUILD_TIME/停诊)· B7 `StructureRemoved` 无法推导释放格集(登记**实例表**为第三份派生态,「实例表为准,载荷仅校验」)· B8 AC 载体集群(AC-23-04/06/09/13/14 换机制化载体:反射断言 / 可执行四向用例 / golden-vN 绑定 ADR-012 §三 / 写者守卫编译期不可达 + spy-sink / SlotType 标记)。**其余用户裁定**:换模块 P0 删除归 P1a(`modified_fields` 白名单仅朝向/变体)· 初始布局烘焙。
**四项用户设计裁定(2026-09-17,均照准并落盘)**:① 换模块归 P1a · ② 实体占格 = 玩家 + 敌人 · ③ P0 重排 R=1 全额返还(成本走拟物轴) · ④ 初始布局 = 烘焙初始占用。
**涟漪**:`adr-016`(§五 补注 EffectiveWalkable 口径)· `adr-022`(C2' 模板 collider 足迹同源 + BakedInitial 导出契约 + Validation 复选框)· `adr-010`(§三 义务 12 `structure_id` 高水位共用 `IIdAuthority`;折叠谓词不适用结构行)· `entities.yaml`(3 条公式条目,校验通过)。
**系统 23 结案** —— `systems-index.md` row 23 / §11 / 设计序三处已更新为 **✅ Approved**。
Prior verdict resolved: First review