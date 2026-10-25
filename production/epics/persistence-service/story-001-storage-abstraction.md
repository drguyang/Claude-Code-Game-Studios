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
