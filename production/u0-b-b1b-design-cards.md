# U0-b b1b · 待定型类型批 —— 逐支拍板卡

> **Status**: **已拍板 + 已落盘(2026-09-22 批 · 【超算】写,待【桌面】编译验收)**。
> 裁定的四项见下「裁定记录」。X-2(Amendment G)已就地回写 ADR-006(正文 + 头注计数 + Amendment A 加注)。
> **前置**:b1a ✅(13 文件零编译错误)/ b5 ✅(16 绿)/ b6 ✅(kindgen 34 支 A1–A5 全过)。
> **权威件**:ADR-005 · ADR-006 Amendment A / **G(本批新立)** · ADR-008 §三 · registry `entities.yaml` payload_schema(真源,ADR-024 §①)·
> ADR-001 §一之二 · ADR-018 §二/Key Interfaces · `clinic-machine.md` OQ-CP-4 · `case-system.md` 2026-09-17 类型订正。

## 裁定记录(2026-09-22 批,用户 widget 裁定)

| 点 | 裁定 | 落点 |
|---|---|---|
| 支 0 EventPayload 联合形态 | **甲 · header/blob 分家** | `SimEvent.Payload` = `PayloadRef`;34 支 per-Kind struct = codec 解码形;X-2 → **Amendment G-2** |
| 支 1-a「Fix raw」字段 | **用 `Fix`**(long 是落盘形状) | magnitude / drug_potency / EnvMod / EquipMod |
| 支 1-b 枚举类字段 | **`int` + 注释标 ordinal 来源**(contracts 不造枚举,防第二真源) | 全族 |
| 支 2-a VitalsDto signs[] | **甲 · mask + count 全整数** | `SignChannelMask` + `SignCount`,`SignChannel` 位常量住 contracts(= 支 2-b 一并取建议) |

## 起草方自取的默认值(未单开拍板 · 均可推翻,推翻 = 单点改)

- **`AudioCueHandle`**:ADR-018 :447 用了这类型但**全库零定义**(幽灵类型,同 `WorldPosLatest` 先例)
  ⇒ 落 = `readonly struct { int Id }`;`EndLoop(AudioCueHandle)` 按 :447 字面;
- **`TierSource`**:`enum { Local }`(:449 字面 `SetTier(TierSource)`,§五 D-A 裁定唯一值);
- **`PosFlags` 位序**:ADR-001 只点名三旗未给序 ⇒ 位序由本实现登记(线上契约,改序 = 破协议);
- **`WorldPosLatest` 用 `Int3`**、**`AudioCueDto.Cell` 用 `Int3`**、**`Source` 用 `int`**(ADR-018 修订②字面)—— X-1 换入兑现;
- **`ClinicEnvDto.ContextIds` 用 `int[]`**(支 4-a:情境词表 size 全库未定 ⇒ 不造 bitmask;
  本 DTO 非流事件,数组不触支 0 纪律);**`GetEnv(int roomId)`**(4-c:registry 未点名,24 房号 = 整数,ROOM_NONE=0 合法);
- **registry-vs-抄本 三处冲突一律以 registry 为准**(ADR-024 §②):`PlayerId` 幽灵类型 → `int AuthorPlayerId`;
  `Judgment` 子 struct → sim 消费面只落 `LexiconId`+`Confidence`(`freehand_text` 住呈现装配,承其「永不进判定」例外纪律);
  `PatternRecognized.member_set` ADR-008 抄本 `CaseId[]` vs registry「冻结三元组」→ 落三元组展开
  (`AnchorCase` + `MemberDiseaseSet` + `Version`)。**注:member_set 的两种读法语义不同(成员例集合 vs 单例锚点),
  若 37 侧验收判据要求前者,此处须另裁 —— 已在 CasePayloads.cs 头注自白。**

---

## 支 0(先行裁定 · 决定支 1 全族形状)· `EventPayload` 联合形态

**问题**:`SimEvent.Payload` 字段须承载 34 支异构载荷。registry 实测分类:

