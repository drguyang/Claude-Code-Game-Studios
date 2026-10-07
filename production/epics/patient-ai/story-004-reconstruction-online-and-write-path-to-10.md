# Story 004: 重建、联机单跑与写路径归 10 —— 端到端确定性与接缝验收

> **Epic**: 病人 AI 与行为
> **Status**: **Complete ✅ 2026-10-07**(补做评审轮:`review-patient-ai-story-004-2026-10-07.md` 落库 + 修复 + 复跑绿 —— 承 review-workflow「评当下,不追认」;原 2026-10-05 收口 `0b6f948` 的 37 测试与 5B/6M 修复轮留档)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-07

## Context

**GDD**: `design/gdd/patient-ai.md`(§Rules 一/七/八 派生态重建 · V8 联机:13 只在主机跑、客户端不重算(float 不逐位,承 ADR-012);病人位姿经 ADR-001 第二 QoS · §Rules 查体诱发痉挛 / 搬运昏迷病人的写路径归 10 · F 组 + E 组重建 AC · EC-13 在场进出/死亡不在场等边例)
**Requirement**: TR-patient-007(行为决策 = 派生态,不进流/不存档,加载/重放期确定性重建;住边界层不进 sim 程序集)· TR-patient-008(重建三源不变量:输入恰 ∈ {事件流, 版本化烘焙数据, 二者纯函数},禁第四来源)· TR-patient-021(「查体诱发痉挛」写路径归属 → ADR-027:归 10)· TR-patient-022(「搬运昏迷病人」写路径归属 → ADR-027:归 10)· TR-patient-014(id 经 IIdAuthority,与敌人共用空间与高水位)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-027(主): 13 病人 AI 的写路径归属;ADR-016: 分层确定性;ADR-012: 跨平台确定性 CI 门
**ADR Decision Summary**: ADR-027 裁定两条写路径**皆归系统 10**(与 CPR/止血同构的「玩家对病人的物理干预」),形态 = ADR-009 §七 三段式(意图事件 + 主机当下判距 + 效果进流)+ ADR-020 §四 对称落实;**13 只出表现(姿态骤变 + 呻吟),零写**;新 Kind 义务归 10 的 GDD 轮(经 entities.yaml + kindgen)。V8:13 只在主机求值,客户端不重算(`VitalsDto` 是 float,跨平台逐位不成立 → 归 ADR-012 域);病人表现态位置走 ADR-001 第二 QoS。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(联机)/ HIGH(跨平台逐位,EXTERNAL 挂 CI)
**Engine Notes**: 重建测试为纯 sim/边界层回放(LOW);联机接缝依赖 45 的 pipe 抽象(P0 预埋、P1b 实现,`unity/` 无第二 QoS 实装 ⇒ 该 AC 记 BLOCKED-BY 45,不借绿);IL2CPP 对拍须 CI 矩阵(EXTERNAL)。

**Control Manifest Rules (this layer)**:
- Required: 重建只从 {事件流, 烘焙数据, 二者纯函数} 取输入;写路径(痉挛/搬运)经 10 的意图事件 + 主机判定,13 仅订阅结果表现;13 程序集引用集 ⊆ 边界层允许集(不含 `Unity.Entities`/DOTS,承 ADR-017 门 A 内侧 + 表现层不引入)
- Forbidden: 13 直写三流或调用 `IIdAuthority.Next()`(id 仅消费);客户端本地重算 13 行为;把 float DTO 写回 sim/存档;用「给敌人/病人加事件」掩盖决策器分叉(承 27 规则二十一教训)
- Guardrail: 决策器分叉 = bug 非容差(EC-13 同型 EC-27-19);重建后 `acc`/格重新播种,不承诺与冻结前连续

---

## Acceptance Criteria

*From GDD `design/gdd/patient-ai.md`, scoped to this story:*

