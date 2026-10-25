# 实现证据件:TD-persist-04 动作① —— 事件触发 checkpoint 集中观察接线(2026-10-25)

> 前序:**裁定轮同日**(四裁定全取推荐,原件 = 债册 `TD-persist-04` 裁定块;
> `HouseCallStarted` 已入 registry)。本件 = 实现轮。

## 一 裁定 → 落点对账

| 裁定 | 落点 |
|---|---|
| ① 集中观察 | `Sim.Codec/AnchorCheckpointTrigger.cs`(`Drain` 游标扫新尾段);`BootRoot.Update` tick 边沿驱动(兜底之后);写者(8/37/52)零改动 |
| ② Kind 映射 | `DefaultAnchors` = {`JudgmentRecorded`, `CaseClosed`, `HouseCallStarted`}(类级静态,`whitelistMatchesRegistryMapping` 逐员字面钉);registry `EVENT_CHECKPOINT_ANCHORS` 同日已加映射注 |
| ③ 复用带门入口 | 触发器预问门(不安全 ⇒ 该次不触发,锚点消费,兜底顺延补上)+ 闭包 = 同一带门 `Checkpoint(台账 NextWritableSeq)`;瞬时源同一实例(装配反射钉) |
| ④ 同批一次 + 清兜底 | 触发在扫描循环后(结构性每 drain ≤ 1 次);成功 ⇒ `onFired` → `CheckpointFallback.NotifyExternalCheckpoint()` 窗口清零;**失败(`Func<bool>` = false)⇒ 不清**(不变量保护) |

## 二 registry / kindgen 面(`HouseCallStarted` 动作② 结构兑现)

- `EventKind.cs` 追加区 + `HouseCallStartedPayload`(WorldPayloads;`TargetPatientId` 引用非发号);
  kindgen 计数预期 36 → 37,**重跑 A1–A5 全过**,`StreamRouting.g.cs` 37 支 switch 再生
  (复算:`python3 tools/kindgen/kindgen.py --check`;`grep -c "name: SimEvent.Kind\." design/registry/entities.yaml` = 37)。
- ⚠️ `HouseCallStarted` 今日**零写入者**(52 的 GDD 轮义务)—— registry 与枚举先行,
  触发器的白名单含它 = 面向「只增不删」的结构完备,不构成「52 已接线」(禁借绿)。

## 三 首验

- w1(= 唯一验):**34/34 绿**(`AnchorCheckpointTriggerTest` 11 + `CheckpointFallbackTest` 11 +
  `CompositionRootTest` 12;`EXIT=0`)。
- 装配断言扩展:触发器在袋(saveBasePath 有 ⇒ 非 null,无 ⇒ null)+ 触发器 `_context`
  与袋暴露 `NarrativeContext` 反射同实例(「一个量」第三处)。

## 四 证伪轮(9 变异全红、零假绿;驱动器 `~/.claude/jobs/181888b1/tmp/falsify_act1.py`)

| 变异 | 停用什么 | 结果 |
|---|---|---|
| AT1 | 尾对齐改游标恒 0(同锚点重触发) | 红 4 |
| AT2 | 删门预问(不安全仍触发) | 红 1 |
| AT3 | 失败仍通知(没存成却清兜底窗口) | 红 1 |
| AT6 | 删空白名单守卫 | 红 1 |
| AT7 | 白名单漂移(HouseCallStarted 出表) | 红 2 |
| WG3 | 装配根删触发器构造 | 红 1 |
| NF | `NotifyExternalCheckpoint` 空转 | 红 1 |
| AT4′ | 删尾部游标对齐(游标永不推进) | 红 4 |
| AT5′ | 逐锚点触发(删同批一次语义) | 红 7 |

- **变异打错靶两次,如实登记**(均非夹具假绿,系变异与原码语义等价):
  ① 初版 AT4 打「开头钳制」⇒ 绿 —— 实测**钳制冗余**(尾对齐自愈覆盖),生产码**删该死代码**,
  测试改名 `..._noSpuriousFire_newAnchorsStillFire`,换打「删尾对齐」⇒ 红 4;
  ② 初版 AT5 删早退 `break` ⇒ 绿(触发在循环后,结构性单次)—— 换打「逐锚点触发」⇒ 红 7。
