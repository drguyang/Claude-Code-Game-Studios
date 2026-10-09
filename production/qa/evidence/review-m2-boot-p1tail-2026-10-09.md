# 评审原件 — M2 接线轮阶段 1 尾(输入→采样接线)· 2026-10-09

> **对象**:阶段 1 尾 delta —— `MovementFeed` / `MovementInputReader`(新)· `PlayerController`
> 两处偏离(懒绑定 + 速度记账真 bug 修复)· `BootRoot.Update` 薄壳化 · asmdef +`Unity.InputSystem`
> · `movement_feed_test.cs`(4 条 + 修复轮增 1 条回归)· ADR-025 §① 引用集回写
> **流程**:实现(子代理)→ 主会话抽查 diff → 双代理恰一轮评审 → 合并修复 F1–F8 → 复跑绿
> → 2 发判别力变异恰中 → 本原件
> **前置**:阶段 1 装配轮原件 `review-m2-boot-phase1-2026-10-09.md`(代码-4 缺口即本批闭合对象)

---

## 一、原判定(双代理评审 · 恰一轮 · 2026-10-09)

### 代码面(`lead-programmer`)FIX-THEN-APPROVE(0 BLOCKING + 10 ADVISORY)

核过零问题 15 项(速度修复推演成立 · 帧内次序无错格路径 · ADR-025 §① 与 asmdef 逐项一致 ·
b3 差集仍 0 · Sim 四程序集零改动 · 越界面仅两处已声明偏离 · IL 门不受影响 · 主机唯一 Append 未破)。

| # | 级 | 位置 | 缺陷 | 处置 |
|---|---|---|---|---|
| 代码-1 | ADVISORY-高 | BootRoot.cs:149 · World.unity | 生产静置点骑 y 格界(胶囊中心 ≈1.0 恰 FloorToInt 边界);测试用地面顶 0.5 规避而生产未保护,启动期可能 1~2 条 Y 翻转噪声 | **登记**(结构性,挂 EC-1/AC-1-21 归世界几何/调参轮;门④ PlayMode 实测) |
| 代码-2 | ADVISORY-中 | MovementInputReader.cs:34-58 | W+D 对角出口 (1,1) ‖·‖=√2>1,与类备注及测试④矛盾;EditMode 无输入断言恒真空过 | **本轮修**(F1 出口径向钳制) |
| 代码-3 | ADVISORY-中 | MovementFeed.cs:50-57 | `axis/m` float32 舍入可略 >1(千分级概率),AC-1-09 严格 throw | **本轮修**(F2 出口二次钳制) |
| 代码-4 | ADVISORY-中 | PlayerController.cs:190 | Move 热路径每帧 `LoadDefault()` ~44B 堆分配 | **本轮修**(F3 static 缓存) |
| 代码-5 | ADVISORY-低 | PlayerController.cs:211-215 | 零输入急停(Decel 跨帧路径不可达);与原实现一致非本批回归 | 登记(手感轮核对 F-1-3/R9) |
| 代码-6 | ADVISORY-低 | PlayerController.cs:213-215 | 方向瞬时反转 + TurnRate 死配置;pre-existing | 登记(手感轮) |
| 代码-7 | ADVISORY-低 | PlayerController.cs:221 | 接地常数 -0.5f*dt 与 GDD EC-1 字面 v_y=-0.015 不符;pre-existing | 登记(调参轮核对) |
| 代码-8 | ADVISORY-低 | PlayerController.cs:210,216 | EC-6 MAX_DT 未钳位(受 Unity maximumDeltaTime 硬上界保护);GDD 值待定 | 登记(调参轮定值后实现) |
| 代码-9 | — | 提交纪律 | 固定排除项照旧 | 提交时排除 |
| 代码-10 | — | MovementInputReader.cs 头注 | ADR-011 §一 action 走廊偏离已登记(装载器缺失=未来故事) | 确认合规 |

### 测试面(`qa-lead`)FIX-THEN-APPROVE(1 BLOCKING + 6 项)

判别力推演 M1–M11:10 发合格;M3(次序颠倒)**逃逸**、M9(速度旧式)**未核实**。