- **23 支可机械转录**为定长整数 struct(i32/i64/枚举/`格 WorldPos`/哨兵缺省);
- **11 支含破格式构件**:
  - `DiseaseIdSet`(版本化整数 ordinal 集合)×3 —— CaseOpened / CaseClosed / PatternRecognized
    (2026-09-17 裁定 `FixSet`→`DiseaseIdSet`,住 9 的注册表,append-only);
  - 内嵌子 struct `Judgment` + **`string freehand_text`**(全案唯一非定长例外,ADR-006 Amendment A 已登记)×2;
  - `CaseId` 三元组(Tick, Patient, Seq)×5 —— 可转录为 struct 三字段,不算硬破;
  - **数组**:`actor_ids[]`(EncounterStarted)· `edge_ticks[]`(EmergencyAttempt)·
    Craft 四数组(`input_instance_ids[]/ActualConsumed[]/OutputQty[]/OutputQuality[]/output_instance_ids[]` —— 实为 5 个)。

**门 A 约束下不可行的方案**:C# `fixed` 内联数组(禁 unsafe)、`object`/接口装箱(引用字段 + 版本脆弱)。

### 选项

**甲 · 头/载荷分家(推荐)**
`SimEvent` 降为纯 header `{Tick, Patient, Seq, Kind, PayloadRef}`;
`PayloadRef = readonly struct { int BlobId; int Offset; int Length }`(全整数);
载荷字节住流持有的不可变 blob 池。强类型访问 = `Sim.Codec` 按 registry schema 解码
(`TryGetPayload<T>(in SimEvent, out T)`),per-Kind struct 仍**逐支定义**(类型层保住「每 Kind 一个扁平 struct」)。
- ✅ 数组天然变长,零 padding 浪费;流膨胀可控;与 ADR-010 全二进制 codec / ADR-006 §五「按字段名编码」同构
  (blob 就是编码层落盘形状,**存档 = 传输 = 同一段字节**);无引用类型进 struct。
- ❌ 需要一处修正案:Amendment A「Payload 是值 struct 无引用字段」改读为「header 值类型 + 载荷经 codec 定序」
  (= **X-2 的落点**,见支 5);读载荷多一次解码(7a 反正必须解码,无净增成本)。

**乙 · 最大 inline(union 定长)**
`EventPayload` = 一个大 struct,容量 = max(34 支),数组须全数上界常量化
(`EDGE_TICKS_MAX` / `ACTORS_PER_ENCOUNTER_MAX` / `CRAFT_IO_MAX`…)。
- ✅ SimEvent 自包含、按位拷贝即复制。
- ❌ 每条事件按最大支付字节(Craft ≈ 5×N×8B ⇒ 数百 B),而流内大多数事件只用 16–32B ⇒ **史料膨胀一个量级**;
  三个新结构上界常量 = 新旋钮,且上界一旦突破 = 拒收(与有界性论证新增摩擦);违背 ADR-010 稀疏编码取向。

**丙 · 混装**(定长支 inline、破格支走 blob)
- ❌ 双形状 = `PayloadRef` 与 inline 并存,codec/探针/kindgen 三处都要判两条路。**不推荐**(复杂度买不到对应收益)。

> **拍板点 0-1**:甲 / 乙 / 丙?(推荐甲)
> **拍板点 0-2(若甲)**:`PayloadRef` 的 blob 生命周期归流实现(7a/45 侧),contracts 只定义
> `PayloadRef` struct + 34 支 payload struct + codec 访问签名。**freehand_text** 存进 blob
> (UTF-8 字节,**永不进判定**的纪律由 PresentationDtoGuard + 构建期扫描守,不变)。

---

## 支 1 · per-Kind payload struct 族(34 支)

形态 = 每 Kind 一支 `readonly struct`,`public readonly [Kind]Payload`,住
`unity/Assets/Sim.Contracts/Payloads/`(新目录,同 asmdef)。字段逐字从 registry `payload_schema` 转录:

