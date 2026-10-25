# Story 001: 存储抽象层（二进制 codec 接口 + 校验骨架）

> **Epic**: 7a 持久化服务
> **Status**: In Progress
> **Last Updated**: 2026-10-25
> 🔁 **2026-10-25 QA 轮退回(用户裁定 · 承签核 C-1,原文 `Status: Complete` 不删)**:
> 该件此前标 `Complete` 但 **7 条 AC 全未勾选**(`[x]`=0 / `[ ]`=7),而 `Type: Logic`
> ⇒ 自动化证据是 BLOCKING 门。qa-lead 判此为**第五个假绿形态**:用「文件已落盘」顶替「AC 已判定」。
> 本轮逐条核实后重开勾选,详见下方 §Acceptance Criteria 的逐条判据。
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
      > **2026-10-25 判定 = NOT-RUN**。仅有的关联测试 `save_service_test.cs:96`
      > `test_checksumFallback_readsBakWhenFinalCorrupted` 断言的是**「回退到 bak 后长度 = 68」**
      > (校验和回退路径),**未触及「三逻辑流完整重放」**,更未比对 outcome / 掉落清单 / id 高水位。
      > 真重放零实现 ⇒ 本条不可满足(QA plan §未及核查项 1 的 `R-2`)。
- [ ] **AC-7a-02**: 快照内容与三逻辑流不一致 ⇒ 各游戏事实以世界流/事件流为准（BLOCKING）
      > **2026-10-25 判定 = NOT-RUN**。`save_service_test.cs:4` 只有**文件头注释**提及该 AC 编号,
      > **零测试方法**。`grep -c "AC-7a-02" unity/Assets/Tests/EditMode/PersistenceService/save_service_test.cs` ⇒ 1(仅注释行)。
- [ ] **AC-7a-03**: 同一具名存档夹具，编辑器(Mono)与 IL2CPP 玩家上各序列化一次 ⇒ 字节流逐位一致（BLOCKING · 执行点 = ADR-012）
      > **2026-10-25 判定 = NOT-RUN**。`save_service_test.cs:159` 自陈「**简化版** —— 同一数据两次序列化应逐位一致」
      > ⇒ 测的是**同平台两次**一致,**不是 Mono vs IL2CPP 跨平台**。跨平台矩阵本机不可跑(`TD-ci-01` EXTERNAL)。
- [ ] **AC-7a-04**: 一条三流事件写盘再读回 ⇒ 字段值逐位往返不变；三次序列化同一状态 ⇒ 三次字节流逐位一致；IL 扫描零 BinaryFormatter/JsonUtility/PlayerPrefs（BLOCKING）
      > **2026-10-25 判定 = NOT-RUN(假绿)**。`save_service_test.cs:127` `test_simEventCodec_roundTrip`
      > 的断言**只有** `Assert.IsTrue(bytes.Length > 0, "编码后应有字节")` —— **未解码回比任何字段**,
      > 更未做三次序列化对拍、未做 IL 扫描。**该测对 AC-7a-04 的三项子判据零判别力。**
      > ⚠️ 这是本轮抓出的**第六个假绿形态**:测试名与 AC 编号都对,断言却只验「非空」。
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

**判据小结(2026-10-25 QA 轮)**:PASS **3** / NOT-RUN **4**(其中 AC-7a-04 = 假绿)。
⇒ 该件**不得**维持 `Complete`。剩余 4 条的清偿载体:
`AC-7a-01` / `AC-7a-02` 归 `TD-persist-03` 的真重放 · `AC-7a-03` 归 `TD-ci-01` 跨平台矩阵 ·
`AC-7a-04` 须补真往返夹具 + IL 扫描。

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