| # | 级 | 缺陷 | 处置 |
|---|---|---|---|
| 测-B1 | **BLOCKING** | 全量复跑数字 3073/3026/0/46 未在任何评审材料声明(仅 gitignored XML) | **本轮修**(本原件 §三 + active.md 补录) |
| 测-2 | 实质 | 头注钉死「位移→采样→边沿」但次序颠倒变异全绿(提交晚一帧仍满足全部断言) | **本轮修**(F5:事件格 == 提交时刻位置判格) |
| 测-3 | 实质 | `_actorId` 恒 0(IdAuthority 初值 0)⇒ 身份断言 0==0 空转(O-6 同型) | **本轮修**(F4:fixture 丢一号取二号 + Greater(0) 守卫) |
| 测-4 | 实质 | 速度修复无直接回归;dt≈0.333 旧式残值可能单帧吸收(推演存疑) | **本轮修**(F7:变异实证 3 红 + 新增自由落体回归测) |
| 测-5 | 次要 | ∞ 分支只测了 NaN | **本轮修**(F6) |
| 测-6 | 缺口 | BootRoot.Update 薄壳转发零测 | **登记**(Boot.unity wiring 测试 + 亲核覆盖;门④ PlayMode 收口轮补) |
| 测-7 | 缺口 | MovementInputReader 键盘/手柄择一、对向抵消、摇杆钳制零测(读取面断言空转) | **本轮修**(F8:InputSystem 虚拟键盘注入,断言非空转;手柄侧仍登记) |

### 主会话抽查(修复前)

- PlayerController diff 亲核:速度 bug 修法自洽(水平分量独立记账 + 落地清负 y + currentSpeed
  只取水平);懒绑定两路径均正确。
- 测试 XML 原件亲核:`editmode_full_after_p1tail.xml` = 3073/3026/0/46 ·
  `editmode_boot_p1tail.xml` = 14/14(与自报一致)⇒ 测-B1 属**声明缺失**,非证据缺失。

---

## 二、修复落点(F1–F8 · 修复代理执行 · 主会话复核锚点)

| # | 修了什么 | 落点 | 验证 |
|---|---|---|---|
| F1 | ReadMoveAxis 键盘出口径向钳制(‖kb‖>1 ⇒ normalized)+ 注释同步 | MovementInputReader.cs:37-49,64-66 | 测试④注入后 ‖·‖≤1 + 全量绿 |
| F2 | ToMoveInput 归一出口二次钳制(float32 舍入超额) | MovementFeed.cs:56-64 | 测试④ diag + 全量绿 |
| F3 | `static readonly DefaultConfig` 缓存(消每帧 ~44B);数值未动 | PlayerController.cs:53,198 | 全量绿;**连带**踩 AC-1-27 门 ⇒ 白名单增列 `typeof(LocomotionConfig)`(纯常量配置,主会话核字段面零游戏状态),motor_lease_test.cs:246-250,门最终绿 |
| F4 | fixture 先 NextPatientId() 丢一号再取二号(_actorId≥1)+ `Assert.Greater(_actorId,0)` 防丢号步被删 | movement_feed_test.cs:54-59 | ①③ 身份断言以 id=1 通过 |
| F5 | `CommitTimeCell(player)` = 复用 `CellTransitionDetector.CellFromPosition`(不造第二判格);①③ 断言事件格 == 提交时刻位置格 | movement_feed_test.cs:93-97,167,223 | **变异实证**:次序颠倒恰红 ①③(mut_order_swap.xml 2 红) |
| F6 | ④ 补 `ToMoveInput(+∞ 轴)==zero` | movement_feed_test.cs:246-249 | 绿 |
| F7 | 变异验证:python 反向替换还原旧速度式 → **恰红 3 条**(新回归测 7.61 vs 0.83 · ① 跨格 +2 · ② 噪声 9 条)→ 恢复(逐字节一致);因变异已红,补 1 条自由落体回归测 | PlayerController.cs(变异往返)· movement_feed_test.cs(新测) | mut_old_velocity.xml 3 红;恢复后过滤 57/54/0/3 |
| F8 | InputSystem 虚拟键盘注入(AddDevice/QueueStateEvent/Update + TearDown 清理)⇒ 读取面断言非空转 | movement_feed_test.cs:258-266,331-364,80 | 注入后 ReadMoveAxis>0 断言真生效 |