- `i32`→`int` · `i64`→`long` · `格 WorldPos`→`WorldPos` · `Fix raw`→`long`(载荷内**不**用 `Fix` 包壳?——
  待定 **1-a**:建议**用 `Fix`**,构造即校验、意图显式;registry 写 `i64(Fix raw)` 是落盘形状不是内存形状)
- **`枚举`→`int`**(1-b 拍板点:显式 `int` 字段 + 注释标 ordinal 来源,还是各枚举具名?
  推荐 **int**——闭合枚举表散在 9/25/21a 的数据表里,contracts 造枚举 = 把 ordinal 表第二真源搬进契约层);
- 新支撑类型 3 支:
  - `CaseId { long Tick; PatientId Patient; long Seq }`(= 全序键三元组,ADR-008);
  - `DiseaseIdSet`(1-c:甲 · 内联 `int Count + int[]` ⇒ 引用字段,甲仅存于乙案下合法 /
    乙 · blob 内变长(承支 0 甲,推荐)/ 丙 · 固定 bitmask `ulong` ×N ⇒ 病种数上界即结构上界);
  - `Judgment { int LexiconId; byte Confidence; string FreehandText }` —— **引用字段例外**,
    ADR-006 已登记「不得扩张」;若支 0 取甲,FreehandText 只在 codec 解码后短暂存在,struct 本身仍走 blob。
- 哨兵缺省字段(如 SkillGrown.level「未升级 = 哨兵」)照 registry 用 `int` + 文档注释,不造 nullable。

> **拍板点 1**:1-a(Fix vs long)· 1-b(枚举 vs int)· 1-c(DiseaseIdSet 形态)。

---

## 支 2 · `VitalsDto` + `IVitalsQuery`

`IVitalsQuery { VitalsDto GetVitals(PatientId p); }` 从 Abstractions.cs 头注预告兑现,住同包。
**这是全案唯一 float 出口**(ADR-005:241),DTO 含 float 是裁决本体,不受「禁 float」面约束
(该禁令管的是**流与存档**;呈现 DTO 走 ADR-012 口径:逐位判据只覆盖整数域)。

```csharp
public readonly struct VitalsDto
{
    public readonly float Position;              // 0–1(AC-20「原始量」)
    public readonly float Trend;                 // 带符号
    public readonly int   SignChannelMask;       // 六通道按位或(AC-21 通道位契约,整数存储)
    public readonly int   SignCount;             // signs[] 词条计数(表现层据此选素材档)
}
```

> **2-a 拍板点 · `signs[]` 的 DTO 形状**:AC-20 要求「只读 `signs[]`」且 AC-21 定死
> 「通道位整数存储、构建期位冲突检查」。
> 甲(上表,**推荐**)= mask + count 全整数,词条→素材的解析住在 13/8 的烘焙表,DTO 零数组零引用;
> 乙 = `int[] SignIds` 原样携带 ⇒ 呈现层自己查通道 ⇒ AC-21 的「通道位唯一」断言失去 DTO 侧落点。
> **2-b**:`SignChannelMask` 的位定义常量(六通道枚举位)归 9 的数据表还是 contracts 常量类?
> 建议 contracts 放 `SignChannel` 位常量(位序即契约,9 的表只填词)。

字段白名单断言(AC-20 BLOCKING):b4 家族加一条 —— `VitalsDto` 反射扫描,字段 ∈ {Position, Trend, SignChannelMask, SignCount},
含任何 `disease_id`/派生显示量 = 构建失败。

---

## 支 3 · X-1 换入批:`AudioCueDto`+`IAudioCueSink` · `WorldPosLatest`+`IPositionalChannel`

**共同点**:ADR 原文写 `int3`(Unity.Mathematics,引擎类型)—— b1a 已裁定自建 `Int3` 消解,本批兑现换入。

### 3.1 AudioCueDto + IAudioCueSink(ADR-018 §二 + §架构 :444)

