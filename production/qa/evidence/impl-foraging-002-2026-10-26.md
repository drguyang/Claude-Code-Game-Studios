# 实现证据 —— foraging-002 品级抽取管线 F-17-1 / 单次采量 F-17-2

> **日期**:2026-10-26
> **对象**:`production/epics/foraging/story-002-quality-roll-pipeline.md`
> **被实现**:`unity/Assets/Sim/Foraging/GatherQualityRoll.cs`(新)· `unity/Assets/Sim/EventRng.cs`(改)
> **被验证**:`unity/Assets/Tests/EditMode/Foraging/gather_quality_roll_test.cs`(新,22 条)
> **裁定**:**COMPLETE WITH NOTES** —— 4/5 AC 记绿;**AC-17-02 只验 Mono 半边**(跨平台面 BLOCKED-BY-ADR-012)

---

## 一、交付面

| # | 文件 | 性质 | 说明 |
|---|---|---|---|
| ① | `unity/Assets/Sim/Foraging/GatherQualityRoll.cs` | **新建** | F-17-1 四步(`DrawU` / `RawQuality` / `OutQuality`)+ F-17-2 两式(`GatherMul` / `HarvestQty`) |
| ② | `unity/Assets/Sim/EventRng.cs` | **改** | 新增 `CdfWalkHigh48`(GDD 定死的高 48 位形);既有 `CdfWalk`(取模形)加注「不得用于品级抽取」 |
| ③ | `unity/Assets/Tests/EditMode/Foraging/gather_quality_roll_test.cs` | **新建** | 22 条夹具 |

**门 A 合规**:两件均住 `Sim`(`noEngineReferences: true`,引用集恰 = `{BCL, Sim.Contracts}`)—— 纯 C# 整数算术,零引擎 API。

---

## 二、AC 逐条

| AC | 判据 | 结果 |
|---|---|---|
| **AC-17-03** | 单档 100% ⇒ raw 恒等该档;无除零/越界 | ✅ **绿**(5 条夹具) |
| **AC-17-04** | `raw > Cap` ⇒ `out == Cap`;`raw` 可从三元组重算 | ✅ **绿**(4 条夹具) |
| **AC-17-11** | `gather_seq` 零独立计数器 | ✅ **绿**(3 条夹具) |
| **AC-17-15** | `GatherMul` 形状四性质 | ✅ **绿**(6 条夹具) |
| **AC-17-02** | 跨平台逐位相同 | ⚠️ **只验 Mono 半边** —— 跨平台面 BLOCKED-BY-ADR-012 |

### 2.1 AC-17-02 的不记绿声明

跨平台逐位对拍归 ADR-012 的三格 CI 矩阵(Linux-x64-Mono / Linux-x64-IL2CPP / Linux-ARM64-IL2CPP)。
该矩阵**不存在**,且 F7(int64 溢出为 UB)spike **未跑** ⇒ 本件**不得**记绿跨平台半边。

已证的半边:同输入 100 次重跑逐位相同 + 求值次序无关(纯函数性)。
**测试方法名带 `_monoSideOnly` 后缀**,使「只证一半」在测试列表里可见,而不是藏在一个绿里。

> ⚠️ 这不是「留债」,是**如实挂起**:ADR-012 是独立的基础设施工作项,不属于本 story 的交付面。

### 2.2 真表侧不可判(第二条不记绿声明)

`quality_distribution` 在 21a 四行 `gather_profile` 里**全不存在**:

```
willow_bark   | keys: ['ecosystem', 'parts', 'qty_per_node', 'quality_character']
foxglove      | keys: ['ecosystem', 'parts', 'qty_per_node', 'quality_character']
cinchona_bark | keys: ['ecosystem', 'parts', 'qty_per_node', 'quality_character']
yarrow        | keys: ['ecosystem', 'parts', 'qty_per_node', 'quality_character']
```

S20-3 已把这个登记成 ADVISORY(`story-001:229-232`),归 21a 形状落地轮。

⇒ 本件**全部用合成分布驱动**(story 的 Implementation Notes 第 6 条已预见:「数值未裁不阻塞,
以合成夹具驱动」)。**本件的绿覆盖「算子与边界的形状」,不覆盖「真表能喂进来」**。

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import json
d=json.load(open('assets/data/item_database_items.json'))
missing=[it['base_id'] for it in d['items']
         if it.get('gather_profile') and 'quality_distribution' not in it['gather_profile']]
