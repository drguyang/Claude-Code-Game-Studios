# 评审原件 — M2 接线轮阶段 1 装配轮 · 2026-10-09

> **对象**:阶段 1 装配轮首批(新装配 `Gameplay.Boot` · BootRoot 组合根 · SimTickDriver · World.unity ·
> ADR-025 §① 增补 · Boot.unity 挂载 · EditMode Boot 测试)
> **流程**:实现(子代理)→ 主会话抽查 → 双代理评审(恰一轮)→ 合并修复 13 项 → 复跑绿 → 1 发变异 → 本原件
> **裁定背景**:sprint-04 §Phase 3 四裁定(2026-10-09,`f28d71c`);组合根 = 案 A 新装配

---

## 一、原判定(双代理评审 · 恰一轮 · 2026-10-09)

### 代码面(`lead-programmer`)**APPROVE**(0 BLOCKING + 13 ADVISORY)

核过零问题 18 项,关键:b3 清单封闭性双向差集实算 0(15↔15)· 装配三环构造签名逐一匹配 ·
tick 泵「Advance 返回值逐次驱动 OnTickEdge」与「每 tick 恰一次」等价 · `_worldLoadHandle` 常驻
确非泄漏(默认 `ReleaseSceneWhenSceneUnloaded`,包源实读)· World.unity 零相机/零监听/零
MonoBehaviour · ADR-025 修订窄(5 行)· 新代码零手搓 `PayloadRef` · 越界检查:Sim 四程序集源码零改动。

| # | 级 | 位置 | 缺陷 | 处置 |
|---|---|---|---|---|
| 代码-1 | ADVISORY-高 | adr-025 §① Gameplay.Boot 行 | 「无人引用」失实(EditMode 测试装配引用) | **本轮修**(措辞收窄「无生产装配引用」) |
| 代码-2 | ADVISORY-高 | `BootRoot.cs:89` | init 句柄 Release 在 try 外,失败路径不释放 | **本轮修**(try/finally) |
| 代码-3 | ADVISORY-高 | `BootRoot.cs:122` | 玩家落活动场景未钉死(拆序卸 World 可能连玩家销毁) | **本轮修**(`MoveGameObjectToScene` → Boot) |
| 代码-4 | ADVISORY-高 | 全库 | `OnPositionSample` 零调用点 ⇒ 跨格写者运行期恒不发事件 | **登记**(BootRoot.Update 文档注 + 本原件 §二之二;归 Phase 1 尾/Phase 2) |
| 代码-5 | ADVISORY-高 | `composition_root_test.cs:38` | PatientSpawner 仅 NotNull,装配链第 3 环零行为覆盖 | **本轮修**(T4 端到端) |
| 代码-6 | ADVISORY-中 | `AssemblyGates.cs:241` | b6 扫描面未含 Gameplay.Boot(O-6 同型) | **本轮修**(扩面 + 判据测试改三面) |
| 代码-7 | ADVISORY-中 | `AssemblyGates.cs:46` | 注释「七装配」陈旧 | **本轮修**(八装配) |
| 代码-8 | ADVISORY-中 | `SimTickDriver.cs:104` | 死亡螺旋丢余量无登记条目 | **登记**(本原件 §二之二;已知行为,非确定性背离 —— 流内无墙钟) |
| 代码-9 | ADVISORY-中 | `CompositionRoot.cs:87` | 不可达死代码 + `<exception>` 过度声明 | **本轮修**(注记「不可达兜底」,不删码) |
| 代码-10 | ADVISORY-中 | ADR-023 §② 执行面 | 零 gameplay 构建期扫描工具不存在(既存) | 登记(归 S2 spike/Tooling,本轮不阻断) |
| 代码-11 | ADVISORY-低 | `AssemblyGates.cs:180` | b4 扫描面/白名单同缺 Gameplay.Boot(当前零冲突) | 登记(扩面时同批改白名单) |
| 代码-12 | ADVISORY-低 | `composition_root_test.cs` | WorldSeed==0 未断;payload seq 与流 Seq 可分叉未断 | **本轮修**(T3:patientId 非默认 + Seq==0 断言 + 注释) |
| 代码-13 | ADVISORY-低 | S6 语义无调用方 | 纸面语义勿冒充实测 | **登记**(T6 改名 + 注释;S6 spike 归 Phase 2 同批) |