```csharp
public readonly struct AudioCueDto
{
    public readonly int  Cue;        // 语义 cue id(表 ordinal;3-a:不造 CueId struct,理由同 1-b)
    public readonly byte Intensity;  // 0–255 归一
    public readonly Int3 Cell;       // ← X-1 换入(原文 int3)
    public readonly int  Source;     // 声源实体 id;None = -1(环境音)。3-b:用 int 不用 PatientId ——
                                     // ADR-018 修订②明写「契约不引用 sim 程序集类型」?——实际 PatientId
                                     // 就在 Sim.Contracts 内,该理由已半失效;**建议仍用 int**(与 ADR-018 字面一致)
    public readonly bool Looped;
}
public interface IAudioCueSink
{
    void Emit(in AudioCueDto cue);
    void BeginLoop(in AudioCueDto cue);
    void EndLoop(int cueHandle);      // 3-c:EndLoop 的参数 —— 原文只写名字未写签名;
                                      // 推荐句柄(int)而非整 DTO(回传 DTO 会被误读为可改参)
    void SetTier(int tier);           // 设备级,非逐 cue(2026-09-18 修订①)
}
```

**涟漪**:`docs/registry/architecture.yaml:392` signal_signature 是 2026-09-18 修订**前**的旧抄本
(带 `byte Tier` / `WorldPos Cell` / `PatientId Source`)⇒ 本批顺带回写成员清单口径(V-5 同族义务)。

### 3.2 WorldPosLatest + IPositionalChannel(ADR-001 §一之二)

```csharp
public readonly struct WorldPosLatest
{
    public readonly int  ActorId;    // 与 IIdAuthority 同空间
    public readonly Int3 Cell;       // ← X-1 换入
    public readonly uint ServerTick;
    public readonly byte Flags;      // IsMoving / IsDowned / OcclusionHint —— 位常量登记点:
                                     // 3-d:位定义放 contracts(WorldPosFlags 常量类)还是散在 20/45?
                                     // 推荐 contracts(跨发布者共享的线上契约)
}
public interface IPositionalChannel
{
    void PublishLatest(in WorldPosLatest p);
    bool TryReadLatest(int actorId, out WorldPosLatest p);
    void SubscribeActor(int actorId, Action<WorldPosLatest> onUpdate);  // System 命名空间,BCL 合法
}
```

> 45 GDD 轮另有「铸造契约」残留(ADR-006 注记),不属本批;本批只落类型。

---

## 支 4 · `ClinicEnvDto` + `IClinicEnvQuery`(OQ-CP-4 裁定甲,2026-09-22)

裁定文本已点名全部字段,机械转录:

```csharp
public readonly struct ClinicEnvDto
{
    public readonly int RoomNameId;   // 词表索引
    public readonly int ContextMask;  // 4-a:contexts[] 形状 —— 情境词表若为闭集小表 ⇒ 位掩码全整数
                                      // (与支 2 的 SignChannelMask 同构,推荐);
                                      // 若语境 id 空间大 ⇒ 退回「Count + blob 变长」(承支 0 甲)
    public readonly int EquipModRaw;  // Q16.16 raw(4-b:Fix vs long,随 1-a 同批裁)
    // envMod「未钳制房间分量」是 24 侧语义,contracts 只载值
}
public interface IClinicEnvQuery
{
    ClinicEnvDto GetEnv(int roomId);  // 4-c:room 参数 —— registry 写 GetEnv(room),room 类型
                                      // 未点名;24 的房间住整数格/房间表 ⇒ 推荐 int(房号),待确认
}
```

「零 `disease_id`、零 float 字段」= 裁定原文 ⇒ 白名单断言同支 2 家族。

---

## 支 5 · X-2 修正案回写 ADR-006

随支 0 裁定形态落,内容 = 三道(视 0-1 结果取用):

1. **SimEvent 字段序单一抄本裁定**:`{Tick, Patient, Seq, Kind, Payload}` 为唯一权威抄本
   (ADR-007 旧抄本 `{Tick, Patient, Kind, Seq, Payload}` 作废声明 —— ADR-006 §五 :221 已预警两抄本
   写出两种字节流的静默失败;`SimEvent.cs` b1a 实现**已按 Amendment A 序**,本条只补权威字面);
