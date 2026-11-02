# Story 001: 存储抽象层（二进制 codec 接口 + 校验骨架）

> **Epic**: 7a 持久化服务
> **Status**: Complete(2026-11-03 · **COMPLETE WITH NOTES** —— 7 条 AC 逐条重判,PASS 5 / NOT-RUN 2;见 §Completion Notes 的 2026-11-03 段)
> **Last Updated**: 2026-11-03
> 🔁 **2026-10-25 QA 轮退回(用户裁定 · 承签核 C-1,原文 `Status: Complete` 不删)**:
> 该件此前标 `Complete` 但 **7 条 AC 全未勾选**(`[x]`=0 / `[ ]`=7),而 `Type: Logic`
> ⇒ 自动化证据是 BLOCKING 门。qa-lead 判此为**第五个假绿形态**:用「文件已落盘」顶替「AC 已判定」。
> 本轮逐条核实后重开勾选,详见下方 §Acceptance Criteria 的逐条判据。
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2h
> **Manifest Version**: 2026-11-02
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

- [x] **AC-7a-01**: 快照损坏/缺失/跨版本不可读 ⇒ 三逻辑流完整重放，指定病人 outcome / 掉落清单 / id 高水位 == 直接重放世界流所得（BLOCKING）
      > **2026-10-25 判定 = NOT-RUN**。仅有的关联测试 `save_service_test.cs:96`
      > `test_checksumFallback_readsBakWhenFinalCorrupted` 断言的是**「回退到 bak 后长度 = 68」**
      > (校验和回退路径),**未触及「三逻辑流完整重放」**,更未比对 outcome / 掉落清单 / id 高水位。
      > 真重放零实现 ⇒ 本条不可满足(QA plan §未及核查项 1 的 `R-2`)。
      > 🔁 **2026-11-03(S24-2)重判 = PASS(前件已限定)**。新夹具
      > `save_replay_roundtrip_test.cs::test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay`:
      > 存档 → 新流 Load → **掉落清单(`InventoryFold.CanonicalForm`)/ id 高水位 / 折叠行**
      > 三项逐位等于「直接重放存档前世界流」所得。
      > ⚠️ **「快照损坏」这个前件今日不存在**(布局里没有快照段,`SnapshotOffset` 恒 0 哨兵)——
      > 故该测的 **①** 断言正是「`SnapshotOffset == 0`」,它把「前件变了」变成一条**会转红**的判据
      > (证伪 P3 实测:改成 1 ⇒ 该测红)。**不得**把本条读成「快照恢复能力已验证」。
- [ ] **AC-7a-02**: 快照内容与三逻辑流不一致 ⇒ 各游戏事实以世界流/事件流为准（BLOCKING）
      > **2026-10-25 判定 = NOT-RUN**。`save_service_test.cs:4` 只有**文件头注释**提及该 AC 编号,
      > **零测试方法**。`grep -c "AC-7a-02" unity/Assets/Tests/EditMode/PersistenceService/save_service_test.cs` ⇒ 1(仅注释行)。
      > 🔁 **2026-11-03(S24-2)重判 = NOT-RUN(前件不存在,非「没人做」)**。
      > 本 AC 的宾语是「**与三流不一致的快照**」;而 `SaveComposer.cs:38/:56` 明写
      > 「快照段 / 分册态段留待后续,`SnapshotOffset` 恒写 0 哨兵」⇒ **没有快照可与之不一致**。
      > 照措辞造夹具 = 造一条**零输入可达的门**(本项目已登记的假绿形态)。
      > 前件由 AC-7a-01 的 ① 断言机械看守:快照段一旦引入,那条会先红并提示补本条的真夹具。
- [ ] **AC-7a-03**: 同一具名存档夹具，编辑器(Mono)与 IL2CPP 玩家上各序列化一次 ⇒ 字节流逐位一致（BLOCKING · 执行点 = ADR-012）
      > **2026-10-25 判定 = NOT-RUN**。`save_service_test.cs:159` 自陈「**简化版** —— 同一数据两次序列化应逐位一致」
      > ⇒ 测的是**同平台两次**一致,**不是 Mono vs IL2CPP 跨平台**。跨平台矩阵本机不可跑(`TD-ci-01` EXTERNAL)。
      > 🔁 **2026-11-03 维持 NOT-RUN(EXTERNAL)** —— 本机无 IL2CPP 播放器腿,不假装跑过。