- [x] 端到端重建:一段含在场进出、恶化、会诊开闭、死亡的真实会话,存档→读档→重放,决策序列与原始运行**逐位一致**(重建三源不变量的落地判据,AC-13-B3 扩面)——
  **2026-10-07 登记口径**:P0 测试面 = **内存重置代理存档腿**(`ResetForLoad` fresh construct + 重新入表后 run1==run2 决策相等);**二进制序列化腿归 7a**(ADR-010,13 无 codec 义务)
- [x] 第四来源静态扫描:13 决策器全部读输入类型**恰 ⊆ 白名单**(正面反射断言,非「无 Vector3」负断言,承 27 AC-27-02 同法);注入 `Transform` 读取的负面夹具被拦下 ——
  闭包扫 + 方法体 IL 扫 + 签名扫 + 源码文本扫四台机器,负夹具共用同机(2026-10-07 修复轮重写)
- [x] 写路径归 10:「查体诱发痉挛」「搬运昏迷病人」两场场景 —— 13 侧**零 Append**;痉挛效果经 10 的意图事件→主机判定→效果进流,13 仅从结果表现(姿态骤变/呻吟);13 无新增 Kind 义务(归 10 的 GDD 轮,承 ADR-024)——
  **2026-10-07 登记口径**:13 侧零 Append + 三个处置载荷类型闭包**不可达**已断言;**端到端契约(10 写流→13 读表现)BLOCKED-BY-10 NOT-RUN**
- [x] id 边界:敌人与病人共用 `IIdAuthority` 空间与高水位成立(含敌人行场景,`max(id)+1` 扫三流并集排除 `PatientId.None`),13 只消费 id 不发号(AC-13-C4 扩面,承 ADR-006 Amendment B / ADR-016 §二)——
  **2026-10-07 登记口径**:逐流增量断言(病史 3 / 病例 6 / 世界 8);**TC-4 迁移腿(`max(id)+1` 重构)归 7a**;`None` 排除谓词在本 API 下不可观测(`m10` 注记),只钉可观测契约
- [ ] 联机:客户端不重算 13 —— 断言客户端进程内 13 决策器零求值;病人在客户端的表现(位置/姿态)来自第二 QoS 投影(该 AC **BLOCKED-BY 45**,P0 以桩+断言存在为准,NOT-RUN 照登)—— **NOT-RUN**(V8 桩已置防借绿闸门,摘 `[Ignore]` 即红)
  **转勾判据(2026-10-07 补,qa-lead 席)**:45 P1b 实装客户端装配 → 实现判据本体(客户端装配引用集零 13 决策器类型 / 求值计数)→ 摘 `[Ignore]` → 绿。
- [ ] 跨平台逐位:同事件流在 Mono/IL2CPP 各跑,13 决策轨迹哈希一致(挂 ADR-012 集成级夹具,EXTERNAL,CI 产物为证)—— **EXTERNAL**
  **拆腿 + 产出者现状(2026-10-07 补)**:① 夹具腿 = 轨迹哈希 + 四场景 tick 列表(Implementation Note 4)**尚不存在**,P0 可写、归 ADR-012 CI 轮;② 对拍腿 = Mono↔IL2CPP 依赖 `.github/workflows/unity-tests.yml:91-98` `il2cpp-determinism` job(**现为 TODO 空桩,无产出者**)⇒ Test Evidence 的 CI 产物按 `(EXTERNAL, 未产出)` 登记。
- [x] 程序集卫生:13 住边界层,不进 `Sim`(门 A);引用集白名单断言(不含引擎物理/ECS;EditMode 探针)——
  2026-10-07 修复:原负夹具为恒真 ⇒ 改**共用源码文本扫描机器**(`LoadProductionPatientAiSources` 正测 + 影子源文本负夹具)
- [ ] **[L]** 行为可读性走查:Idle/Seeking/Bedridden/InTreatment/Terminal 五档玩家可从姿态+音**无 UI** 读出(承支柱四「瘟疫表现为散落的病例」与 13 的 Player Fantasy;SIGN-OFF,[L])——
  **13 侧结构前提已闭**(五档落点表见走查件);**整体签署待 NR-S4-1(42)/ NR-S4-2、3(44)解除,本 story 不代签**

