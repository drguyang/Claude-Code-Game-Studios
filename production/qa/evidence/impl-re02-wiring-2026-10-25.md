# 实现轮证据件 —— 52 出诊启动生产接线轮(接 TD-randomevents-02 二十九订 ⚠ 接线义务①)

**日期**:2026-10-25 · **轮次**:sprint-19 · **commit**(回归时点):`6971d97` 工作树
**三裁(2026-10-25 用户全取推荐)**:① 范围 = 仅出诊启动一节 ② 医馆格源 = 空集占位 ③ 急召 key 集 = 生产空集
**权威件**:GDD random-events.md 规则十二 · `design/registry/entities.yaml` HouseCallStarted ·
`production/qa/evidence/impl-td-randomevents-02-2026-10-25.md`(写者轮)· 债册二十九订块

## 一、接线落点(义务①结清)

| # | 件 | 落点 | 判据 |
|---|---|---|---|
| 1 | 52 主循环壳 | `unity/Assets/Gameplay.Boot/RandomEventsDirectorDriver.cs`(晚绑定 `BindPlayer` 一次;每边沿调 `TryStartAtTick`;其余 52 节具名槽入头注) | `grep -c "class RandomEventsDirectorDriver" … = 1` |
| 2 | 医馆格占位源(裁定②) | `unity/Assets/Gameplay.Boot/ClinicCellSource.cs`(空集 fail-safe;`Configure` = 23/24 接线缝) | `grep -c "IsClinicCell" … ≥ 2` |
| 3 | 服务袋发放 | `CompositionRootServices.cs` 属性 `RandomEventsDirector` / `ClinicCells`(可选尾参) | `grep -n "RandomEventsDirector { get; }" CompositionRootServices.cs` |
| 4 | 组合根构造(同源单点发放) | `CompositionRoot.cs`(构造段注释载三裁;服务袋尾参传递 —— 修过一次位置实参错位:`currentConfigVersion` 是位置实参,新参须在其后) | `grep -n "randomEventsDirector, clinicCells" CompositionRoot.cs` |
| 5 | 边沿接线 | `BootRoot.cs`:玩家铸造后 `BindPlayer`;边沿块插在急救通道之后、**锚点触发器 Drain 之前**(插后 = 锚点观察晚一拍) | `grep -n "RandomEventsDirector?.OnTickEdge" BootRoot.cs` |
| 6 | 夹具 | `Tests/EditMode/Boot/random_events_director_wiring_test.cs`(8 条)+ `composition_root_test.cs` 装配同源钉 1 条 | 见 §二 |

**「通电待信」态(如实登记,禁借绿)**:双重空集(裁定②③)⇒ 生产 `HouseCallStarted` 恒零写入 ——
出诊链每 tick 真跑,37 回填 key 集 + 23/24 回填地块集后自动激活,零代码改动。playtest
「未见出诊触发」不是缺陷。每边沿 O(流长) 全扫的复杂度登记见驱动类头注(游标优化归性能轮)。

## 二、回归(基线三元组齐备)

| 项 | 值 |
|---|---|
| **本轮全量回归** | **`5028/4993/0/35`**(EditMode total/passed/failed/skipped) |
| **基线** | `5019/4984/0/35` @ `2026-10-25T11:08 UTC` · TD-randomevents-02 实现轮 · `9edb75c` 工作树(现 `6971d97` 已含该轮内容) |
| **本轮三元组** | `2026-10-25T11:33 UTC` · 52 接线轮(全量第 2 跑,夹具修正后)· commit `6971d97` 工作树 |
| **增量等式** | 5028 − 5019 = **+9** = 新夹具数(8 wiring + 1 composition)· 4993 − 4984 = **+9** · failed **0**(零回归) |
| **首跑红 2 条(已修)** | 正路径两夹具漏 `Configure` 医馆格(占位源空集拒之)—— 恰是 WC 变异所钉行为的实例;修夹具非修产品码 |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/re02wiring-editmode2.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

## 三、证伪轮(S12-4 · 三条守卫变异 · 同命令改→跑→还原 · SHA-256 逐字节还原全过)

| # | 变异(停用什么) | 期望红 | 实测 |
|---|---|---|---|
| WA | 驱动未绑定 no-op 守卫(`if (_writer == null) return;` → `if (false)`) | `test_director_unbound_onTickEdge_noop` | ✅ 1 红(7/8) |
| WB | `BindPlayer` 重复绑定抛(`if (_writer != null)` → `if (false && …)`) | `test_director_bindPlayer_guards` | ✅ 1 红(7/8) |
| WC | 占位源空集语义(`=> _cells.Contains(cell);` → `=> true;`) | `test_clinicCellSource_defaultEmpty_configureFeeds` | ✅ 3 红(+正路径两夹具:全格恒 true ⇒ 跨出判定恒不满足,同守卫下游) |

三轮均:锚串恰命中 1 次(脚本断言)· SHA-256 逐字节还原 · `grep "=> true;\|if (false"` 两文件零残留。
过滤器 = `DaYiJingCheng.Tests.Boot.RandomEventsDirectorWiringTest`(重跑该 fixture 口径)。

## 四、安全属性 → 测试映射

| 属性 | 测试 | 状态 |
|---|---|---|
| 双空集 fail-safe(生产恒零写入) | `productionShape_emptyKeys_zeroWrites` / `emptyClinicCells_notCrossingOut_zeroWrites`(WA/WC 反证钉) | ✅ RUN |
| 晚绑定一次(未绑定 no-op / 重复绑定抛) | `unbound_onTickEdge_noop` / `bindPlayer_guards`(WA/WB 反证钉) | ✅ RUN |
| 正路径(driver→writer→流) | `crossingWithActiveTarget_appliesOnce` / `sameTickEdge_drivenOnce` | ✅ RUN |
| 同源单点发放(装配级) | `composition_root_test.test_assemble_randomEventsDirector_clinicCells_sameInstance`(`AreSame`) | ✅ RUN |

## 五、未跑 / 未声称(禁借绿)

- 边沿序列的**运行时**行为(BootRoot Update 块)未做 PlayMode 实测 —— EditMode 钉的是组件语义与装配形状;真启动序走查归 PlayMode/手测轮。
- 23/24 回填真地块集、37 回填出诊 key 集后的**激活面**未测(数据未存在)。
- 其余 52 节(延迟威胁 / 遭遇 / 急召生成链)零接线 —— 具名槽见驱动类头注,各归其轮。
- `F1PipelineInput` 生产构造方不存在;同源钉今日只有单消费点,另两点出现时断言面扩列(组合根测试注释已载)。