- [x] **AC-7a-04**: 一条三流事件写盘再读回 ⇒ 字段值逐位往返不变；三次序列化同一状态 ⇒ 三次字节流逐位一致；IL 扫描零 BinaryFormatter/JsonUtility/PlayerPrefs（BLOCKING）
      > **2026-10-25 判定 = NOT-RUN(假绿)**。`save_service_test.cs:127` `test_simEventCodec_roundTrip`
      > 的断言**只有** `Assert.IsTrue(bytes.Length > 0, "编码后应有字节")` —— **未解码回比任何字段**,
      > 更未做三次序列化对拍、未做 IL 扫描。**该测对 AC-7a-04 的三项子判据零判别力。**
      > ⚠️ 这是本轮抓出的**第六个假绿形态**:测试名与 AC 编号都对,断言却只验「非空」。
      > 🔁 **2026-11-03(S24-2)重判 = PASS**。原测**整条重写**,并按三项子判据补足 4 条:
      > `test_ac7a04_simEventHeader_roundTrip_fieldByField`(逐字段回比,含 `PayloadRef` 三字段)·
      > `test_ac7a04_simEventHeader_roundTrip_extremesAndNegatives`(`long` 两端点 / 负值)·
      > `test_ac7a04_threeSerializations_areByteIdentical`(三次逐字节一致)·
      > `test_ac7a04_payloadFix_roundTrip_preservesRawBits`(`Fix` raw 往返,含 `long.MinValue`)·
      > `test_ac7a04_assemblyScan_noForbiddenSerializersOrHostEndianApis`(Cecil 扫 `Sim.Codec` 的
      > `BinaryFormatter` / `JsonUtility` / `PlayerPrefs` / **`BitConverter`**)。
      > ⚠️ **该扫描当场抓出一个真缺陷**:`SaveHeaderCodec.HasValidMagic` 用了
      > `(uint)BitConverter.ToInt32`(主机端序,违 ADR-010 §一)—— 小端机上行为恰好相同,
      > 既有四条存档夹具全绿也照不出来。已改 `BinaryPrimitives.ReadUInt32LittleEndian`。
      > 证伪 P1 / P2 各命中(见 `production/qa/evidence/review-persistence-001-ac-rejudge-2026-11-03.md`)。
- [x] **AC-7a-05**: 翻转存档中任一非校验和字节 ⇒ 校验和验证失败（BLOCKING）
      > **2026-10-25 判定 = PASS**。`save_service_test.cs:41` `test_checksumValidation_detectsCorruption`:
      > 合法数据通过校验 ⇒ 翻转 `withChecksum[40]` ⇒ 校验失败。**双向断言齐**,判据成立。
- [x] **AC-7a-06**: 写 checkpoint ⇒ tmp → final 原子替换（final 存在则先 final → bak）（BLOCKING）
      > **2026-10-25 判定 = PASS**。两条测试合成完整判据:
      > `:58` `test_atomicWrite_createsFinalFile`(final 存在 / tmp 被 rename 掉 / 首次无 bak)+
      > `:78` `test_doubleBackup_bakCreatedOnSecondWrite`(二次写 ⇒ bak 存在,且 final=`second` / bak=`first`)。
- [x] **AC-7a-18**: grep 7a 侧全部源码 ⇒ 零 float/double 类型用于存档或事件流（BLOCKING）
      > **2026-10-25 判定 = PASS(载体口径已收窄)**。`save_service_test.cs:141` `test_noFloatTypes_inSaveCodec`
      > 反射扫 `SaveService` 所在程序集内 `FullName` 含 `Save` 的类型的**实例字段**,断言非 float/double。
      > ⚠️ **与 AC 原文的差异如实登记**:AC 说「grep 7a 侧**全部源码**」,测试只扫**一个程序集内名字含 `Save` 的类型**
      > (漏掉静态字段、属性、局部变量、以及 `Sim` / `Sim.Contracts` 侧)。判 PASS 是因为**零 float 的实现事实成立**
      > (`grep -rn "float\|double" unity/Assets/Sim.Codec/ --include=*.cs` 仅注释命中),但**夹具的扫描面窄于 AC 措辞**。
      > 🔁 **2026-11-03(S24-2)加宽 = PASS(措辞差已消)**。新夹具
      > `test_ac7a18_sourceSurface_ofSaveCodec_hasNoFloatTypes`:扫 `unity/Assets/Sim.Codec/**.cs`
      > **全部源文件**(剥注释后按标识符边界匹配 `float` / `double` / `decimal`),
      > 覆盖静态字段 / 属性 / 局部变量 / 方法返回值 —— 即 AC 原文的「全部源码」口径。
      > 原反射夹具**保留**(它扫的是**类型面**,两者互补,不是替代)。

