# ADR-037 — 存档 v3:配置版本段(逐份 artifact 的 `ConfigVersion`)

| Field | Value |
|-------|-------|
| **Status** | Accepted(2026-10-31 · §Decision ③–⑦ 由本轮就地裁定,未经用户逐条确认 —— 同 ADR-034 口径,实现轮之前可改) |
| **Date** | 2026-10-31 |
| **Supersedes** | 无(只**追加**一段,不改既有段语义) |
| **Related** | ADR-010 §一/§七 · ADR-014 §五 · ADR-034(载荷段,布局先例)· ADR-012 §三(黄金夹具版本化) |

## Context

`AC-7a-14` 末句要求「`ConfigVersion` 不匹配 → 已发生事件不受影响、**仅记录并供排查**」。
2026-10-31 该句首次有了执行体(`LoadSequencer.RecordConfigVersionOnLoad` + `ConfigVersionDriftRecord`),
但**只能记录「不匹配」这件事本身**,记不出**哪一张表变了**:

- 存档头只有一个 `ConfigVersion`(u32),而它是 22 份 data-core 产物逐份版本的
  **组合值**(`DataCoreConfigVersion.Combine`,FNV-1a over `(address, version)` 按 address 序);
  组合是单向的 —— 从 u32 反推不出成员。
- ⇒ 排查时能得到的全部信息是「两串十六进制不同」。四路专家会审(2026-10-31)把
  「开发者侧逐表差异清单」列为候选方案 **D**,并指出:**这是唯一的零成本窗口** ——
  今日零真实存档(`ADR-030 R3` 的同族事实),一旦首次对外发档,布局再变就要写迁移且
  `ConfigVersion` 的排查价值永久残缺。

**为什么不能靠「不匹配就整体重算」**:ADR-010 §七 明文「后续窗口用新配置」,**已发生事件
不受影响**;重算 = 改写已发生的事实。ADR 的这一条不是待办,是裁决。

## Decision

### ① 布局 v3 = `头部 | 配置版本段 | 事件段 | 折叠段 | 载荷段`

配置版本段紧贴头部、位于**所有既有段之前**。两条理由:

- **可迁移**:迁移链的第三不变量按「从末尾算的 N 字节」比对正文
  (`SaveMigrationChain.TailBody`)。**尾部追加**会改变尾部字节 ⇒ 那条断言必红 ⇒
  v2→v3 迁移**结构上不可能**(这正是 ADR-034 §Decision ⑥ ⓑ 拒绝尾部追加的同一条理由)。
  插在头部之后,**尾部一个字节不动** ⇒ 断言③ 通过,v2→v3 成为本仓**第一条真实迁移**。
- **可定位**:起点 = `SaveHeaderCodec.FixedSize`(定长),迁移脚本无须解析事件段即可插入。

### ② 段形状 = `count:i32 + count × { addrLen:u16, addr:utf8[addrLen], version:u32 }`

- **写地址字符串而不写索引**:`DataCorePreloader.DataCoreAddresses` 的数组序是**装载序**,
  会随接线轮次变化(该类注释记了 7→8→…→22 的历次追加)⇒ 索引**不稳定**,用索引会让
  旧档的归因指错表 —— 比不给归因更坏。
- **编码序 = address 的 `StringComparer.Ordinal` 升序**;解码**拒收**非升序 / 重复地址 / 负 count。
  确定性要求同 ADR-034 §②(两台机器写出的同态存档必须逐字节相同)。**单一规范形**:
  同一集合只有一种字节表示。
- 段为空(`count = 0`)= **「本档不含逐份版本」**,不是「无差异」。二者的区分由
  `ConfigVersionDiff.HasPerArtifactData` 承载(见 ④)。

### ③ `SaveVersion` 2 → 3;`PayloadSegmentMinVersion` **保持 2**

`< 2` 的档依旧在版本门被拒(v1 无载荷字节,信息不存在)。`= 2` 的档**下沉进迁移链**,
由 ⑤ 的脚本升到 3。这是 `PayloadSegmentMinVersion` 自 ADR-034 引入以来**第一次**让
`[PayloadSegmentMinVersion, CurrentSaveVersion)` **非空** ⇒ 迁移分支第一次有**走完整 `Load`**
的真实输入。
⚠️ **这不「关闭」`TD-persist-10`** —— 该债已于 2026-10-31 由 `S21-7` 以「把接缝抽成
`ApplyMigrationsIfNeeded` 直测」的方式**关闭**。本件与它是**互补**的两层:直测覆盖「链被调用 /
返回值被用」,本件覆盖「一份真实的 v2 档能被 `Load` 端到端读完」。

### ④ 「未知」与「无差异」必须可分

- 有逐份段且逐份相同 ⇒ 差异集 = **空**,结论 = 「配置完全一致」;
- 有逐份段且部分不同 ⇒ 差异集 = **那些 address**,结论 = 「这几张表变了」;
- **无逐份段**(v2 迁上来的旧档)⇒ `HasPerArtifactData = false`,结论 = **「本档早于 v3,无法归因」**。

