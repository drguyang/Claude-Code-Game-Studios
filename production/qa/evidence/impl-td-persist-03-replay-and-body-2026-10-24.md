# TD-persist-03 实现轮证据原件 —— 三流重放 + 存档体 v1 布局(折叠段)

**日期**:2026-10-24 · **执行**:AutoClaw 会话(`181888b1`)· **commit**(跑测时工作树):`6198671`
**债条目**:`docs/tech-debt-register.md` `TD-persist-03`(2026-10-24 `PRE-S19-D` 甲案立债)
**用户四裁定**(2026-10-24):① 字节布局 = 修 ADR-010 承认单列表 + 折叠段;② 重放抽象点 = 不立新点(BCL 委托 + `EventStream.Restore`);③ 恢复三关 = 三分化(绕 CAP · 留去重/单调 · Seq 不重发);④ `SaveVersion` 仍为 1。
**同日追加裁定**:发号器钉测回归 ⇒ 采纳「组最大号播种」(`_restoredGroupMax` 两遍扫描,只抬不压)—— 既有显式 Seq 计数器语义(`event_stream_test` o1 / dedup 两钉)一字未动。

---

## 一、改动清单(Landed)

| 文件 | 动作 | 内容 |
|---|---|---|
| `unity/Assets/Sim/EventStream.cs` | 改 | 新增 `Restore(events, foldedRows)`(替换语义;折叠行唯一性校验先于 Clear);新增 `_foldedRows` / `_restoredGroupMax` 字段;`AppendCore` 增播种块(只抬不压);`GetNextPatientId` 计折叠行高水位(ADR-006 Amendment B);`Clear()` 清 `_foldedRows` + `_restoredGroupMax` |
| `unity/Assets/Gameplay.Boot/FoldRowCodec.cs` | 新 | 折叠段编解码:`[count:i32][36B 定长行 × count]`,行序 = ADR-010 §二 六字段逐字;outcome ∉ {1,2} 解码期即拒;count 负/超界/未读尽均抛 |
| `unity/Assets/Gameplay.Boot/SaveComposer.cs` | 重写 | `Compose(..., existingFoldedRows)`:折叠循环 → `SpliceBack`(键集成员判定,`IdentityKey = Kind_Patient_Tick_Seq`)→ 头部(`SnapshotOffset=0` 哨兵)+ 事件段(**三流合一**)+ 折叠段 → `AddChecksum`;病例流/世界流不再静默丢弃 |
| `unity/Assets/Sim.Codec/SaveService.cs` | 改 | ctor 增 `foldedRowSink`(null ⇒ fail-closed);`Load` 契约次序 = 先 `replaySink(events)` 后 `foldedRowSink(折叠段原始字节)`;`ParseBody` 复合体读(`ReadWithinBody` + 折叠段必备,<4 字节按损坏拒收) |
| `unity/Assets/Sim.Codec/SimEventListCodec.cs` | 改 | `Read`(读尽断言,独立段用)包装新 `ReadWithinBody`(无读尽,复合体用) |
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 改 | replaySink/foldedRowSink 双委托闭包(pendingReplay 合并喂 `stream.Restore`);compose 闭包传 `existingFoldedRows: stream.FoldedRows`(回写义务) |
| `docs/architecture/adr-010-persistence-save-format.md` | 改 | §一 增补块(v1 布局图 + 四裁定逐条 + 定长位置布局与禁位置打包的关系 + 实现落点) |

**装配边零新增**:`Sim` / `Sim.Codec` / `Sim.Contracts` 三方 asmdef `references` 一字未动 —— 与 `IPayloadEncoder` / `IPayloadDecoder` 同款「不立新抽象点、BCL 委托跨界」(用户裁定 ②)。

## 二、测试面(20 条新增,全绿)