---

## Implementation Notes

1. 重建测试用 `Sim` 侧假时钟 + 事件流重放器,13 决策器以纯函数形式暴露(禁单例);播种点 = 在场进入 tick。
2. 写路径联调与 10 的 epic 共用夹具:10 的意图事件 Kind 若尚未入 registry(归 10 GDD 轮),13 侧以**契约测试**(订阅面形状)代替端到端,登记 BLOCKED-BY-10。
3. 联机桩:`IPresentPatients` 消费方(37)与客户端投影用 ADR-001 pipe 接口打桩;真实第二 QoS 走 45 P1b。
   ⚠️ **2026-10-07 边界评估订正**(qa-lead 席点名「未兑现主张」):原写「零重算断言可静态验证、不阻塞 P0」——
   **仓内零客户端装配**(仅 `Sim.Contracts` DTO 提及),静态断言**无主体** ⇒ 该半边**归 45 P1b 实装时落**,
   非当期可静态验证。P0 现状 = 桩 + `Assert.Fail` 防借绿闸门 + NOT-RUN 照登。
4. 跨平台轨迹哈希的夹具 tick 列表须含:滞回带内、Seeking→AtClinic、Terminal 闩锁、在场进出。
   ⚠️ **2026-10-07 边界评估登记**:该**夹具本体尚不存在**(全文件零轨迹哈希计算);`.github/workflows/unity-tests.yml:91-98`
   的 `il2cpp-determinism` job = `echo "TODO(实现轮)"` 空桩 ⇒ AC6 拆两腿 —— **夹具 + 单平台金标准(P0 可写,归 ADR-012 CI 轮)** ·
   **Mono↔IL2CPP 对拍(EXTERNAL,依赖 job 落地)**;测试体已置防借绿闸门(摘 `[Ignore]` 即红)。
5. 任何 13 侧新 Kind 需求 = 停,走 10 的 GDD 轮 + entities.yaml(禁 Amendment 追加通道,承 ADR-024 裁定③)。

---

## Out of Scope

- [Story 001–003]: 机制实现本体(本 story 是其端到端验收面)
- 10 的意图事件 Kind 注册与判定实现(归 emergency epic)
- 45 网络层实装(P1b;此处只立桩与「客户端零重算」门)
- 9 侧 VitalsDto 的跨平台浮点一致性(归 disease-sim / ADR-012 域)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 含全部关键转移的会话 | 存档→读档→重放 | 决策序列逐位一致 |
| TC-2 | 决策器注入读 `Transform.position` 的负面构建 | 反射扫描 | 白名单断言失败并点名 |
| TC-3 | 玩家对就诊病人触发痉挛(10 路径) | 观察三流 + 13 调用计数 | 恰 10 写流;13 Append = 0,仅表现响应 |
| TC-4 | 敌人与病人共存高水位 | 迁移后 next id | = max(三流并集)+1,排除 −1 |
| TC-5 | Mono / IL2CPP 对拍(CI) | 轨迹哈希 | 一致(EXTERNAL,产物链接) |
| TC-6 | 客户端进程桩 | 跑一段会话 | 13 决策器求值次数 = 0 |

**Edge cases**: 重放期间 `signs[]` 词条新增(表现变、决策不);读档落点 tick 恰为在场进/出边界;会诊未闭合时读档(SessionState 重置 None 的连锁口径)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: EditMode 夹具 + CI 矩阵产物链接 + `production/qa/evidence/patient-ai/story-004-readability-walkthrough.md`([L] 项)
> **2026-10-07 拆腿订正**(qa-lead 席点名「must exist 与产物不存在矛盾」):EditMode 夹具与走查件 = **must exist and pass / SIGN-OFF(已满足)**;CI 矩阵产物 = **(EXTERNAL, 未产出)** —— `il2cpp-determinism` job 现为 TODO 空桩,产出者落地前本行不构成缺口(见 AC6 拆腿登记)。