print('缺 quality_distribution 的品种:', missing)
"
# 预期:4 个品种全在列表里
```

---

## 三、两处技术判断(须记)

### 3.1 两种 CDFWalk 形:统计同构、逐点不同

`EventRng` 原有 `CdfWalk` 用取模形(`r = seed % Σw`),而 GDD `foraging.md:279-285` 定死的是
高 48 位形(`U = h >> 16`,取满足 `U × Σw < C_q × 2^48` 的最小 `q`)。

**实测量化**(2026-10-26,5 档等权,20 万采样):

| 形 | 各档占比 |
|---|---|
| A(取模形,现有) | 19.95 / 20.06 / 20.01 / 19.97 / 20.02 % |
| B(高 48 位形,GDD 定死) | 20.21 / 19.94 / 19.98 / 19.83 / 20.04 % |

**逐点一致率 = 20.1%**(恰为 5 档随机配对的期望值)。

⇒ **两形分布相同,但对同一 seed 落不同档**。若混用,同一 `(WorldSeed, node_id, gather_seq)`
重放会得到**不同品级**。

**处置**:新增 `CdfWalkHigh48`(形 B),给 `CdfWalk`(形 A)加注「不得用于品级抽取」。
`CdfWalk` 的既有调用方**仅测试**(实测 `grep -rn "CdfWalk" unity/Assets --include=*.cs | grep -v Tests/`
除 `EventRng.cs` 自身外零命中),故加注不影响生产代码。

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -rn "CdfWalk" unity/Assets --include=*.cs | grep -v "Tests/" | grep -v "EventRng.cs"
# 预期:零命中(仅测试在用)
```

### 3.2 `GatherMul` 是阶跃曲线,不是连续曲线(新登 `TD-foraging-02`)

GDD `:543` 给 `GatherMul(L) = 1 + L / 30`。**整数除法**下:

| 等级区间 | `GatherMul` |
|---|---|
| `L ∈ [0, 29]` | 1 |
| `L ∈ [30, 59]` | 2 |
| `L = 60` | 3 |

⇒ **只有三个阶跃值**,L=1 与 L=29 完全同权,L=30 突然翻倍。这与散文暗示的「随等级平滑增长」不符。

**本实现按字面式落地**(与 `TD-foraging-01` 的 `CapTable` 同一处置口径),并登债册
`TD-foraging-02`(open · P0 面 N)。

⚠️ **测试断的是「字面式落地的形状」,不是「曲线平滑」** ——
`test_ac1715_gatherMul_isStepwise_notContinuous` 逐点钉死 0/29/30/59/60 五个边界。
把这条读成「设计正确」就是给一个错误的设计背书。

---

## 四、证伪轮(S12-4 · 强制)

**命中形态**:阈值边界(`U×Σw < C_q×2^48` 的严格 `<` / `clamp` 上下界)· 定义域收窄(`Σw=0` 拒)·
一次性幂等(重算逐位同)。

### 4.1 首轮 4/7 —— 抓出两条真夹具缺口

| 变异 | 结果 | 根因 |
|---|---|---|
| **F1** `>> 16` → `>> 8` | **`red=6` 但期望靶测未红** | 靶测 `test_ac1703_cdfBoundaries` **直接调 `CdfWalkHigh48(lo,…)`,根本不经过 `DrawU`** —— 红的 6 条全是间接效应。**「U = 高 48 位」这条属性零夹具** |
| **F2** `<` → `<=` | **`red=0`** | 夹具用 `Σw ∈ {5,14}`,而 `2^48 mod 5 = 1`、`mod 14 = 4` ⇒ `U×Σw` **永不恰等于** `C_q×2^48`,等号分支不可达 |
| **F6** 停用 `Σw=0` 守卫 | 锚串不存在 | **脚本 bug** —— 靶子写成 `GatherQualityRoll.cs`,实际守卫在 `EventRng.cs:172` |

⇒ **前两条是夹具假绿(BLOCKING 级),不是代码缺陷。** 第三条是脚本问题,归因须分开。

### 4.2 补夹具

| 补件 | 钉住的属性 | 关键设计 |
|---|---|---|
| `test_ac1703_drawU_takesHigh48Bits_notLow` | `U = h >> 16` | **独立重写**一份 `SplitMix64.HashTagged` 做期望值,不复用生产实现 —— 否则断言自指(改哈希两边一起动) |
| `test_ac1703_strictLessThan_atExactBoundary_takesNextLevel` | 严格 `<` | 夹具 `[1,1]` + `U = 2^47`:此时 `U×Σw` **恰等于** `2^48`,严格 `<` 落 q=2,`<=` 落 q=1 |

⚠️ **两条补件都是先用离线 `python` 复算验证过才写进测试的** ——
因为这类断言写错会把**正确**的实现判成红的,第一反应会去改实现,那就把对的改错了。

### 4.3 第二轮 7/7

```
===== SUMMARY: 7 / 7 转红且命中期望靶测 =====
```