| 文件 | 条数 | 钉什么 |
|---|---|---|
| `EditMode/PersistenceService/eventstream_restore_test.cs` | 9 | 三分化(CAP 绕过带 Append 对照 · 单调保留批非原子 · 去重保留 · Seq 不重发 · 播种接续=4)+ 折叠行(高水位 41⇒42 · 重复行抛 · 替换语义 · 只读视图) |
| `EditMode/PersistenceService/fold_row_codec_test.cs` | 8 | 往返 · 黄金字节逐位(六字段序 + 偏移钉)· outcome=0/未知拒 · count 负/超界 · 未读尽 · 空段 |
| `EditMode/PersistenceService/save_replay_roundtrip_test.cs` | 1 | 端到端:Checkpoint(可折病人)→ 新流 Load(Restore)→ 事件段不含被折行 / 折叠行=1(patient_id+seed 必留)/ 高水位 7⇒8 / **二次 Checkpoint 折叠行回写**(回写义务钉) |
| `save_composer_wiring_test.cs`(改写) | +2 | 折叠段字节含行 · `existingFoldedRows` 回写 · 全零折叠 `count=0` · 反向判据(不折 ⇒ 字节与旧版逐位同) |
| `folded_predicate_test.cs` / `save_slot_contract_test.cs` | — | ctor 断言 6→7 形参;MinimalSaveBytes 补 4 字节空折叠段 |

**元数据变更**:PersistenceService 面 146 条(此前 126);全集 +20 与上表逐条对齐。

## 三、回归数字(三元组)

- **EditMode**:`4822 / 4787 / 0 / 35` @ `2026-10-24 23:11 CST` · 批次 `tdef01f`(全量)· commit `6198671`
  (基线 `4802/4767/0/35` @ `2026-10-24 21:25 CST` · `tdef01f` · `8ba3fb4` ⇒ **total +20 = 新增测试数,零回归**)
- **PlayMode**:`202 / 202 / 0 / 0` @ `2026-10-24 23:12 CST` · 批次 `tdef01f`(全量)· commit `6198671`
  (基线 `202/202/0/0` @ `2026-10-24 19:51 CST` · `s19-smoke-playmode` · `9e23c42` ⇒ **逐位一致**)
- 复算命令(XML 各自落 `unity/Logs/` 同款格式;本轮 XML 在 job tmp,判读式):
  `grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' <xml> | head -1`
- 批后编译(纯注释订正后):EXIT=0,`grep -c "error CS"` ⇒ **0**(`tdp03-recompile.log`)。

⚠️ 诚实登记:基线 `4802/4767` 的 commit 字段是 `8ba3fb4`,本轮跑测工作树 = `6198671` + 11 项未提交改动(与本轮改动集同树连续演化);无中间提交,差集可归因(20 新增,无消失、无翻转)。

## 四、证伪轮 M1–M8(S12-4 形态,八轮全红)

**纪律**:变异 → 跑 PersistenceService 面 → 还原在**同一命令内**完成;还原判据 = SHA-256 逐字节等于备份(基准哈希:`EventStream 996d0a04…` · `FoldRowCodec 1b94ce46…` · `SaveComposer 724be0d5…` · `SaveService b2dccf7c…`);变异窗口内不读被变异文件,判定只读 XML。锚串唯一性断言(恰命中 1 次,否则中止不动盘)。

| # | 变异(停用哪个守卫) | 结果 | 转红的测试 |
|---|---|---|---|
| M1 | `Restore` 改走 CAP(`enforceCap: true`) | 145/146,1 红 | `test_restore_absentPatientAtFullCap_doesNotThrow_capBypassed` |
| M2 | 去重短路 `false &&` 化 | 145/146,1 红 | `test_restore_duplicateEvent_isRejected_dedupKept` |
| M3 | `GetNextPatientId` 忽略折叠行 | 144/146,2 红 | `test_restore_foldedRows_feedPatientHighWater` + 端到端 roundtrip |
| M4 | `WriteRow` diseaseId/patientId 字段序对调 | 141/146,5 红 | 黄金字节 ×2 + 组版折叠段 ×2 + 端到端 |
| M5 | outcome 合法域混入 0 | 145/146,1 红 | `test_foldrowcodec_outcomeZero_rejectedOnDecode` |
| M6 | `Compose` 折叠段恒 `count=0` | 143/146,3 红 | 组版写行 / existingFoldedRows 回写 / 端到端 |
| M7 | `SpliceBack` 保留被抹行(`if (true)`) | 144/146,2 红 | 折叠段写入测 + 端到端(被折行未抹 ⇒ 断言不等) |
| M8 | `Load` 不交出折叠段(`_foldedRowSink(foldedSegment);` 删除) | 145/146,1 红 | 端到端 roundtrip(折叠行零恢复) |