2. **Payload 形状定稿**(若支 0 裁甲):Amendment A「Payload 是值 struct 无引用字段」增补
   「载荷经 header + blob/codec 承载」的新形态说明;
3. **freehand_text 例外在 blob 形态下的重述**:例外从「struct 字段」平移为「blob 内变长段」,
   「全部消费者是呈现层」纪律不变、守门机制不变。

落点 = `docs/architecture/adr-006-fixed-point-boundary-contract.md` Amendment 节新增
**Amendment G**(F 是最后一条;⚠️ 承 ADR-024 §③「Amendment 追加通道退役」—— 该纪律管的是
**Kind 登记**,不是 ADR-006 自身的修正案,故本条合法;头注自白一句)。

---

## 支 6 · 落盘动作序(**【超算】侧已全做**,2026-09-22)

```
【超算】✅
 1. 落 Payloads/(PayloadCommon + HistoryPayloads 13 + CasePayloads 5 + WorldPayloads 16 = 34 支
    + PayloadRef/CaseId/DiseaseIdSet)+ VitalsDto.cs(+IVitalsQuery+SignChannel)
    + PresentationDtos.cs(AudioCueDto/IAudioCueSink/AudioCueHandle/TierSource/WorldPosLatest/PosFlags/IPositionalChannel)
    + ClinicEnvDto.cs —— SimEvent.cs 改写(PayloadRef)+ Abstractions.cs / Fix.cs 头注更新
 2. X-2 = **Amendment G** 回写 adr-006(头注计数 A–F→A–G + Ordering Note + Amendment A 就地加注 + G 正文三条)
 3. 回写轮三件已做:architecture.yaml:392 signal_signature(旧抄本订正 + 注)/ adr-005 :252 前向指针注
 4. kindgen 重跑:A1–A5 全过,产物零 diff(幂等回归 —— EventKind 未动,预期内)
 5. 括号平衡自查 + 全仓 grep「EventPayload / new SimEvent / payload struct 构造」零消费者残留
【桌面】待做
 6. pull → Unity 生成新 .meta → Console 零编译错误(b1b 唯一硬判据,同 b1a)
    → 菜单 DaYi/Validation/Run Assembly Gates 三门全过 → EditMode 仍 16 绿 → 回传 meta
```

**验收判据**:编译零错误 + 三门不破 + 16 绿基线未破。SimEvent 形状虽动(Payload 字段换型),
但全仓**零构造消费者**(b2–b6 的断言面只读 asmdef / Kind 枚举,不触 SimEvent 字段),预期零返修。

## 残留登记(出本批)

| # | 残留 | 归 |
|---|---|---|
| R-1 | **Sim.Codec 解码器本体**(34 schema 的字节↔struct 编解码 + PayloadRef 的 blob 池契约 + freehand_text 变长段)—— b5 的 8 字节小端 helper 迁移亦在此 | codec 轮(ADR-006 §五 / ADR-010) |
| R-2 | **b4 白名单断言扩员**:VitalsDto / ClinicEnvDto / AudioCueDto 字段反射扫描(AC-20 [L] BLOCKING / AC-37-15 家族)—— 类型现已齐,断言可写 | U1 gates 批 |
| R-3 | **member_set 双语义**:registry「冻结三元组」vs ADR-008 抄本 `CaseId[]`(模式识别的**成员例集合**语义在 37 侧更自然)—— 本批按 registry 落三元组展开,若 37 验收要集合形须另裁 | 37 / ADR-008 修订轮 |
| R-4 | adr-008 §三 C# 抄本(`PlayerId` 幽灵 + `CaseId[]`)降级注记(承 ADR-024 §② 同款) | 回写轮 |
| R-5 | 45 的 `WorldPosLatest` 铸造契约(第二 QoS 上行格式,ADR-006 注记残留) | 45 GDD 轮(不变) |