### 测试面(`qa-lead`)**FIX-THEN-APPROVE**(2 BLOCKING + 8 ADVISORY)

| # | 级 | 缺陷 | 逃逸变异 | 处置 |
|---|---|---|---|---|
| 测-B1 | **BLOCKING** | 本批改动(新 asmdef+两场景+工具)后无全量 EditMode 复跑证据;门日志无判决行 | 过滤集外被新 asmdef 打红被三条过滤 XML 掩盖 | **本轮修**(全量复跑,§三) |
| 测-B2 | **BLOCKING(裁定归主会程)** | 门④ 组合根冒烟仍 EditMode 直测;BootRoot 生命周期/真实场景加载/Update 转发零 Play 验证 | Boot 挂载 guid 错/场景未注册 ⇒ EditMode 三条照绿 | **裁定:按门定义(「playtest 前」Go/No-Go)排期至收口轮前置,显式登记 NOT-RUN,禁借绿**(§二之二) |
| 测-1 | ADVISORY | 缺等值边界 `Advance(0.05)==1` | `>=`→`>` 全绿 | **本轮修**(T1) |
| 测-2 | ADVISORY | S6 测试名 overclaim(halt 无调用方) | — | **本轮修**(T6 改名 `remainderSurvivesAcrossCalls` + 注) |
| 测-3 | ADVISORY | round-trip patientId 恰默认值;decoded.Seq 未断 | 整字段不写照绿 | **本轮修**(T3:patientId=5 + Seq 断言) |
| 测-4 | ADVISORY | PatientSpawner 仅存在性断言 | 装配错线照绿 | **本轮修**(T4) |
| 测-5 | ADVISORY | AssembleCore IOE 不可达死代码无测 | — | 登记(代码-9 同批注记) |
| 测-6 | ADVISORY | Boot.unity 挂载 guid 与 Addressable 注册零测试 | guid/注册被改全绿 | **本轮修**(T5 两条门测;Addressable 侧 gitignored 目录缺失则 Ignore) |
| 测-7 | ADVISORY | SimTickDriver 三条 fail-loud 无测 | 删守卫照绿(tickSeconds=0 会死循环) | **本轮修**(T2) |
| 测-8 | ADVISORY | 方法命名与 test-standards 不全对齐 | — | 登记(库内既有松散口径同病,统一轮补) |

测试面另附:round-trip 与死亡螺旋两测判别力**合格**(逐变异推演:Store 空操作/Encode 空/Encode -1/
上限失效/告警重复,各恰红对应断言);假绿扫描干净(真流真池、非 O-7 自洽环、EditMode 无静默跳过);
确定性零随机零墙钟。

---

## 二、修复落点(13 项全落 · 修复代理执行 · 主会话复核锚点)

代码:F1 init 句柄 try/finally(`BootRoot.cs:89-94`)· F2 玩家移入 Boot 场景(`:126-133`)·
F3 八装配注释 + b6 扫描面增 `Assets/Gameplay.Boot`(`AssemblyGates.cs:46,247`)·
F4 判据测试改「恰三面」(`o6_actor_cell_payload_test.cs:239-266`)· F5 adr-025 措辞收窄(:117)·
F6 不可达兜底注记(`CompositionRoot.cs:86-89`)· F7 OnPositionSample 缺口登记(Update 文档注)。
测试:T1 等值边界 · T2 三条 fail-loud(断 ParamName)· T3 patientId=5 + Seq==0(实读
`PatientSpawner.SpawnNext` 确认载荷 seq 为占位 0,流 Seq 另发号)· T4 SpawnNext 端到端
(DiseaseOnset 入流 + 解码 + 池字节 + id 差值恰 1)· T5 挂载 guid 恰 1 + Addressable 共现
(`boot_scene_wiring_test.cs`,新)· T6 改名 + 注。

