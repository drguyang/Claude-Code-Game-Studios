# 评审报告 —— case-system story-006 重放持久化测试

**评审时点**:2026-10-08
**评审对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_replay_persistence_test.cs`(评审时 = 首版 8 测)
**Story**:`production/epics/case-system/story-006-replay-persistence-boundary.md`
**评审轮次**:单轮双代理(lead-programmer 代码面 + qa-lead 测试面)

---

## 一、原判定

**双代理一致判 CHANGES REQUIRED / NOT READY** —— 两份报告**独立收敛**于同一根因:
**「8 测全绿」不构成 AC 覆盖** —— 多条断言恒真 / 空集绿 / 测的是测试私有 helper 而非生产码。

### lead-programmer(代码面)—— 9 条 BLOCKING

| # | 类别 | 描述 |
|---|------|------|
| B1 | AC 覆盖错位 | round-trip 逐字段比对**漏掉 `Payload`**;全部载荷为 `default` ⇒ codec 丢弃载荷仍绿 |
| B2 | 被测对象是测试私有 helper | `EncodeCaseStream`/`DecodeCaseStream` 自造帧格式,非生产 `ISaveCodec` |
| B3 | 迁移/重放腿未覆盖 | 无 `ITickProvider` 桩、无状态重建(未调 `PatternDetector`/`CaseStreamQuery`) |
| B4 | 不折叠恒真断言 | 只比 round-trip 前后行数,任何忠实 codec 恒真;未制造终态折叠对照 |
| B5 | 谓词测错 + 场景取反 | 应验「有未结案病例 ⇒ `Folded(p)`=false」,却调 `HasOpenCase` 且选**已全结案**的病人 1 |
| B6 | 高水位三流零承重 | 全程内联 LINQ 自算,未调生产 `EventStream.GetNextPatientId()`;唯一实质断言 `> -1` 删任两流仍绿 |
| B7 | 53 边界恒绿 | 判据「字段名不含子串 "53"」与「是否引用 53」无关 |
| B8 | 空集绿(零迭代) | `for(i=1;...)` 在 1 条 PatternRecognized 上零迭代;`if(Count>0)` 把空集藏起来 |
| B9 | 非测 + 掩盖缺口 | 函数体仅 `Assert.Pass`;核验 D-37-B 在 entities.yaml/tr-registry **零命中** ⇒ 掩盖真实文档义务缺口 |

### qa-lead(测试面)—— 5 条 BLOCKING(独立复现)

| # | 类别 | 描述 |
|---|------|------|
| B1 | 空断言 vacuous pass | `test_case_d37b_registration_only` 只有 `Assert.Pass` |
| B2 | 同起点差分 | 高水位期望值来自测试自身 `Math.Max`,断言 `> -1` 恒真 |
| B3 | 空集绿风险 | `if(patternEvents.Count > 0)` 守卫使空集恒真 |
| B4 | 载体错位 | 字段名子串扫 ≠ 接口边界 |
| B5 | payload 未验证 | 载荷全 `default`,AC-37-06「病例状态集逐位同」未被有效覆盖 |

> **两份报告的 B1/B4/B5 与 B2/B6/B7 互为独立复现** —— 同一缺陷被两条独立路径指出,非单点误判。

---

## 二、修复落点

**策略:接生产码,不接则如实 NOT-RUN**(承 story-005 修复口径)。逐条:

| # | 修复 |
|---|------|
| B1/B5 | 载荷改为**真实 `PayloadCodec.Case.cs` 五支编码**入 `InMemoryBlobPool`;逐字段比对**增列 `Payload.BlobId/Offset/Length`**;补 `TryGetPayload` 断言载荷经池可还原 |
| B2 | **删除测试私有 helper**,改用生产 `SaveCodec`(:`ISaveCodec`)+ `CodecWriter/CodecReader` |
| B3 | 高水位改用生产 `EventStream.GetNextPatientId()`;**新增 `PatternDetector`/`CaseStreamQuery` 直调** |
| B4 | 不折叠改为**具体值断言**(病人 1 = 2 开案 + 2 结案,开/结分别计数,防「一对抵消」) |
| B5 | 谓词测**双向**:有未结案 ⇒ `true`;全结案 ⇒ `false`;结案后重开 ⇒ `true`(纠正场景取反) |
| B6 | 高水位**钉具体值**(三流并集 max=5 ⇒ next=6);新增「只含哨兵 ⇒ next=0」反向测 |
| B7 | 53 边界改**两条**:①载荷面无延迟/归因语义字段(token 白名单)②`Sim` 程序集引用集不含 53 侧(ADR-025 §① 结构性) |
| B8 | 乱序夹具改为**2 条 PatternRecognized** + `Assert.AreEqual(2, ...)` 防空集;并断言「到达序 ≠ 时间序」证非恒真 |
| B9 | 改为**读 GDD 断言 D-37-B 登记存在**(`case-system.md` 含 `D-37-B` + 标题 + 转登目标);**转登 9/7a 半边 NOT-RUN**(见 §四) |

**新增空集绿守卫**:所有字段扫描循环保留 `Assert.IsNotEmpty(fields)`。

---

## 三、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) CaseSystem 过滤(本 story + story-005 回归)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EditMode.CaseSystem" \
  --output unity/Logs/case_replay_v3.xml
# 2) 全量回归
unity test unity --mode EditMode --output unity/Logs/editmode_full_case006.xml
```