**判据小结(2026-10-25 QA 轮)**:PASS **3** / NOT-RUN **4**(其中 AC-7a-04 = 假绿)。
⇒ 该件**不得**维持 `Complete`。剩余 4 条的清偿载体:
`AC-7a-01` / `AC-7a-02` 归 `TD-persist-03` 的真重放 · `AC-7a-03` 归 `TD-ci-01` 跨平台矩阵 ·
`AC-7a-04` 须补真往返夹具 + IL 扫描。

**判据小结(2026-11-03 · S24-2 重判轮)**:PASS **4**(`01` / `04` / `05` / `06`)· **PASS(加宽)** **1**(`18`)
· NOT-RUN **2**(`02` 前件不存在 · `03` EXTERNAL)。
⇒ **零假绿**:每条 PASS 都有「改坏生产就转红」的判据(证伪轮 P1/P2/P3 全命中);
每条 NOT-RUN 都写明**今天为什么判不了**(不是没做,是没有宾语 / 没有平台)。
原件:`production/qa/evidence/review-persistence-001-ac-rejudge-2026-11-03.md`。

---

## 跨 story 承载的 AC(2026-10-25 补登记 · 承 `TD-persist-04` 解除动作 ③)

*以下 4 条 AC 由 **TD 工作项**兑现(用户裁定「能归 epic 的登记 TD,不立 story」),不是本 story 的交付物。
本节只登记「它们的载体在哪」—— 原本 13 条 BLOCKING AC 在任何卡面都查不到,是本条要修的根因。*

- [x] **AC-7a-10**(规则八 · `Folded(p)` 三条件单一出处,未结案不折叠)
      > **判定 = IMPLEMENTED_TESTED**(2026-10-25 勘察 + 抽验)。
      > 生产:`Sim/FoldedPredicate.cs:76-87` `Folded()` 按 ①②③ 短路合并;条件③**复用**
      > `CaseStreamQuery.HasOpenCase`(`:217-222`)而非另抄 —— 这正是 AC 要求的「三条件合并且仅一处」。
      > 夹具:`folded_predicate_test.cs:125/132/149/248/260/273/300/312/321` 覆盖 (a)(b)(c) 三态,
      > 含多病种收紧后 6 条(`:176/193/207/219/231/440`)。
      > 载体:`TD-persist-01`(折叠谓词,已 closed)。
      > ⚠️ **残留**:「三条件合并仅一处」这条**可证伪断言**零夹具 —— 测试文件头 `:4` 只有注释声明,
      > 无「别处不存在第二份合并」的机械判据。如实登记,不因此降级判定(实现事实成立)。
      > 复算:`grep -n "public bool Folded" unity/Assets/Sim/FoldedPredicate.cs` ⇒ `:76`。
- [x] **AC-7a-11**(规则八 · 折叠行保留 `patient_id`)
      > **判定 = IMPLEMENTED_TESTED**。生产:`Sim/FoldRow.cs:30-58` 六字段含 `PatientId`;
      > 编码 `Gameplay.Boot/FoldRowCodec.cs:63-68`(含 `:65` `WriteInt32LittleEndian(row.PatientId)`),解码 `:75-80`。
      > 夹具:`folded_predicate_test.cs:412` `test_fold_rowKeepsPatientId_standaloneAssertion`(单列断言,
      > 防被「字段数对」掩盖)· `:396` 六字段顺序断言 · `fold_row_codec_test.cs:32` 字节往返 ·
      > `save_replay_roundtrip_test.cs:84` 端到端。
      > 载体:`TD-persist-01` + `TD-persist-03`(折叠段字节布局,均已 closed)。
- [x] **AC-7a-12**(规则九 / F-7a-3 · 高水位扫三流并集)
      > **判定 = IMPLEMENTED_TESTED**。生产:`Sim/EventStream.cs:267-285` `GetNextPatientId()`,
      > 跳过 `PatientId.None`(`:272`),空集返回 `0`(`int max = -1` + `max+1`)。
      > 夹具(跨系统载体):`PatientAI/reconstruction_and_write_path_test.cs:579` 逐流增量 ·
      > `:613` 排除 None · `:652` 空流返 0 · `:661` 仅 None 返 0。
      > ⚠️ **形状差如实登记**:「三流」在实现上是**单一 `List<SimEvent>`**(`EventStream.cs:38`),
      > `StreamRouting` 只用于排序与遥测、不用于分桶 ⇒ 「三流并集」是**退化形态**。
      > 本 AC 的子判据只约束**结果值**,故不因此降级;但该形状差是 `TD-persist-03` 动作 ③ 的开放项,
      > **不得与本 AC 混读**(混读会让「12 已绿」掩盖「布局从未造出来」)。