| 变异 | 属性 | 靶测 | 结果 |
|---|---|---|---|
| F1 | `U = h >> 16` → `>> 8` | `test_ac1703_drawU_takesHigh48Bits_notLow` | **`red=7`** ✅ |
| F2 | `<` → `<=` | `test_ac1703_strictLessThan_atExactBoundary_takesNextLevel` | **`red=1`** ✅ |
| F4 | `GatherMul` 分母 `30`→`40` | `test_ac1715_gatherMul_isStepwise_notContinuous` | **`red=3`** ✅ |
| F9 | 盐位 `"gather"`→`"gatherX"` | `test_ac1703_drawU_takesHigh48Bits_notLow` | **`red=1`** ✅ |
| F6 | 停用 `Σw = 0` 守卫 | `test_ac1703_zeroSumWeights_throws_notSilent` | **`red=1`** ✅ |
| F7 | `cap` 改成 `MAX_QUALITY` | `test_ac1704_rawAboveCap_clampsToCap` | **`red=2`** ✅ |
| F8 | 去掉 `+1`(`GatherMul(0)` 变 0) | `test_ac1715_gatherMul0_isOne` | **`red=5`** ✅ |

**还原判据 = SHA-256 逐字节**,脚本在每次变异后同命令内还原并比对,不符即 `exit 9` 中止整轮:

```
EventRng.cs            ac24eeb7ee91b5cf3681417897696cc55bbcee92eaa13b611788a78b2bbd9c2d
GatherQualityRoll.cs   8a92af9f8291745d3c218d36fbdba9cca98f9d3f76579ee8362fe1b245d99001
```

⚠️ **两个数字都须保留**:首轮 4/7、补夹具后 7/7。只记后者 = 把补完的结果当成一开始就有的结果。

---

## 五、测试证据(三元组 · 可复算)

| 项 | 值 |
|---|---|
| 截至时刻 | 2026-10-26 13:51(本机)· XML `end-time="2026-10-26 05:51:24Z"` |
| 批次 | `f2-full`(foraging-002 实现轮之后的**全量 EditMode**) |
| commit | `2b14787`(foraging-002 改动**未提交**,故 commit 为基线提交) |

| 级别 | total | passed | failed | skipped |
|---|---|---|---|---|
| **夹具级**(`f2-gather-roll`) | **22** | **22** | **0** | 0 |
| **全量**(`f2-full`) | **5196** | **5161** | **0** | 35 |
| 基线(S20-3 收口,`w-05c-full`) | 5174 | 5139 | 0 | 35 |
| **增量** | **+22** | **+22** | 0 | 0 |

⇒ **+22 恰等于新增夹具数**,零回归。

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 夹具级(total 须 22 · failed 须 0)
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/f2-gather-roll.xml | head -1
# 预期:total="22" passed="22" failed="0"

# ② 全量(须 5196/5161/0/35)
head -2 unity/Logs/f2-full.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
# 预期:total="5196" passed="5161" failed="0" inconclusive="0" skipped="35"

# ③ 失败用例数(须 0)
grep -c 'result="Failed"' unity/Logs/f2-full.xml
# 预期:0

# ④ 无编译错误
grep -c "error CS" unity/Logs/f2-full.log
# 预期:0

# ⑤ 证伪轮结果
tail -12 /home/gu/.claude/jobs/181888b1/tmp/falsify_f2_run2.log
# 预期:7 / 7 转红且命中期望靶测
```

---

## 六、未及项(如实登记)

| 项 | 状态 |
|---|---|
| **AC-17-02 跨平台半边** | **NOT-RUN** —— BLOCKED-BY-ADR-012(三格矩阵不存在 + F7 spike 未跑) |
| **真表侧 `quality_distribution`** | **不可判** —— 21a 四行无此字段,归 21a 形状落地轮 |
| **`GatherMul` 曲线的设计合理性** | **未评** —— 本件只按字面式落地并钉住实际形状;「阶跃是不是想要的设计」归数值轮(`TD-foraging-02`) |
| **`_monoSideOnly` 的 100 次重跑** | 在同进程内完成 —— 它证的是纯函数性,**不是**跨进程/跨平台一致性 |

---

## 七、结论

| 项 | 结果 |
|---|---|
| BLOCKING | **0** |
| AC 记绿 | **4/5**(AC-17-02 只验 Mono 半边,不记绿) |
| 夹具 | 22/22 绿 |
| Falsifiability Gate | ✅ **PASS** —— 7/7 转红(首轮 4/7,已如实记录) |
| 新登债 | `TD-foraging-02`(`GatherMul` 阶跃曲线) |
| **裁定** | **COMPLETE WITH NOTES** |