### 二之二、登记不修(承双评审,5 项)

代码-4(`OnPositionSample` 零调用点 → Phase 1 尾/Phase 2 接采样)· 代码-8(死亡螺旋丢余量 =
**已知行为**:丢的是逻辑时间,流内无墙钟 ⇒ 非确定性背离;登记于此)· 代码-10(零 gameplay
构建期扫描 → S2 spike/Tooling)· 代码-11(b4 面同缺,扩面时同批)· 测-8(命名统一轮)·
测-B2(**门④ PlayMode 冒烟 = 收口轮前置 NOT-RUN**:Go/No-Go 门定义在「人工 playtest 前」,
阶段 1 不借绿;收口轮(Phase 3 #7)执行前必须补齐,5 门全绿才开 playtest)。
另 F6 口径注:AssembleCore 共 6 条 IOE,本轮注「兜底」三条(产品),参数三条属前置校验未注
(有 T2 级 ANE 断言族覆盖同类面),留档不展开。

---

## 三、验证命令与实数

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
unity test unity --mode EditMode --filter "SimTickDriverTest|CompositionRootTest|BootSceneWiringTest" \
  --output unity/Logs/editmode_boot_p1fix.xml
unity test unity --mode EditMode --output unity/Logs/editmode_full_boot_p1_20261009.xml
```

| run | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| 过滤 p1(评审前) | 5 | 5 | 0 | 0 | 首批 5 条 |
| 过滤 regress(EventStream\|CellTransition\|HostAuthority\|StreamBound) | 39 | 38 | 0 | 1 | 既有 VR Ignore |
| **过滤 p1fix(修复后)** | **10** | **10** | **0** | **0** | T1/T2/T4/T5×2 净增 + T6 改名 |
| **全量 EditMode(修复后)** | **3069** | **3022** | **0** | **46** | 基线 3059/3012 ⇒ +10(新 5 + 既有 Boot 5);46 skip 全既有,零新增零转绿 |
| 变异(`>=`→`>`) | 10 | 9 | **1** | 0 | 唯红 = T1 等值边界(恰判别) |
| 变异还原复跑 | 10 | 10 | 0 | 0 | python 反向恢复,0 MUT 残留 |

⚠️ 全量慢性返回「Unity 进程以代码 2」(既往同款环境怪癖)—— **XML 为判据**(根
`type="TestSuite" name="unity`),failed=0 判绿。CameraPresentationDisciplineTest 12/12
(16:57,晚于 World.unity 导入)证明 AudioListener 全场景仍恰 1。

---

## 四、判定链

**实现落盘 → 主会话抽查(源码两件通读 + guid/锚点/XML 三方核)→ 双代理恰一轮评审**
(代码面 APPROVE 0B+13A · 测试面 FIX-THEN-APPROVE 2B+8A)→ **13 项修复全落**
(测-B2 裁定排期收口轮前置 NOT-RUN,非放行)→ **复跑绿**(过滤 10/10 · 全量 3069/3022/0 红)→
**1 发变异恰中**(`>=`→`>` ⇒ T1 唯红)→ **APPROVE 收口**(登记 6 项不修 + 门④ NOT-RUN 前置)。

**未跑/未核声明**:PlayMode 本轮零跑(测-B2 裁定排期);编辑器真机 Play 未验(Boot 四步启动序
首次运行验证归门④/收口轮);`AssemblyGates.RunMenu` 整菜单未点(private 入口,b3 以等价脚本复算 0 差)。
