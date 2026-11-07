# 实现证据 —— S26-1 `casebook-001` 分册态数据契约与存档段

**日期**: 2026-11-08
**Story**: `production/epics/casebook/story-001-volume-state-contract-and-save-segment.md`
**权威**: `docs/architecture/adr-010-persistence-save-format.md` §一 / §三 义务 13 ·
`design/gdd/casebook.md` 规则一 / 规则二 · **AC-39-13** · `F-39.1` / `F-39.2`
**存档版本**: `CurrentSaveVersion` **6 → 7**

---

## 0. 结论先行

| 项 | 结果 |
|---|---|
| 段编解码(契约 + 编解码器 + 迁移) | ✅ 21 条判据全绿 |
| 存档 v7 接线(写 / 读 / 迁移链 / 装配根) | ✅ 全量 EditMode 与 PlayMode 零回归 |
| 「已落笔判断」进存档 | ❌ **不做** —— 与 AC-39-13 / ADR-010 义务 13 冲突,用户 2026-11-08 裁定甲案 |
| `AC-5` 分册键走 `IIdAuthority` 真发号 | 🔴 **NOT-RUN** —— 39 今日无运行期分册态持有者 |
| 生产写侧 | ⚠️ 恒传 `null`(写空段),原因与影响见 §6 |

---

## 1. 取件期发现的两处硬问题(不是「补个字段」)

### 1.1 卡面引用的 `F-39.3` **在 GDD 里不存在**

```bash
grep -rn "F-39\.3" design/ docs/ production/ | grep -v session-logs
# ⇒ 唯一命中 = story-001 卡面第 13 行自己
```

`design/gdd/casebook.md` §Formulas 只有 **F-39.1**(列表排序)与 **F-39.2**(分册键 / 痕),
**没有 F-39.3**。本 story 的交付物恰恰是「分册态数据形状」—— 它要兑现的那条公式从未成文。

⇒ 字段清单的权威落回 **ADR-010 §三 义务 13**(「分册键 · 两个 tick · 纯呈现量(页码 / 折叠状态)」)
+ GDD 规则一(「三个时刻中的两个」归 39)。**不是自创 schema**,是把已有两条权威件落成类型。

### 1.2 卡面 AC-1 与 ADR-010 义务 13 **互相矛盾**(用户已裁)

| 出处 | 说法 |
|---|---|
| 卡面 AC-1 | 「内含两个 tick + **已落笔判断的呈现缓存(分册正文)**」 |
| ADR-010 §三 义务 13 | 「分册键 · 两个 tick · 纯呈现量(页码 / 折叠状态);**痕与排序不在其中**(读时计算)」 |
| GDD **AC-39-13**(BLOCKING) | 「痕 = 病例流读时投影,**不在任何分册态存储中**」+ 反射断言 |

把已落笔判断写进存档 = 病例流的**第二真源**,重放与补算会分叉,而 AC-39-13 的反射扫描会直接判红。
**用户 2026-11-08 裁定:甲案 —— 不落正文。** 本段的语义面因此只有四类:分册键 / 两个 tick / 页码 / 折叠状态。

---

## 2. 落了什么

| 文件 | 内容 |
|---|---|
| `Sim.Contracts/CasebookVolumeRecord.cs`(新) | `CasebookVolumeRecord`(册级:分册键 + 页码)· `CasebookPageRecord`(页级:分册键 + 病人 + 两个 tick + 折叠态)· `CasebookVolumeSnapshot`。全整数域,构造器拒负页码 / 非 0-1 折叠态 |
| `Sim.Codec/CasebookSegment.cs`(新) | 段编解码。形状 `volumeCount:i32 + 册级 8 B × n + pageCount:i32 + 页级 24 B × n`,自描述(无头部偏移字段)。解码侧三条硬判据:规范形唯一(严格升序 + 拒重复)/ 负 count 与巨 count 拒收(分配前按剩余字节复验)/ `folded ∉ {0,1}` 拒收 |
| `Sim.Codec/SaveMigrationV6ToV7.cs`(新) | 第五条真实迁移:在 **v6 头部末尾**插 8 字节空段,版本号写 7,尾部逐位不动 |
| `Sim.Codec/SaveHeader.cs` | `CurrentSaveVersion` 6 → 7 + 布局变更说明 |
| `Sim.Codec/SaveService.cs` | `ParseBody` 读段(紧跟头部)· `LastLoadedCasebookVolume` 出口(入口先置 `null`) |
| `Gameplay.Boot/SaveComposer.cs` | `Compose` 增 `casebookVolume` 参数(默认 `null` ⇒ 空段) |
| `Gameplay.Boot/CompositionRoot.cs` | 迁移链追加 `SaveMigrationV6ToV7`;`compose` 闭包处显式写 `casebookVolume: null` 并注明原因 |
| `Tests/EditMode/PersistenceService/casebook_segment_codec_test.cs`(新) | **21 条判据**(见 §3) |

### 段序(v7)

```
头部 | 分册态段 | 配置版本段 | 事件段 | 折叠段 | 载荷段
```

⚠️ **分册态段在配置版本段之前**,这是实测逼出来的,不是排版偏好 —— 见 §4。

---

## 3. 判据(21 条)

