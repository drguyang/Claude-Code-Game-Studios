# Story 003: POI 一分为二 —— 定义加载、三态状态机与 PoiStateChanged 唯一写通道

> **Epic**: 世界与生态区
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(§Detailed Rules R-6-7 三态单调不可逆 `enum PoiState{Undiscovered=0,Discovered=1,Resolved=2}`(可跳过 Discovered)· R-6-9 唯一写 = `PoiStateChanged` 主机 Append · R-6-10 `spawn_anchor` 读定义不读状态 · §Formulas F-6-4 转移总数 `≤ 2×|POI_DEF|` · B 组 AC-6-10…18)
**Requirement**: TR-worldeco-001(定义 = 派生态,不进流)· TR-worldeco-002(状态 = 模拟态,所有者与唯一写者 = 6,主机唯一)· TR-worldeco-003(载荷 `{poi_id,new_state}` 均整数枚举)· TR-worldeco-004(`Patient=PatientId.None` 不污染高水位)· TR-worldeco-005(无第二存储)· TR-worldeco-006(重放从流重建,快照非真源)· TR-worldeco-007(转移有界)· TR-worldeco-008(52 读定义不读状态)· TR-worldeco-009(枚举值与合法转移归 6)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-021(主): POI 状态所有权与世界流承载;ADR-009 / ADR-005
**ADR Decision Summary**: ADR-021 是首条以「系统 ADR 追加世界流 Kind」方式扩骨架的实例;`PoiState` 具体值归 6 的 GDD(骨架先行纪律);状态写经 `IEventSink.Append`,主机唯一执行;事件用 `PatientId.None=-1` 哨兵;世界流不折叠 + 有界性论证。`PoiStateChanged` 已具名入 `entities.yaml`(stream/author/payload_schema 必填,ADR-024 真源)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(本体)/ MEDIUM(流侧重放与 7a 接缝)
**Engine Notes**: 纯数据边界与所有权裁决,零 post-cutoff API;重放测试用 sim 侧假时钟(事件流驱动,非墙钟)。

**Control Manifest Rules (this layer)**:
- Required: 转移单调不可逆 `Undiscovered → Discovered → Resolved`,**允许跳级**(Undiscovered→Resolved 合法);逆转移**结构上不存在**(无 API,非仅「不推荐」);每次转移恰一条 `PoiStateChanged`
- Forbidden: 第二存储 —— POI 状态不落存档独立字段、不落场景 GameObject 标志位、不落静态内存字典(grep 无旁路);敌人行套用病史流折叠(ADR-006 Amendment B 注记,世界流不折叠);27 读 POI **状态**(OQ-6-1 已把 27 移出消费白名单 {4,25,37})
- Guardrail: 事件率上界 —— 转移总数 `≤ 2×|POI_DEF|`(每个 POI 至多 2 次写入),集成断言在真实会话统计条目数

---

## Acceptance Criteria

*From GDD `design/gdd/world-and-ecozones.md`, scoped to this story:*

- [ ] `PoiState` 恰三值整数枚举;对任一 POI,状态历史单调不减,跳级可达,任何逆转移尝试无对应代码路径(枚举 API 不暴露 setter,只暴露 `TryAdvance(poi_id, to)` 且校验转移合法,AC-6-10…12)
- [ ] `PoiStateChanged` 载荷 `{poi_id:int, new_state:int}` 类型反射断言 ∈ 整数域;`Patient = PatientId.None`;`Seq` 由发号器给出(承 ADR-006 Amendment 后的 SimEvent 形状)
- [ ] Append 权 = 主机唯一:客户端调用写通道 ⇒ 断言失败/拒写(AC-6-26a,`[B]`)
- [ ] 同一 tick 同 POI 至多一条状态事件(全序键 `(Tick, StreamPriority, Patient, Seq)` 下无重号、无乱序可见,AC-6-13)
- [ ] 重放/读档:从世界流条目序列重建 POI 状态 == 运行期内存态;删掉快照只留流仍重建正确(快照 = 优化非真源,AC-6-14/15)
- [ ] 无第二存储扫描:静态检查(反射 + 存档字段清单)证明 POI 状态无旁路持久化(AC-6-16)
- [ ] 有界性:构造 N 个 POI 全量转移的会话,世界流条目数 `≤ 2N`;52 的 `spawn_anchor` 抽池在「状态任意变化」后结果不变(只读定义,AC-6-17/18)
- [ ] `entities.yaml` 中 `PoiStateChanged` 的 `stream/author/payload_schema` 与 kindgen 生成的路由一致(A1–A5 断言绿,ADR-024)

---

## Implementation Notes

1. 定义侧:`poi_defs.cooked`(整数 id + 格锚点 + 类型 + 守卫配置,派生态加载重建)与状态侧(`poi_state` map,由事件流折叠出的**当前视图**,非存档对象)严格分离 —— 定义住在只读加载器,状态住在主机 sim。
2. 写通道 = `IEventSink.Append(SimEvent{Kind:PoiStateChanged,...})` 的纯路由(Kind→StreamId 白名单由 kindgen 生成);6 侧唯一入口函数点名「主机」。
3. 「可跳过」的实现 = `TryAdvance` 允许 `to > cur`;禁 `to == cur`(幂等重复写)与 `to < cur`(逆转移)。
4. 客户端读视图:订阅世界流(承 ADR-001 pipe),不自算状态。
5. 哨兵:`PatientId.None = -1` 不参与 `max(patient_id)` 高水位扫描(ADR-010 §七 口径,回归测试覆盖)。

---

## Out of Scope

- [Story 004]: 触发发现门(何时写 Discovered —— 判距与意图归各系统,接缝在 004 联调)
- 发现门 UI 呈现(归 42 epic)· 病例/脉案对 POI 的消费(归 37/4)
- 世界流的序列化编码实现(归 7a persist epic;本 story 用其接口)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | POI p 处于 Undiscovered | 写 Discovered / Resolved / 跳级 Resolved | 前两种各生成恰 1 条事件;跳级成功(无 Discovered 历史仍合法) |
| TC-2 | p 处于 Discovered | 尝试写 Undiscovered | 无 API 路径可调用(编译期失败)+ 反射守卫测试(负面用例) |
| TC-3 | 同 tick 对 p 写两次 Discovered | Append | 第二次被拒(幂等/无变化),事件计数不变 |
| TC-4 | 客户端角色(非主机) | 调写通道 | 拒写,断言点名「主机唯一」 |
| TC-5 | 一段含全部转移与会话外无事件 | 关档 → 从流重建 | 状态逐 POI 相同;比对快照重建路径结果一致 |
| TC-6 | 世界流事件计数 vs POI 数 N | 全量转移会话 | ≤ 2N;52 抽池在事件流前后不变 |
| TC-7 | `PatientId.None` 条目 | 跑 `max(id)+1` 高水位扫描 | 排除哨兵(承 ADR-021 裁定④) |

**Edge cases**: 读档时世界流条目缺失(损坏)⇒ ADR-010 §四 自动回退链;跳级后 `Discovered` 永不可达(单调,合法终局);同 tick 多 POI 并发写(Seq 由发号器保证全序,无竞争)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/world-ecozones/poi_state_machine_test.cs`(或 `unity/Assets/Tests/EditMode/WorldEcozones/`,流侧重放如走 PlayMode 则注明)— must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(poi 定义加载)· ADR-024 kindgen(已生成路由)· 7a 事件流读写接口(接口存在即可)
**Unlocks**: Story 004(发现门联调)· 52 的 `spawn_anchor` 与遭遇挂载验收 · 37 病例对 POI 状态的只读消费

---

## Completion Notes
