# 实现证据件:7a 生产接线轮 —— 瞬时源实装 + 兜底/退出保存接通(2026-10-25)

> 指令记录:**2026-10-25 用户裁定「生产接线今日不为零,现在修」** —— 推翻 act4 件
> §七.1 登记的「生产接线今日为零 = 有意终态」(原文不删,该登记如实记录了裁定时的状态;
> 本件是它的订正件)。前序件:`impl-td-persist-05-act4-2026-10-25.md`。

## 一 瞬时源实装:`Gameplay.Boot/NoPendingMedicalActionQuery.cs`

- 实现 `INarrativeContextQuery`(裁定甲具名接口):`InNarrativeContext() = !IsArmed`
  —— 读 **`IArmedState.IsArmed`**(ADR-013 Amendment A;生产实现 = `EmergencyChannelDriver`,
  `DirectChannelState.Armed` = 急救动作进行中)。
- **W5 证伪形态不回归**:这是**独立于流的内存态**(瞬时),不是流内「曾出现过锚点」(累积)。
  零缓存、每次调用重答 —— 顺延语义与读档门都建立在这上面。
- **Bind 晚绑定**(装配序约束:`EmergencyChannelDriver` 构造段晚于 `SaveService` 段):
  未 Bind 期恒 `false`(fail-closed);Bind 只许一次(热替换 = 读档门与兜底门分叉)。
- SHA-256:`00bdce179c38447c…`。

## 二 兜底接通

- `CheckpointFallback.OnTicks(long tickCount)` 新增:整批推进(等价 N 次 `OnTick`)——
  **一帧推进 N tick 时按帧驱动会把 N 计成 1,interval(以 tick 计)被帧率稀释**;负数拒收。
- `CompositionRoot`:`narrativeContext` 提升为装配段单变量,**三处共用同一实例**
  (`SaveService` 门 · `CheckpointFallback` · `Bind`)—— 规则十四「一个量」的结构性保障;
  兜底闭包 = `Checkpoint(台账 NextWritableSeq)`(「下一个可写槽位」单一出处;
  try/catch + LogError,与 BootRoot 退出钩子同款纪律 —— 兜底在 tick 边沿跑,未兜的异常会杀帧)。
- `BootRoot.Update`:tick 边沿序列内、急救通道检查**之后** `_services.CheckpointFallback?
  .OnTicks(Δtick)`(halt 期无 Advance ⇒ 无边沿 ⇒ 不驱动,承 S6 停机时钟语义)。
- `CompositionRootServices`:新增 `CheckpointFallback` / `NarrativeContext` 两属性
  (后者是 7b 呈现侧灰置的登记复用面,非本轮新义务)。
- `CheckpointFallback.cs` SHA-256:`18578ccee8195df6…`;`CompositionRoot.cs`:`82f0debe18a35502…`。

## 三 生产行为面变化(与接线前对比)

| 面 | 接线前(裁定丙) | 接线后 |
|---|---|---|
| `Checkpoint`(定时兜底触发) | 必抛(narrativeContext null) | **可用**:Armed 期拒,Idle 期写 |
| `SaveOnExit`(BootRoot 退出钩子) | 必抛 ⇒ 被 try/catch 兜成「置位 + 日志」 | **真写** `save_exit.bin`(独立文件名,不占玩家槽);try/catch 保留为纵深 |
| `Load` 门 | 必抛 | 可用:Armed 期拒(AC-7a-19 (c) 安全门**首次有真源**) |
| 事件触发半边(锚点接线) | 零 | **零(不变)** —— 仍归 `TD-persist-04` 动作 ① |

## 四 首验轨迹(如实)

- 第一轮三红,**三条全是本轮夹具自身的错**,生产代码零改动:
  ① composition 测试没传 provider ⇒ Checkpoint 走到 compose 的 E-13(ConfigVersion 派生)
  —— 断言改为「异常不再是 narrativeContext 拒绝」(门已过、失败点前移的证据);
  ② doubleBind 夹具首绑源忘了置 Armed(断言与消息不符);
  ③ batchEquals 在批中间断言计数为零(批越过触发 tick 后计数应为 2,非 0)。
