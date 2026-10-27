// S20-8 · processing story-002 —— F-18.1 调用契约与四修正项供料
//
// 权威来源:
//   design/gdd/processing.md
//     · F-18.1(:416-434)调用形状:
//         (consumed[], produced[], outQuality) = F1_21a(recipe, L, InQ, EquipMod,
//                                                        EnvMod_climate, EnvMod_clinic)
//     · F-18.1 表(:423)`L` 由 **30** 供给,18 **传等级,不传 SkillMod 结果**
//     · F-18.1 表(:424)`InQ` 归 **20**,= min(各**被点名实例**的 quality)
//     · F-18.1 表(:425)`EquipMod` 归 **24**;两 EnvMod 分量(:426-427)**无域**
//     · F-18.1 表(:428)(求和+钳制)归 **21a F1 正文** —— **18 不相加不钳制**(R-18-C)
//     · F-18.1 表(:429)`consumed[]` / `produced[]` / `outQuality` —— **18 只搬运,不重算**
//     · 规则九(:243-245)`drug_profile` / `polarity` / `treatable_by` —— **18 不认识药**
//     · AC-18-01 / 02 / 03 / 04 / 06 / 14
//   ADR-014 §一 —— 18 供料、21a 定曲线的分工(数据管线单一出处纪律)
//   ADR-006 —— 定点边界;求和 + clamp 的**唯一落点**在 21a F1 正文(EnvMod_total)
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//   story production/epics/processing/story-002-f1-call-contract-and-mod-inputs.md
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件的**唯一职责**:把 18 的四类输入装配成 `RecipeSettlementRequest`,调 F1 **恰一次**,
// 把 F1 的出参**原样转发**。零新数学 —— 这是 F-18.1「形状,非求解」的字面含义。
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不做任何算术** —— 无加法、无 clamp、无 Ceil/Round、无定点乘除(AC-18-02)。
//    两 EnvMod 分量**原样透传**(AC-18-06):它们各自无域(D-21-31),求和与唯一钳制在
//    `RecipeSettlementSolver.EnvModTotal` 内。此处若「好心」clamp 一下,就制造了**第二个**
//    钳制落点 —— 正是 R-18-C 要禁的形态。
// ② **不传 `SkillMod` 结果,只传等级**(AC-18-03)。曲线归 21a;在 int 域预先除尽会压死曲线
//    (GDD :508-514:Level < SKILL_CAP 时 `0.3 × 30 / 60` 整数除法 = 0)。
// ③ **不读 `drug_profile` / `polarity` / `treatable_by`**(AC-18-04,规则九)。
//    本文件对这三个符号**零引用** —— 想读也读不到(不持任何药物档案句柄)。
// ④ **不加工出参** —— 不排序、不去重、不重建数组(AC-18-01②)。「好心」的排序同样是再加工:
//    它让 20 拿到的不是 F1 的返回值,而是「F1 的返回值经 18 之手改造后的东西」。
// ⑤ **不写事件流** —— 本类零 `IEventSink` / 零 `IPayloadEncoder` 依赖。落流归 Story 003。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么求解器是**注入的委托**而不是直接调静态方法
// ─────────────────────────────────────────────────────────────────────────────
// AC-18-01① 要断「F1 恰被调用 1 次」、② 要断「出参与返回值**同一引用**」、AC-18-03 要断
// 「`L` 入参与 `QueryLevel` 返回值逐位相同」。三条都是**对调用本身的观察**,而静态方法调用
// 在 C# 里不可拦截。
//
// ⇒ 求解器以 `RecipeSettlementFn` 委托注入(该签名已由 `RecipeSettlementSolver` 提供,
//   见 `RecipeSettlementEntryPoints.ProcessingSettlementEntry.Solver`)。生产侧传
//   `ProcessingSettlementEntry.Solver`(它指向唯一求解器的同一 `MethodInfo`),
//   夹具侧传记录层。**这不是为测试而设的缝** —— 它同时是 AC-21a-5「两入口收敛到同一
//   MethodInfo」得以成立的机制:18 与 19 各自注入自己的入口委托,指向同一个 `Solve`。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>F-18.1 的**供料参数包** —— 18 从四个源系统收齐的全部输入。
    /// <para><b>纯数据</b>:不含运行时对象引用(可重建性三源不变量 —— 全部输入 ∈
    /// {事件流, 版本化烘焙数据, 二者的纯函数})。</para>
    /// <para>⚠️ <b>刻意不含 `SkillLevel` 字段</b>:等级由本包持 `ActorId`、在
    /// <see cref="ProcessingSettlementInputs.Assemble"/> 内**当场查询**。若允许调用方把等级
    /// 算好塞进来,「传等级不传结果」(AC-18-03)就退化成一条无法证伪的纪律 ——
    /// 夹具看不出那个 int 是 `QueryLevel` 的返回值还是别处算出来的。</para>
    /// </summary>
    public readonly struct ProcessingInputs
    {
        /// <summary>被炮制的配方(提供 `Inputs` / `Outputs` 基数;18 不改写)。
        /// <para>⚠️ <b><see cref="Recipe"/> 是值类型,不存在「Recipe 为 null」</b> —— 缺料的
        /// 表现形态是 <c>default(Recipe)</c>,即 <c>Inputs</c> / <c>Outputs</c> 两个数组为
        /// <c>null</c>。fail-closed 判据据此写在 <see cref="ProcessingSettlementInputs.Assemble"/>
        /// 的**消费侧**(`readonly struct` 的构造器守卫对 <c>default(T)</c> 不生效 ——
        /// 它绕过构造器,承 S20-4 的 `default(struct)` 教训)。</para>
        /// </summary>
        public readonly Recipe Recipe;

        /// <summary>玩家 id —— 用于查炮制技能等级,与 `InventoryOf(player)` 同一号空间。</summary>
        public readonly int ActorId;

        /// <summary>输入品级 <c>InQ</c> ∈ [1, MAX_QUALITY]。**归 20 供给**
        /// (F-18.1 表 :424)—— 18 不自行从 `Recipe.Inputs` 行取 min,那是行级 min 的
        /// 静默失效形态(F-18.2 BL-9 补注①:20 规则六可跨栈凑量点名)。</summary>
        public readonly int InputQuality;

        /// <summary>设备修正 <c>EquipMod</c> —— **归 24**,18 只读、不合成数值。</summary>
        public readonly Fix EquipMod;

        /// <summary>环境修正分量甲 <c>EnvMod_climate</c> —— **归 5**,<b>未钳制、无域</b>
        /// (D-21-31)。取值时刻 = **点火 tick**(本包由调用方在点火 tick 组装一次)。</summary>
        public readonly Fix EnvModClimate;

        /// <summary>环境修正分量乙 <c>EnvMod_clinic</c> —— **归 24**,<b>未钳制、无域</b>
        /// (D-21-31)。不落 24、不落 18。</summary>
        public readonly Fix EnvModClinic;

        /// <summary>构造供料参数包。</summary>
        /// <param name="recipe">被炮制的配方(须带非空 <c>Inputs</c> / <c>Outputs</c>)。</param>
        /// <param name="actorId">玩家 id。</param>
        /// <param name="inputQuality">输入品级(20 供给;∈ [1, MAX_QUALITY])。</param>
        /// <param name="equipMod">设备修正(24 供给;只读)。</param>
        /// <param name="envModClimate">环境分量甲(5 供给;**未钳制**)。</param>
        /// <param name="envModClinic">环境分量乙(24 供给;**未钳制**)。</param>
        /// <example><c>new ProcessingInputs(recipe, 42, 3, FixParse.Parse("1/5"),
        /// FixParse.Parse("-3/5"), FixParse.Parse("1/2"))</c></example>
        public ProcessingInputs(
            Recipe recipe,
            int actorId,
            int inputQuality,
            Fix equipMod,
            Fix envModClimate,
            Fix envModClinic)
        {
            Recipe = recipe;
            ActorId = actorId;
            InputQuality = inputQuality;
            EquipMod = equipMod;
            EnvModClimate = envModClimate;
            EnvModClinic = envModClinic;
        }
    }

    /// <summary>F-18.1 的**供料与调用** —— 18 侧的唯一落点(AC-18-01③「第二求解实现 = 零」)。
    /// <para><b>零新数学</b>:本类不含任何结算算式,只做「查询等级 → 装配参数包 → 调 F1 恰一次
    /// → 原样转发出参」。</para>
    /// </summary>
    public static class ProcessingSettlementInputs
    {
        /// <summary>炮制技能 id —— <c>SkillId.炮制 = 7</c>(GDD 的 `CRAFT_SKILL` 符号名)。
        /// <para>⚠️ 取自 <see cref="SkillId"/> 而非字面量:技能号是 30 的所有物,
        /// 本系统不拥有该常量(与 <c>AdmissiblePredicate.CraftSkillId</c> 同源同纪律)。</para>
        /// </summary>
        public const int CraftSkillId = (int)SkillId.炮制;

        /// <summary><b>装载期防御</b>(AC-18-14 的 18 侧半边)—— 常量表违反
        /// <c>Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1</c> 时**硬失败**。
        /// <para>⚠️ <b>委托唯一判据,不重抄算术</b>(承 <c>RecipeValidationGates</c> 的单一实现纪律):
        /// 判据本体住 <c>RecipeSettlementConstantTableValidator.IsSelfConsistent</c>,
        /// 与 21a 的 AC-21a-3 / AC-21a-9 是**同一谓词的两面**。若此处重写一遍不等式,
        /// 就有了两处可各自漂移的定义 —— 而漂移方向恰是「门说没问题、运行期静默截断」。</para>
        /// <para>⚠️ 主执行归 21a 烘焙管线(OQ-18-2 已裁甲);本方法只断 18 的**防御半边** ——
        /// 即使烘焙侧漏了,18 装载时仍会拦下。</para>
        /// </summary>
        /// <param name="constants">待校验的常量表。</param>
        /// <exception cref="InvalidOperationException">不等式不成立(硬失败,非 <c>Debug.Assert</c>)。</exception>
        /// <example><c>ProcessingSettlementInputs.ValidateConstantTable(c)</c> —— 坏表必抛。</example>
        public static void ValidateConstantTable(in RecipeSettlementConstants constants)
            => RecipeSettlementConstantTableValidator.ThrowIfInconsistent(constants);

        /// <summary>把供料参数包装配成 F1 的入参 —— **本方法的全部产出**。
        /// <para>⚠️ <b>三个「零」必须同时成立</b>,缺一条就不是 F-18.1 的契约:</para>
        /// <list type="bullet">
        /// <item>零算术 —— `Outputs` / `Inputs` 数组**直接转发**(同一引用),不复制不排序;</item>
        /// <item>零钳制 —— 两 EnvMod 分量**逐位原样**(AC-18-06);</item>
        /// <item>零换算 —— `CraftSkillLevel` = 同一次 `QueryLevel` 的返回值(AC-18-03)。</item>
        /// </list>
        /// </summary>
        /// <param name="inputs">供料参数包。</param>
        /// <param name="skillLevels">30 的只读等级查询(仅只读面,拿不到成长入口)。</param>
        /// <returns>F1 的入参(纯数据)。</returns>
        /// <exception cref="ArgumentNullException">配方的 <c>Outputs</c> / <c>Inputs</c> 为
        /// <c>null</c>(含 <c>default(Recipe)</c>),或 <paramref name="skillLevels"/> 为 null。</exception>
        /// <remarks>
        /// <b>为什么 null 必须抛</b>(承 <c>AdmissiblePredicate.Judge</c> 2026-10-27 评审同款):
        /// 把缺失的配方 / 查询当成默认值继续跑,会让「没供料」与「供了个零」在结果上不可区分 ——
        /// 而 F1 是纯函数,零输入会**正常返回**一个看似合法的结算结果。静默失效。
        /// <para>⚠️ <b>判据落在两个数组上,不落在 <c>Recipe</c> 上</b> —— <see cref="Recipe"/> 是
        /// 值类型,<c>inputs.Recipe == null</c> **不成立**(它恒 <c>false</c>,且 <c>Recipe</c>
        /// 未重载 <c>==</c> 时连编译都不过)。缺配方的真实形态是 <c>default(Recipe)</c>:
        /// 字段全零 ⇒ <c>Inputs</c> / <c>Outputs</c> 为 <c>null</c>。</para>
        /// <para>⚠️ <b>两条守卫**不是**守同一个情形</b>(2026-10-27 评审 A-5 订正归因):
        /// <c>default(Recipe)</c> 由**第一条**拦下 —— 那时 <c>Outputs</c> 先抛,第二条**不可达**。
        /// 第二条守的是**另一个**形态:**只缺一半数组**的配方(有 <c>Inputs</c> 无 <c>Outputs</c>,
        /// 或反之)。两条各自承重,删任一条都会被对应用例抓到
        /// (夹具 <c>test_missingRecipeArraysOrQuery_failClosed</c> 分别构造这两种半缺配方)。</para>
        /// </remarks>
        public static RecipeSettlementRequest Assemble(
            in ProcessingInputs inputs, ISkillLevelQuery skillLevels)
        {
            if (inputs.Recipe.Outputs == null)
                throw new ArgumentNullException(nameof(inputs) + "." + nameof(Recipe.Outputs),
                    "配方的 Outputs 为 null(含 default(Recipe))—— 拒绝以空配方继续装配" +
                    "(那会让 F1 正常返回一个看似合法的结算)");
            if (inputs.Recipe.Inputs == null)
                throw new ArgumentNullException(nameof(inputs) + "." + nameof(Recipe.Inputs),
                    "配方的 Inputs 为 null(含 default(Recipe) 与「只有一半数组」的配方)" +
                    "—— 拒绝以空投入继续装配");
            if (skillLevels == null)
                throw new ArgumentNullException(nameof(skillLevels),
                    "未传入技能等级查询 —— 拒绝以零级继续装配(那是 AC-18-03 的静默失效)");

            // ⚠️ 等级在**此处**查询,且**原样**进入请求(AC-18-03)。
            //    不做任何换算、不乘任何系数 —— 曲线归 21a。
            int skillLevel = skillLevels.QueryLevel(inputs.ActorId, CraftSkillId);

            return new RecipeSettlementRequest(
                inputs.Recipe.Outputs,      // 直接转发(同一引用)—— AC-18-01②
                inputs.Recipe.Inputs,       // 直接转发(同一引用)—— AC-18-01②
                skillLevel,                 // 传等级不传结果 —— AC-18-03
                inputs.InputQuality,
                inputs.EquipMod,
                inputs.EnvModClimate,       // 原样透传,零钳制 —— AC-18-06
                inputs.EnvModClinic);       // 原样透传,零钳制 —— AC-18-06
        }

        /// <summary>装配 + 调 F1 **恰一次** + 原样转发出参(AC-18-01①②)。
        /// <para>返回的 <see cref="RecipeSettlementResult"/> 与 <paramref name="solver"/> 的返回值
        /// **逐位相同**,其内数组与 F1 返回的数组是**同一引用** —— 本方法不重建、不排序、不去重。</para>
        /// </summary>
        /// <param name="inputs">供料参数包。</param>
        /// <param name="constants">常量表(纯数据;值域校验归 <see cref="ValidateConstantTable"/>)。</param>
        /// <param name="skillLevels">30 的只读等级查询。</param>
        /// <param name="solver">F1 的唯一求解器(生产侧 = <c>ProcessingSettlementEntry.Solver</c>)。</param>
        /// <returns>F1 的出参,**未经任何加工**。</returns>
        /// <exception cref="ArgumentNullException">同 <see cref="Assemble"/>;另
        /// <paramref name="solver"/> 为 null 时抛。</exception>
        /// <remarks>
        /// <para><b>为什么求解器是参数</b>:见文件头「为什么求解器是注入的委托」。
        /// 生产调用点传 <c>ProcessingSettlementEntry.Solver</c> —— 它指向唯一求解器的
        /// 同一 `MethodInfo`(AC-21a-5),故「注入」不引入第二条求解路径。</para>
        /// <para><b>失败路径不调 F1</b>:准入不过时本方法**不该被调用**(准入是 Story 001 的
        /// <c>AdmissiblePredicate</c>)。调用方先判准入再调本方法 ⇒ 失败路径 F1 调用 0 次,
        /// 不会「白烧一炉」。本方法不重复判准入(那是第二实现)。</para>
        /// <para>⚠️ <b>调用方义务(硬)</b>:① 先判准入(<c>AdmissiblePredicate.Judge</c>),
        /// 不过则**根本不调本方法**;② 先调 <see cref="ValidateConstantTable"/> 校验常量表 ——
        /// 本方法**不**替调用方做这项校验(2026-10-27 评审 A-3:校验是独立公开方法,
        /// 调用方漏调则 F1 在高投入段静默 clamp)。</para>
        /// <para>⚠️ <b>本条 AC 的未覆盖半边(如实登记)</b>:上面两句「调用方**不会**这么做」
        /// 目前**无测试载体** —— 生产侧尚无调用点(story §Out of Scope 把接线推给 Story 003),
        /// 故「准入不过时不调 Settle」在当下是<b>NOT-RUN</b>,不是 PASS。
        /// 本方法自身可测的契约是「**我不判准入**」,由
        /// <c>test_ac1801_settleDoesNotRejudgeAdmission</c> 钉住。</para>
        /// </remarks>
        /// <example><c>ProcessingSettlementInputs.Settle(inputs, constants, levels,
        /// ProcessingSettlementEntry.Solver)</c></example>
        public static RecipeSettlementResult Settle(
            in ProcessingInputs inputs,
            in RecipeSettlementConstants constants,
            ISkillLevelQuery skillLevels,
            RecipeSettlementFn solver)
        {
            if (solver == null)
                throw new ArgumentNullException(nameof(solver),
                    "未传入求解器 —— 拒绝静默跳过结算(那会让调用方拿到 default 结果)");

            RecipeSettlementRequest request = Assemble(inputs, skillLevels);

            // ⚠️ 恰一次调用,返回值**直接**返回 —— 中间任何一步加工都破 AC-18-01②。
            return solver(request, constants);
        }
    }
}