### 二之二、登记不修(承双评审,8 项)

代码-1(生产静置骑 y 格界 → EC-1/AC-1-21 + 门④ PlayMode 实测)· 代码-5(急停 Decel → 手感轮)·
代码-6(瞬时反转/TurnRate 死配置 → 手感轮)· 代码-7(接地常数 -0.5 vs GDD -0.015 → 调参轮核对)·
代码-8(MAX_DT → 调参轮定值)· 测-6(BootRoot.Update 转发 → 门④)· 测-7 手柄侧(虚拟设备注入
仅键盘落地;手柄择一/钳制零测,登记)· action 装载器偏离(代码-10,已有文件头登记,改键 overrides
生效前置依赖未来装载器故事)。

---

## 三、验证命令与实数

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 过滤(修复后)
unity test unity --mode EditMode \
  --filter "MovementFeedTest|SimTickDriverTest|CompositionRootTest|BootSceneWiringTest|CellTransitionTest|LocomotionChainTest|MotorLeaseTest" \
  --output unity/Logs/fix_filtered_final.xml
# 全量
unity test unity --mode EditMode --output unity/Logs/editmode_full_p1tail_fix.xml
# 判别力变异
# ① 旧速度式(python 反向替换)→ mut_old_velocity.xml ② 次序颠倒 → mut_order_swap.xml
```

| run | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| 修复前过滤(实现轮自报,原件亲核) | 14 | 14 | 0 | 0 | `editmode_boot_p1tail.xml` |
| 修复前全量(实现轮自报,原件亲核) | 3073 | 3026 | 0 | 46 | `editmode_full_after_p1tail.xml`(测-B1 声明补录于此) |
| **过滤最终(修复后)** | **71** | **68** | **0** | **3** | 3 跳全既有 Ignore |
| **全量最终(修复后)** | **3074** | **3027** | **0** | **46** | 基线 3073/3026 ⇒ +1 = F7 新回归测;46 跳全既有 |
| 变异·旧速度式 | 5 | 2 | **3** | 0 | 恰红 = 新回归测 + ① + ②(判别力合格) |
| 变异·次序颠倒 | 5 | 3 | **2** | 0 | 恰红 = ①③ 的 CommitTimeCell 断言(测-2 逃逸已闭) |
| 变异恢复后复跑 | 57 | 54 | 0 | 3 | python 反向恢复,与修复版逐字节一致 |

⚠️ 全量慢性 EM_EXIT 非零(Unity 退出码 2,既往同款)—— **XML 为判据**(根
`type="TestSuite" name="unity`),failed=0 判绿。跑前独占检查:无编辑器进程、无 UnityLockfile。

---

## 四、判定链

**实现落盘 → 主会话 diff 亲核(速度修复 + 懒绑定自洽)→ 双代理恰一轮评审**
(代码面 FIX-THEN-APPROVE 0B+10A · 测试面 FIX-THEN-APPROVE 1B+6 项)→ **F1–F8 修复全落**
(含 F3 连带 AC-1-27 白名单增列,主会话核字段面零游戏状态)→ **复跑绿**(过滤 71/68/0 ·
全量 3074/3027/0 红)→ **2 发变异恰中**(旧速度式 3 红 · 次序颠倒 2 红;python 恢复复跑绿)→
**APPROVE 收口**(登记 8 项不修,含代码-1 结构性骑格界挂门④)。

**未跑/未核声明**:PlayMode 本轮零跑(门④ 收口轮前置 NOT-RUN 承阶段 1 裁定);
生产配置(地面顶 0 + 出生 1.0)下静置 Y 翻转噪声未实测(代码-1,门④ 顺带);手柄侧虚拟设备
注入未做(测-7 手柄分支登记);`LocomotionConfig` 数值(5f/10f 硬编码)归调参轮未动。