⚠️ 第 3 种不得降级成第 1 种 —— 那会把「不知道」写成「没事」。这与我方在 `F9`
(`default(struct)` 绕过 fail-closed)上的一贯处置同向:`ConfigVersionDiff.Unknown`
刻意**取 `default`**,而其默认语义是「未知」而不是「无差异」。

### ⑤ v2 → v3 迁移 = 在 `FixedSize` 处插入 `count = 0`,并把版本号写成 3

- 迁移输入是**已剥掉校验和**的形态(`ISaveMigration` 契约),头部定长 = `FixedSize`,
  插入点与该形态无关,恒为 `FixedSize`。
- **写入的逐份版本集为空** —— v2 档从未记录过它们,**信息不存在**,不得伪造
  (与 ADR-034 §⑥ 拒绝 v1→v2 的理由同型;区别是这里的空集**有**正确语义:未知)。
- 迁移**不动** `WorldSeed` / 事件段 / 折叠段 / 载荷段(逐字节保留,链的断言会验)。

### ⑥ 组合值 `ConfigVersion`(头部字段)**不变**

`AC-51-D7` / ADR-014 §五 / ADR-010 §七 的面一字不改:头部仍写组合值、仍非致命比对。
新增的段是**追加的信息**,不是替代品 —— 否则等于推翻 ADR-014 §五 的「全局单一值」裁定。

### ⑦ 写侧来源 = `AddressablesDataProvider.LoadAllCookedHeaders()`(既有)

逐份版本 = 各产物头部 offset 12 的 u32(与 `DataCoreConfigVersion.Combine` 同一来源)。
**不新增派生路径**,组合值与逐份段出自**同一次装载** ⇒ 两者不可能互相矛盾。

## Consequences

- **正面**:AC-7a-14 的「供排查」从「两串十六进制」升级为「哪几张表」。零新算法(版本已在产物头里)。
- **负面**:存档体 +约 1 KB(22 × ~48 B);`SaveVersion` 升 3 使**所有既有 v2 夹具**
  走一次迁移(夹具输入是自造的,迁移必须真的跑通 —— 这反而是正面的:迁移链终于有真输入)。
- **风险 R1**:迁移脚本若把插入点算错(用了含校验和的偏移),尾部断言会红 —— 但**红在链里**,
  不是静默错读。判据 = 反证轮把插入点改成 `FixedSize + 32` 必须转红。
- **风险 R2**:`DataCoreAddresses` 若**改名**(非追加),旧档归因会指向不存在的表 ⇒
  差异集里出现「当前产物没有的 address」,按「已移除」报告。这是如实形态,不是缺陷。
- **R3(发布前门,沿用 ADR-034 口径)**:今日零真实存档 ⇒ v2 不可读零损失;
  **首次对外发档后,布局再变即不可逆**。

## Alternatives

- **A1 尾部追加新段**:被否 —— 破迁移链尾部断言(① 的论证)。
- **A2 写 address 索引而非字符串**:被否 —— 索引随装载序漂移,归因会指错表。
- **A3 让 `ConfigVersion` 变致命以「解决」不匹配**:被否 —— 直接推翻 ADR-010 §七 与
  ADR-007 Guidelines 5。ADR-010 §七 自己写明「须另开 ADR」,本件的取向相反:保留非致命,
  只增强**排查信息**。
- **A4 不给逐份段,只把问题登记成债**:被否 —— 债不会让信息在将来变得可恢复;
  而这个窗口一旦关上(首次发档),代价从 0 变成一次带迁移的布局变更 + 永久残缺的排查面。

## Validation(实现轮前全部未跑,禁借绿)

| # | 判据 |
|---|---|
| V-1 | 段往返:同集合 ⇒ 同字节;解码拒收非升序 / 重复 / 负 count |
| V-2 | **差异集恰好等于被改的那一张表**(改一份产物头 ⇒ `Compare` 只报它) |
| V-3 | v2 档迁移到 v3:版本 3、段 count=0、`WorldSeed` 与事件段逐位不变 |
| V-4 | v2 档读档后 `HasPerArtifactData = false`(不得报「无差异」) |
| V-5 | 写侧逐份版本与组合值**同源**(同一份 `LoadAllCookedHeaders`) |
| V-6 | 反证:插入点改 `FixedSize + 32` ⇒ 迁移轮转红(链断言) |

## Engine Compatibility

纯 BCL 字节布局与 C# 接口。零 post-cutoff API。Engine Knowledge Risk **LOW**。

## GDD Requirements Addressed

- `AC-7a-14` 末句(「仅记录并供排查」)——本件把「排查」从二元信息扩到逐表信息。
- 不改任何 GDD 文本;`persistence-service.md` 的 🔁 注将补引用本件。