**结果**:
- CaseSystem:`total=22 passed=22 failed=0`(story-006 10 测 + story-005 12 测)
- 全量:`total=3001 passed=2954 failed=0 inconclusive=1 skipped=46`
  (inconclusive=1 = 既有 `SettingsExposureTest`;skipped=46 与基线同 ⇒ **无回归**)

### 可红性证明(突变验证 · 项目标准)

对生产码注入改坏点 ⇒ 须实测红 ⇒ 还原:

| 突变 | 落点 | 结果 |
|------|------|------|
| **A** `max + 1` → `max`(高水位 off-by-one) | `Sim/EventStream.cs:111` | ✅ **杀死**(2 测红:`threeStreamsUnion` + `sentinelOnly`) |
| **B** 删哨兵守卫 `!= PatientId.None` | `Sim/EventStream.cs:106` | ⚪ **等价突变**(未杀)—— 见 §四 发现 1 |
| **D** 初值 `-1` → `0`(哨兵不再被初值挡住) | `Sim/EventStream.cs:103` | ✅ **杀死**(`sentinelOnly_returnsZero` 红) |
| **E** header 不写真实 `PayloadRef`(写 `default`) | `Sim.Codec/SimEventCodec.cs:40` | ✅ **杀死**(`roundTripBytesEqual` 红)—— **直接反驳旧版 B1/B5** |

**还原确认**:全仓 `grep MUTANT` 零命中;`git diff --stat unity/Assets/Sim/ unity/Assets/Sim.Codec/` **无输出**(生产码与评审前逐字节同)。

---

## 四、未闭登记(禁借绿)

- **AC-37-06 的 IL2CPP 半边**:归 ADR-012 三格矩阵批 ⇒ `BLOCKED-BY-ADR-012`(story 已声明)
- **7a `Folded(p)` 折叠执行半边**:生产谓词**不存在**(全仓 grep 零命中;7a GDD `persistence-service.md:316` 有定义但无代码)⇒ 本测只验 37 侧判据接口 `CaseStreamQuery.HasOpenCase`,折叠执行半边 **NOT-RUN**
- **D-37-B 转登 9/7a 半边**:`design/gdd/disease-simulation.md` + `persistence-service.md` 对「行为学前提 / 勤快度 / 同源检测引入结案」**零命中** ⇒ 转登**未落盘** ⇒ 该半边 **NOT-RUN**(37 GDD 侧登记已验存在)
- **53 消费半边**:`PatternRecognized` 延迟/不可归因的**消费方**测试载体在 53 ⇒ 挂 `AC-53-04`

### 本轮新发现(登记,不静默)

1. **哨兵守卫 `!= PatientId.None` 是冗余防御(等价突变)** —— `max` 初值 `-1` 与哨兵值 `-1` 重合 ⇒ 哨兵天然被 `> max` 挡住。该守卫非承重,但**保留**(防未来初值改动);其承重性由突变 D 证明(改初值即红)。
2. **三流段 codec 不存在** —— ADR-010 §一 存档布局 = 头部 + 三流 + 快照段,但「流段」分帧 codec 全仓无实现 ⇒ 本测的「计数前缀 + 逐事件 `SaveCodec.WriteEvent`」是**story 侧帧**,非生产流段格式。生产流段 codec 落地前,round-trip 的「整档字节级相等」半边维持**部分覆盖**。

---

## 五、跨域上报

- **7a 折叠谓词 `Folded(p)` 生产实现缺失** —— 37 的「不折叠」不变量**无法端到端验证**,只能验判据接口。建议 7a 落地谓词后由本测增补端到端断言。
- **D-37-B 转登未落盘** —— 37 GDD 已登记「须在 9 或 7a 登记」,但两 GDD 零命中。**producer 传播**;本 story 的 D-37-B 条目**结构性不可签核**(只能 NOT-RUN)。
- **三流段 codec 缺失** —— ADR-010 §一 布局已定,流段分帧实现归 7a。建议 7a story 覆盖。