**还原终核**:四文件 SHA-256 逐字节 == 基准(M1–M8 每轮 restored=OK + 终核复跑 OK)。
**判别力判定**:八条主断言全部对变异敏感,无假绿夹具。**Falsifiability Gate = PASS**。

## 五、债动作逐条结清

| 动作(债条目原文) | 处置 | 证据 |
|---|---|---|
| ① 先裁抽象点归属 | 用户裁定:**不立新抽象点**,BCL 委托 + `EventStream.Restore` | ADR-010 §一 增补;CompositionRoot 双委托 |
| ② 重放旁路逐条判 | 三分化:CAP 绕(在场视图未重建必误抛)/ 去重+单调保留(流不变量)/ Seq 不重发+播种接续 | Restore doc;eventstream_restore_test 9 测 |
| ③ 三流字节布局(ADR 级二选一) | 用户裁定甲案:修 ADR-010 承认**单列表**;病例流/世界流进档 | ADR-010 §一 增补;`SpliceBack` else 分支 |
| ④ 读档锁置位点复核 | **复核完成,维持 `Verifying` 通过后**:锁置位在 `SaveService.Load` 的 `_ledger.OnLoaded(slot)`(`SaveService.cs:353`),位于 ParseBody + 双 sink(即真重放)全部成功之后;入口已锁检查(`:302`)挡重入 ⇒ **失败路径(校验和/版本/委托缺失/Restore 违例)不置锁**,重试 Load 不被自己的失败锁住 —— 与 Restore 替换语义自愈闭环。债条目担心的「读档中途失败永久锁槽」在现实现不成立(锁在重放**成功后**才落)。**原动作不需要移位**,判定 = 维持现状 |
| ⑤ 折叠段字节布局(动作 ③ 同洞) | 兑现:`FoldRowCodec` + `Compose` 折叠段 + `ParseBody` 复合读 + 回写义务 | 本件 §一/§二;字节半自此 NOT-RUN → 落地 |

**卡住的判据面**:`AC-7a-01` 的「三逻辑流完整重放 / 指定病人 outcome / id 高水位 == 直接重放所得」—— 本轮兑现其**可测地基**(真重放 + 高水位含折叠行 + 端到端 roundtrip 钉);**该 AC 的全量语义(掉落清单、跨版本、快照不一致分支)仍未转绿**(快照段本批不实现,`SnapshotOffset=0` 哨兵),不得读成 AC-7a-01 已满足。

## 六、遗留与边界(禁借绿)

- **升链(逐版本迁移)= 仍 NOT-RUN**:`SaveService.Load` 的 `< CurrentSaveVersion` 分支维持 fail-closed(已注明「真迁移归 TD-persist-03」—— 本条按用户裁定 ④「仍为 1」**不触发升链义务**,布局从未出货;该分支注释保留)。
- **W-4(高水位初值)未顺手修**:「从槽位 N 续玩」的 Ledger 高水位恢复仍是缺口(注记已在 ctor doc 指向本债;本批做的是流侧高水位,台账侧的槽位恢复未裁),**保持 open,随读档 UX 轮**。
- **`AC-7a-01` / `AC-7a-02` 的语义层载体缺口(TD-persist-03「相邻发现」)**:不在本条裁定面,仍待另立评审项。
- **save-slot-ui 接线(G-4 残余)**:`SlotProjections` 收敛归判据面,本批未触碰。
- **生产消费者仍为零**:`new SaveService` 生产侧 0 调用不变 —— 本批交付的是「读档链真的能用」,不是「游戏在读档」。

## 七、复算命令汇总

```bash
# 回归计数(两套件 XML)
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' <EditMode.xml>
# 证伪轮还原判定
for f in EventStream FoldRowCodec SaveComposer SaveService; do
  sha256sum unity/Assets/{Sim/$f,Gameplay.Boot/$f,Gameplay.Boot/$f,Sim.Codec/$f} 2>/dev/null
done
# 四文件基准哈希(备份在 job tmp fz-tdp03-20261024/)
sha256sum /home/gu/.claude/jobs/181888b1/tmp/fz-tdp03-20261024/*.cs
```