- **批次出处如实**:AT1–3 / AT6–7 / WG3 / NF 七轮跑在删钳制**前**的触发器
  (SHA `ee7b8429d01f9598…`);AT4′/AT5′ 跑在删钳制**后**(`86dd22fc68610bdc…`)。
  两态间唯一差异 = 钳制死代码删除,其语义惰性恰由初版 AT4 的「绿」证明(等价变异)。
  三文件终态 SHA-256(逐字节还原):
  `AnchorCheckpointTrigger.cs 86dd22fc68610bdc…` · `CompositionRoot.cs cf6b03990eff95e1…` ·
  `CheckpointFallback.cs cd8be7a889a713ed…`。

## 五 回归三元组

- 本轮全量 EditMode:**`4999/4964/0/35`**(`act1-full2` · `2026-10-25T10:21 UTC` · commit `d157b4e` 工作树,**未提交**;
  复算:`python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('$HOME/.claude/jobs/181888b1/tmp/act1-full2.xml').getroot(); print(r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))"`)。
- 基线:`4987/4952/0/35` @ `w-full` · `2026-10-25T09:52 UTC` · `5dc9369` 工作树
  (引自 `impl-persist-wiring-2026-10-25.md` §六)。
- 等式:`4999 − 4987 = 12` = 恰好本轮新增测试方法(`AnchorCheckpointTriggerTest` 11 ·
  `CheckpointFallbackTest` +1;`CompositionRootTest` 只加断言不加方法)✓ **零回归**。

### 五.1 Kind 增员连锁七红收口(同批)

首轮全量 `4999/4957/7/35`(`act1-full`,7 红全为 `HouseCallStarted` 入 registry/枚举的 closed-set 连锁,
实现逻辑零回归)。处置(同批落盘):

- **codec 分派三文件**:`PayloadCodec.World.cs` 具名 `Encode(in HouseCallStartedPayload)` +
  `DecodeHouseCallStarted`(tag 1 = `TargetPatientId` i32);`PayloadCodec.cs` / `PayloadEncoder.cs`
  分派表各加一行 `case`;三处 default 哨兵串「不在 36 支闭集内」→「**37** 支」
  (`SimEventCodec.cs` 同款)。往返测试经 `Activator.CreateInstance` 兜底自动覆盖 37/37。
- **b7 写者存在性门**:`WriterExistenceGate.ExemptKinds` 为 `HouseCallStarted` 登记**具名豁免**
  (理由 + 归属轮 = 52 GDD 轮)—— 零写入者是已登记债务,不为绿写假写者、不放宽门;
  判据①(有写者挂豁免 = 红)自动约束撤表时机。
- **计数钉三处**:decoder `dispatchCoversAll36Kinds` → **37Kinds**、encoder
  `dispatchCovers34Kinds` → **35Kinds**(37 − 2 Judgment,单参重载 35)、
  drug_event 闭集哨兵 36→37(探针串同步)+ decoder 往返覆盖断言 36→**37**;
  注释级「36 支」残留五文件一并订正(承 🔁 演变惯例:钉子随增员更新,原文不删历史轮次)。

## 六 缺口与如实登记(禁借绿)

1. **BootRoot 的 `Drain(Stream.Events)` 单行接线无独立反证** —— 若被改成 no-op 或挪出
   tick 边沿,现有夹具不会红(与上一轮 `OnTicks` 行同款 NOT-RUN);事件到达 checkpoint 的
   端到端滞后 ≤ 1 tick(下一边沿扫到),属语义内。
2. **52 侧零写入者** —— `HouseCallStarted` 今日不会在真流里出现;三条锚点中今日可达的
   只有 `JudgmentRecorded` / `CaseClosed`(垂直切片写者已落)。
3. **restore 与首次 drain 之间落流的事件,索引落在旧游标下会被跳过**(头注登记的已知缝)
   —— 生产 Load 接线落地时**读档路径必须调 `ResetCursor()`**;今日生产无 Load 调用点,
   义务登记给 Load 接线轮。
4. **「出诊流程进行中」语义不在瞬时源**(裁定③ 面)—— 52 落地时并入
   `NoPendingMedicalActionQuery`(单一「一个量」),不另立。
5. **触发器闭包的 LogError 是杀帧防护**:组版 / 门编程错误降级为日志 + 兜底补存,
   不阻塞帧循环;写盘失败本体在 worker 内兜住,不经此路径。