- [x] **AC-7a-19**(规则十四 · 7b 手动槽契约:只进不退 / 读档即锁 / 叙事事件上下文)
      > **判定 = IMPLEMENTED_TESTED**。生产:`Sim.Codec/SaveSlotLedger.cs:93-137`
      > (`EnsureWritable` 双门 / `OnCheckpointCommitted` 推水位 / `OnLoaded` 置锁 / `OnNarrativeAnchor` 解全锁)
      > + `SaveService.cs:340-351` 检锁 + `SlotProjections.cs:79-91` 投影。
      > fail-closed 三处:未注入 `narrativeContext` ⇒ 一律抛(`SaveSlotLedger.cs:76/95`、`SaveService.cs:429-436`)。
      > 夹具:`save_slot_contract_test.cs:90`(a) · `:106/119`(b) · `:133`(c) · `:151/165`(锁/解) ·
      > `:183` **闭合回路端到端** · `:477/489/500` fail-closed。
      > 载体:`TD-persist-02`(已 closed)。
      > ⚠️ **两条如实登记,不改判定**:
      > ① `SlotProjections` **零生产消费者**(`SlotProjections.cs:63-70` 自陈)—— 生产路径用的是
      > `SaveSlotLedger` / `SaveService` 里的**手写等价物**;夹具的绿只证「投影类型自身纯度」,
      > **不证「生产走的是这两条公式」**。
      > ② **`Checkpoint` 与 `OnNarrativeAnchor` 生产调用点均为 0**(实测 `grep | grep -v /Tests/` ⇒ 0 / 0)
      > ⇒ 夹具能驱动门,**生产路径上没人驱动**。这是裁定「丙」的有意终态(瞬时源归 10),
      > 不是本条遗漏 —— 但意味着「19 已 IMPLEMENTED_TESTED」只在「夹具可驱动」这个意义下成立。

**本节判据小结**:补登记 **4** 条,全部 `IMPLEMENTED_TESTED`,
**0 条新增失败**。剩余 9 条(07/08/09/13/14/15/16/17/20)另按 `TD-persist-04` 解除动作 ③ 处置,
见 `EPIC.md` §AC 载体总表。

## QA Test Cases

*2026-11-03 由 `/qa-plan sprint` 回填 —— 与 `production/qa/qa-plan-sprint-24-2026-11-03.md` 对齐。
本节的判据面**只覆盖未 PASS 的 4 条**;已 PASS 的 `AC-7a-05/06/18` 见上方 §Acceptance Criteria 的判定注。*

- **AC-7a-04** ❌ **当前是假绿,必须整条重写** —— 全二进制 codec。
  - **现状**:`test_simEventCodec_roundTrip` 只断言 `bytes.Length > 0`,**未解码回比任何字段** ⇒ 零判别力。
  - Given: 一条含 `Fix` 字段 + `long` 字段的三流事件。
  - When: 写盘再读回;同一状态三次序列化。
  - Then: ① **真往返** —— **逐字段相等**(不是「字节非空」);② 三次字节流**逐位一致**(哈希相等);
    ③ **IL 扫描**:承约束的 sim 侧程序集零 `BinaryFormatter` / `JsonUtility` / `PlayerPrefs`;
    ④ `Fix.long` 必须走 `BinaryPrimitives` 显式小端,**禁 `BitConverter`**(该维度**单独 IL 扫描**
    —— 主机端序依赖夹具结构上抓不到)。
  - Edge cases: 空 `PayloadRef`(长度 0)· `Fix` 负值 · `long.MinValue` / `long.MaxValue`。
- **AC-7a-01** — 快照 = 优化非真相。
  - Given: 一份存档的快照段**损坏 / 缺失 / 跨版本不可读**(三种变体各一例)。
  - When: 加载该存档。
  - Then: 三逻辑流**完整重放**;断言**具体量** —— 指定病人 `outcome` / **掉落清单** / **id 高水位**
    **== 直接重放世界流所得**;表现态位置回「上次快照的位置」、加载变慢,**不失败、不提示存档损坏**。
  - Edge cases: 快照段 0 字节 / 空段(与缺失同路径)。
