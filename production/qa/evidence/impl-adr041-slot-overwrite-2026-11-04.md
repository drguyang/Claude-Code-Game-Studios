# 实现原件 — ADR-041 存档槽覆盖语义(2026-11-04)

> **对象**:`SaveSlotLedger` / `SaveService` / `SaveSlotProbe` / `SlotProjections` /
> `SaveSlotView` / `SaveSlotItem` / `SaveSlotsScreen` / `ISaveSlotCommandSink` /
> `SaveSlotCommandRouter` / `BootRoot`
> **权威**:`docs/architecture/adr-041-save-slot-overwrite.md`(Accepted 2026-11-04)
> **用户裁定**:「点一下空档就是保存,点一下已有存档会提示读档还是覆盖存档」+ 三个细项全取建议
> (行内两枚墨字 / 不设槽位上限 / `.bak` 读侧可回退)。

---

## 1. 变更面

### 1.1 代码

| 文件 | 变更 |
|---|---|
| `Sim.Codec/SaveSlotLedger.cs` | **删门①**(覆盖 ⇒ 抛);`FallsBackToBak` 拆为 `ProbeCountsBak`(占用态,手动槽 false)与 `ReadFallsBackToBak`(读档回退,恒 true);`OnCheckpointCommitted` 加「**不得触碰 `_locked`**」不变式注 |
| `Sim.Codec/SaveService.cs` | 占用态用 `ProbeCountsBak`;`LoadBytes` 用 `ReadFallsBackToBak`,且「final 缺失 + 只剩 bak」对手动槽仍判**空槽**(与探测同向) |
| `Sim.Codec/SaveSlotProbe.cs` | 占用态改用 `ProbeCountsBak` |
| `Sim.Codec/SlotProjections.cs` | `Writable` 去掉 `¬Locked` 与 `slot_seq ≥ 高水位` 两条子句 ⇒ `Writable ⟺ InNarrativeContext` |
| `Gameplay.UI/Skeuomorphic/SaveSlotView.cs` | 新增 `IsLocked`(尾参,向后兼容) |
| `Gameplay.UI/Skeuomorphic/SaveSlotItem.cs` | 行内二段态:`EnterChoice` / `ExitChoice` / `ActivateOpen` / `ActivateRewrite` + 两枚可焦点墨字 |
| `Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs` | `Activate` 三路分流(未绑定 ⇒ 载入 / 空槽 ⇒ 落笔 / 有档 ⇒ 二段态);已锁行「启封」灰置并退出焦点序;重画清二段态 |
| `Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs` | 接口加 `TrySaveAt(uint)` |
| `Gameplay.Boot/SaveSlotCommandRouter.cs` | `TrySave` / `TrySaveAt` 共用一个执行体;新增 `saveFailureSink`(可 null ⇒ 回落) |
| `Gameplay.Boot/BootRoot.cs` | 新增 `LogSaveFailure`(报文 `[7b][落笔]` + 门名);`BuildSaveSlotViews` 填 `isLocked` |

### 1.2 夹具

- **新增** `adr041_slot_overwrite_test`(6 条:覆盖合法 / 槽号 0 仍拒 / 覆写不清锁 /
  `.bak` = 上一版 / 叙事上下文门 / 高水位不倒退)
- **新增** `adr041_save_slot_choices_test`(6 条:空槽落笔 / 二段态零动作 / 重写 / 已锁灰置 /
  重画作废 / 留档仍追加)
- **改写** `manual_slot_bak_scope_test`(核心判据**反转**:手动槽 final 坏 ⇒ 回退上一版;
  `final` 缺失 + 只剩 bak ⇒ 仍空槽;两条谓词必须分叉)
- **改写** `save_slot_contract_test`(覆盖合法 + 闭合回路改断在锁上 + 投影真值表重算)
- **改写** `save_slot_lock_release_test`(跨会话显式覆写 ⇒ 允许且留 `.bak`)
- 四个假 sink 补 `TrySaveAt`

### 1.3 文档

`save-slot-ui.md`(规则二改判 / 边界表 / `AC-7b-08` 改写)· `persistence-service.md`
(`AC-7a-19 (b)` + 二次加注)· `save-slots-7b.md`(交互表存盘行 + 新增覆写行)· `adr-010 §四`(两段改判注)·
`adr-041`(实现轮 + §Validation)。

---

## 2. 判据与回归

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6873/6836/0/37` | 截至 `2026-11-04T13:41:27Z` · sprint-24 ADR-041 轮 · `ceac3539` + 工作树 |
| PlayMode | `339/335/0/2/2` | 截至 `2026-11-04T13:42:28Z` · 同批次 |
| 对照基线 | EditMode `6861/6824/0/37` · PlayMode `339/335/0/2/2` | `引用自 production/qa/evidence/impl-main-menu-fixed-rows-2026-11-04.md` @ `13:15:17Z` |

**差值**:EditMode **+12/+12**,恰为新增两件夹具之和(6 + 6);PlayMode 逐位相同 ⇒ **零回归**。

---

## 3. 实跑修正(两处都是我的问题,如实登记)

1. **编译错**:`System.InvalidDataException` 不存在 —— 它是 `System.IO.InvalidDataException`
   (`BootRoot.LogSaveFailure`)。首轮 EditMode 因此零结果(无 XML)。
2. **4 条既有夹具按旧判据转红**:`save_slot_contract_test` 三条(覆盖被拒 / 投影真值表 /
   独立灰置通道)与 `save_slot_lock_release_test` 一条(跨会话覆盖被拒)。
   **处置 = 换判据,不是放宽断言**:
   - 闭合回路仍闭合,断点从「槽号单调」移到「**读档即锁**」(新增环 4);
   - 投影真值表按新公式逐格重算(Writable ⟺ inCtx;Loadable ⟺ ¬locked ∧ inCtx);
   - 跨会话一条改为「显式覆写允许 **且** 旧版留进 `.bak`」。

---

## 4. 不得借绿

- **V-9 NOT-RUN**:`LogSaveFailure` 已与 `LogLoadFailure` 分家,但**没有日志捕获夹具**
  钉住报文措辞 —— 只主张「代码路径已分开」。
- **「覆写后 `.bak` 能救回」只在 EditMode 成立**:没有 PlayMode / 真机走查帧。
- **已锁行的呈现**(「启封」灰置)只有屏级夹具,未做视觉走查。
- `SaveSlotProbe` 对「final 在但坏」仍报 `Corrupt`(不读 bak)⇒ 该行呈**未绑定**,
  按下去走载入并**成功回退**。两处不矛盾(未绑定不声称任何事),但**未做走查确认观感**。