- 修正后四夹具(CheckpointFallbackTest + NoPendingMedicalActionQueryTest +
  CompositionRootTest + SaveComposerWiringTest)= **42/42 绿**(`EXIT=0`)。
- 两处既有 fail-closed 钉按 🔁 演变惯例翻转(⑤):
  `save_composer_wiring_test` 两条(轨迹注 ①→⑤)+ `composition_root_test` 一条;
  `data_core_config_version_wiring_test` 的「判据不可达 NOT-RUN」注随之过期并订正
  (compose 自此可达,「provider 缺失 ⇒ 调用时抛 E-13」判据由 ⑤ 翻转件承载)。

## 五 证伪轮(8 变异全红、零假绿;驱动器 `falsify_fallback.py` MF8/MF9 + `falsify_wiring.py` NG1–4/WG1–2)

| 变异 | 停用什么 | 结果 |
|---|---|---|
| MF8 | OnTicks 批稀释(整批只计 1 格) | 红 3 |
| MF9 | 删 OnTicks 负数拒收 | 红 1 |
| NG1 | 查询未 Bind 期放行(fail-closed 破) | 红 2 |
| NG2 | 查询极性翻转(Armed 放行) | 红 4(含装配级钉) |
| NG3 | 删 Bind 单次守卫 | 红 1 |
| NG4 | 删 Bind null 拒收 | 红 1 |
| WG1 | 装配根删 Bind 调用 | 红 2 |
| WG2 | 兜底拆第二实例(「一个量」装配面) | 红 1(同实例反射钉) |

每轮后 SHA-256 逐字节还原(基线 = 终态):
`CheckpointFallback.cs 18578ccee8195df6…` · `NoPendingMedicalActionQuery.cs 00bdce179c38447c…` ·
`CompositionRoot.cs 82f0debe18a35502…`。
复算:`python3 falsify_wiring.py --list`(变异表)与各 `*-falsify-*.xml`(每轮原始判定)。

## 六 回归三元组

- 本轮全量 EditMode:**`4987 / 4952 / 0 / 35`**(`w-full` · `2026-10-25T09:52 UTC` · commit `5dc9369` 工作树,**未提交**)。
- 基线:`4978/4943/0/35` @ `td10` · `2026-10-25T09:31 UTC` · `5dc9369` 工作树
  (引自 `impl-td-persist-05-act4-2026-10-25.md` §六)。
- 等式:`4987 − 4978 = 9` = 恰好本轮新增(`CheckpointFallbackTest` +3 ·
  `NoPendingMedicalActionQueryTest` +5 · `CompositionRootTest` +1)✓ **零回归**
  (`failed = 0`、`skipped = 35` 与基线持平)。
  复算:`python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('$HOME/.claude/jobs/181888b1/tmp/w-full.xml').getroot(); print(r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))"`。

## 七 缺口与如实登记(禁借绿)

1. **BootRoot 的 `OnTicks(Δtick)` 接线行本身无独立反证** —— 它的语义(按 tick 数驱动)
   由 `CheckpointFallbackTest` 的 OnTicks 钉承载;该单行 glue 若被改坏
   (如改成 `OnTicks(1)`),现有夹具**不会红**。如实登记 NOT-RUN,不借 MF8 的绿。
2. **事件触发半边仍 open**(8 / 37 / 52 锚点接线 + 出诊启动 Kind)—— `AC-7a-20`
   整体仍 `PARTIAL`;本件接通的是定时兜底半边 + 瞬时源。
3. **瞬时源覆盖范围 = 急救 Armed 一类**:GDD 语境的「出诊流程进行中」(52,零载体)与
   「病人未处置」锚点半语义不在本查询;各落地轮**并入本查询**(单一「一个量」),
   已登记于该类文件头与 `TD-persist-04`。
4. **BootRoot / services bag 的 SaveOnExit 注释已按 ⑤ 过期订正**(原文不删);
   `CheckpointFallback.cs` 文件头「生产接线点今日为零」同批订正。
5. **兜底闭包的 try/catch 是杀帧防护,不是错误处理策略**:组版 / 门的编程错误会以
   LogError + 下一 interval 重试的形态出现(可见但不阻塞帧循环);写盘失败本体已在
   worker 内兜住(GDD `:117`),不经此路径。