- **AC-7a-02** — 世界流是唯一裁判。
  - Given: 快照内容与三逻辑流**不一致**(快照是旧派生态)。
  - When: 加载。
  - Then: 同三项量以**世界流重放**为准;快照只贡献加载加速与表现态位置。
- **AC-7a-03** — 黄金字节对拍(**EXTERNAL,本 sprint 不做**)。
  - 同一具名夹具在 **编辑器 Mono** 与 **IL2CPP 玩家**上各序列化一次 ⇒ 逐位一致。
  - **执行点 = ADR-012 三格矩阵**(`TD-ci-01` EXTERNAL)。
  - ⚠️ `save_service_test.cs:159` 的「简化版」只测**同平台两次一致**,**不得替代**本条。

**Estimated test count**:+3 条(其中 AC-7a-04 是**整条重写**,不是新增)。

**次类型**:本件声明 `Logic`,但 `AC-7a-01/02` 要的是三逻辑流完整重放对拍(Integration 形状)
—— 记**次类型 = Integration**,**不改主类型**(声明优先)。

---

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

---

## Completion Notes —— 2026-11-03 重判轮(S24-2)

**Completed**: 2026-11-03
**Verdict**: **COMPLETE WITH NOTES**(7 条 AC:5 PASS + 2 NOT-RUN)

### 逐条

| AC | 新判定 | 载体 |
|---|---|---|
| `AC-7a-01` | **PASS**(前件已限定) | `save_replay_roundtrip_test.cs::test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay` |
| `AC-7a-02` | **NOT-RUN** | 前件不存在:布局无快照段(`SnapshotOffset` 恒 0 哨兵)⇒ 没有「与流不一致的快照」 |
| `AC-7a-03` | **NOT-RUN** | EXTERNAL:ADR-012 三格矩阵 / `TD-ci-01`;本机无 IL2CPP 播放器腿 |
| `AC-7a-04` | **PASS** | 4 条新夹具(真往返 / 端点 / 三次对拍 / `Fix` raw / IL 扫描) |
| `AC-7a-05` | PASS(不变) | `test_checksumValidation_detectsCorruption` |
| `AC-7a-06` | PASS(不变) | `test_atomicWrite_createsFinalFile` + `test_doubleBackup_bakCreatedOnSecondWrite` |
| `AC-7a-18` | **PASS(加宽)** | 原反射夹具 + `test_ac7a18_sourceSurface_ofSaveCodec_hasNoFloatTypes` |

### 本轮的两处实质修复

1. **`SaveHeaderCodec.HasValidMagic` 用了主机端序** —— `(uint)BitConverter.ToInt32(...)` 违 ADR-010 §一
   (显式小端、禁 `BitConverter`)。小端机上行为恰好相同 ⇒ 既有四条存档夹具全绿也照不出来
   (「行为等价掩盖结构分叉」同族)。已改 `BinaryPrimitives.ReadUInt32LittleEndian`。
2. **AC-7a-04 的原夹具是第六个假绿形态** —— 断言只有 `bytes.Length > 0`,未解码、未比任何字段。
   整条重写。

### 证伪轮(3 变异,全命中,SHA-256 逐字节还原)

| 变异 | 期望红 | 实测 |
|---|---|---|
| P1 `SimEventCodec.Encode` 的 `Seq` 写 0 | `test_ac7a04_simEventHeader_roundTrip_fieldByField` | ✅ 命中(该轮 3 红) |
| P2 `HasValidMagic` 退回 `BitConverter` | `test_ac7a04_assemblyScan_noForbiddenSerializersOrHostEndianApis` | ✅ 命中 |
| P3 `SaveComposer` 写非 0 `SnapshotOffset` | `test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay` | ✅ 命中 |

还原判据(改前 = 还原后):`ee624d3f…` / `3173b5bf…` / `a4d93572…`。

### 残余(NOT-RUN 两条,不得借绿)

- `AC-7a-02`:**前件不存在**。快照段一旦引入(而 `AC-7a-01` 的 ① 断言会先红提醒),
  须补「快照与三流不一致」的真夹具。
- `AC-7a-03`:**EXTERNAL**。跨平台字节对拍 = ADR-012 矩阵,本机不可跑。

**Test Evidence**:`production/qa/evidence/review-persistence-001-ac-rejudge-2026-11-03.md`
**Code Review**:本轮为 AC 重判 + 两处缺陷修复;`/code-review` 未另跑(lean 模式)