> **✅ 2026-10-07 修一处假红**:`FindRepoRoot()` 原把【本机绝对路径】`/home/gu/文档/nm/nm2/…` 写死,故只有桌面机的检出能跑通,超算 / CI / 任何其他克隆一律 `DirectoryNotFoundException`(`test_ac13assembly_patientAiRefSetWhitelist_catchesForbiddenRef_negativeFixture` 红)。改用 `[CallerFilePath]` 相对求解(与 `InputSystem.AxisProcessingTest` 同一手法)。
>
> **✅ 2026-10-07 补做评审轮测试证据**:
> - 真身夹具:`unity/Assets/Tests/EditMode/PatientAI/reconstruction_and_write_path_test.cs`(主)+ `spatial_behavior_test.cs` + `presentation_projection_test.cs` + `behavior_map_test.cs`(五档落点)—— **本仓测试住 `unity/Assets/Tests/EditMode/`,卡面原写 `tests/integration/patient-ai/` 为模板假路径,已订正**
> - PatientAI 过滤套件复跑:`unity/Logs/patientai-fix-2026-10-07.xml` —— **175 / 173 过 / 0 红 / 2 跳过**(基线 168/166/0/2,+7 新测;2 跳过 = `AC-13-CrossPlatform` EXTERNAL + `AC-13-V8` BLOCKED-BY-45,既有登记)
> - 全量 EditMode 复跑:`unity/Logs/editmode-full-2026-10-07.xml`
> - 评审原件:`production/qa/evidence/review-patient-ai-story-004-2026-10-07.md`(原判定 → 修复落点 → 验证命令)
>
> **Status**: [x] Created —— 夹具绿 + 原件落库 2026-10-07

---

## Dependencies

**Depends on**: Story 001 / 002 / 003 · ADR-027(已 Accepted)· 10 的意图事件 Kind(契约面,BLOCKED-BY 10 的 GDD/实现轮)· 45 pipe 桩(BLOCKED-BY 45)· ADR-012 CI 矩阵(EXTERNAL)
**Unlocks**: 系统 13 epic Definition of Done · 37 的端到端联诊场景 · 支柱二「人可倒不可死」在病人侧的证据链

---

## Completion Notes

### 2026-10-07 —— 补做评审轮收口(本轮即完成笔记)

- **Criteria**:8 条 AC —— **5 勾**(①②③④⑦)+ **3 未勾按登记**(⑤ BLOCKED-BY 45 · ⑥ EXTERNAL/CI · ⑧ [L] 待 NR-S4-1/2/3)= 62.5% ≤ 50% 未勾线,ADVISORY 记录非阻塞。
- **未勾 AC 与解除条件**(逐条可证伪,详见各 AC 行内「转勾判据 / 拆腿」注):
  - ⑤:45 P1b 实装客户端装配 → 判据本体落地 → 摘 `[Ignore]` → 绿(现 `Assert.Fail` 闸门)。
  - ⑥:夹具腿归 ADR-012 CI 轮(四场景 tick 列表见 Note 4);对拍腿依赖 `il2cpp-determinism` job(现 TODO 桩)。
  - ⑧:NR-S4-1(42 呈现接线)/ NR-S4-2、3(44 素材混音)解除后的走查轮签署(走查件 §四 `[ ]` 行)。
- **评审与证据**:原件 `production/qa/evidence/review-patient-ai-story-004-2026-10-07.md`(补做 · 评当下不追认);
  复跑 `patientai-fix-2026-10-07.xml` + `editmode-full-2026-10-07.xml`;走查件已重写(档1 判据订正)。
- **边界评估轮(同日,双子代理)**:S7 孤儿 `PatientMaterial` 删除 · AC6 防借绿闸门 · 具体类型 IL 负夹具补(M2 守卫)·
  Note 3/AC5/AC6/Test Evidence 措辞订正 · 突变批次实跑记录见原件 §四。