| 组 | 覆盖 |
|---|---|
| round-trip | 2 册 × 3 页逐位相等 · 编码→解码→编码 收敛(规范形实测面) |
| 空段 | 恰好 8 B · `null` 与 `Empty` 产出同一字节 |
| tick 边界 | `long.MinValue` / `MaxValue` / `0` / `-1` 四档往返无损 |
| 整数域 | 反射扫字段(**含泛型实参**)零 `float`/`double`;**负控**证明扫描器抓得住 |
| 规范形 | 输入乱序 ⇒ 输出同一字节流 · 重名分册键拒收 · 重复页拒收 |
| 解码拒收 | 负 count · 巨 count(分配前拦)· 非升序 · `folded = 2` · 截断段 |
| F9 | `default(CasebookPageRecord)` 绕过构造器守卫 —— **如实登记**,并钉住绕过后的值是安全态(摊开) |
| 迁移 | 插 8 B 在头部之后 · 版本号写 7 · 尾部逐位保留 · 错版本 / 错 magic 拒收 · **不依赖配置版本段** |
| AC-6 | 39 命名空间内 `Append` **IL 调用点** = 0(`ILBodyScanner`,非 grep) |

**一处自查记录**:`test_segmentTypes_scannerActuallyCatchesFloat` 首版把**宿主类型**传进扫描器
(而不是字段类型),负控因此空转;该负控在**自己的首跑里**抓住了这个错 —— 修好后才转绿。
这正是负控该干的事,记在此处以免被读成「一开始就写对了」。

---

## 4. 迁移链:v7 的插入点被实测**改判过一次**

**原稿**:分册态段插在**配置版本段之后**(当时的自然位置)。
**实测**:三条单项断言(版本号 / WorldSeed / 尾部字节数组)全过,但**整链**报
`迁移脚本 FromVersion=6 改变了事件正文` —— 而正文一个字节都没动。

**根因(几何)**:`SaveMigrationChain` 的「已发生事件原样保留」断言按**末尾等长**取窗
(`TailBody(current, bodyBefore.Length)`)。插在配置版本段之后时,末尾窗口
必然横跨插入点 ⇒ 比的是「cfg 尾巴 + 新插入 8 B + 事件」对「cfg + 事件」,**恒不等**,
**与文件大小无关**。

**处置**:把段挪到**头部之后、配置版本段之前**(与 `V2ToV3` 同一个位置)⇒ 插入点回到「头部区」,
窗口不重叠。既有四条迁移都插在头部区内,所以这条坑至今没暴露。

**未顺手修断言**:改成「按事件段对齐比较」需要在链里知道每版的段序与段长,而链的输入只有
字节 + 版本号;贸然改会让**四条既有迁移的判据同时失去保护**。⇒ 已登记为 **`TD-persist-24`**。

---

## 5. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7107 / 7070 / 0 / 0 / 37` | `2026-11-07T17:34:11Z` | sprint-26 S26-1 | `99232fe9` + 工作树 |
| PlayMode | `365 / 361 / 0 / 2 / 2` | `2026-11-07T17:35:39Z` | 同上 | 同上 |

**基线** `7086 / 7049 / 0 / 0 / 37`(sprint-25 收口后)⇒ **+21 / +21,零回归**。

**等式判据**:`7107 − 7086 = 21` = `casebook_segment_codec_test.cs` 的 `[Test]` 计数。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/PersistenceService/casebook_segment_codec_test.cs   # ⇒ 21
```

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ("unity/Logs/s26-1-full-em7.xml","unity/Logs/s26-1-pm.xml"):
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ("total","passed","failed","inconclusive","skipped")})
PY
```

**过程记录(不删)**:为让全量复绿共跑了 **7 轮** EditMode。前 6 轮的红**全部**是测试侧对
布局的假设(手拼存档 8 处 / 迁移链 5 处 / 降级夹具 3 处)没跟上新增段,以及我自己的两处笔误
(`MethodBase` 误传、迁移用例的尾部断言按偏移算)。**生产代码在 7 轮里只改过一次**
(段位置改判,§4)。

---

## 6. 仍未做(不得借绿)

| 项 | 状态 | 原因 |
|---|---|---|
| **`AC-5` 分册键走 `IIdAuthority` 真发号** | 🔴 **NOT-RUN** | 39 今日**无运行期分册态持有者** —— `CasebookScreen` 只渲染、不持状态(ADR-013 §9 C3)。「谁在什么时候交出 `CasebookVolumeSnapshot`」是 story-005 / 007 的交付面。生产 `compose` 闭包因此恒传 `null`(写空段) |
| **`AC-2` 的「读档后两个 tick 逐位还原」端到端** | ⚠️ **部分** | 段级往返已证(§3);经 `SaveService` 的**端到端**未单测,因为写侧恒 `null` ⇒ 端到端只能验「空段往返」,证不出「真分册态往返」 |
| **`AC-4` `Fix` 自定义编码器** | ✅ **零输入可达** | 本段**没有任何 `Fix` 字段**(全整数 tick / id / 页码),该 AC 今天没有宾语。**不声称已兑现**;若将来往段里加 `Fix` 字段,此条立即变为必须 |
| **`AC-7` 的「回退上一 checkpoint」** | ⚠️ **部分** | 段级损坏拒收已证(§3);「回退上一 checkpoint」是既有全档机制(`CheckpointFallback`),本轮**未**做段级端到端夹具 |
| 生产写侧接通 | 🔴 未做 | 同 `AC-5` |

---

## 7. 与相邻件的边界

| 件 | 管什么 |
|---|---|
| **本件(S26-1)** | 段**形状 + 编解码 + 迁移** —— 「分册态怎么进存档、怎么读回来」 |
| `casebook-003`(S26-2) | 分册**内容**的读时投影(`J(c,p)` / 痕)—— 本段**刻意不存**内容 |
| `casebook-005`(S26-4) | 页布局与焦点 —— **运行期**分册态持有者的落点 |
| `casebook-007` | 落笔写流 + 检查点锚点 —— 分册态何时被写 |
