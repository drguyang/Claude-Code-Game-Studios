// 48 的装配可见性(AC-48-17 零设门的配套)。
//
// 为什么需要 `InternalsVisibleTo` 而不是把这些类型做成 public:
//   规则五的要求是**做不到**,不是**没做**。`TutorialProjection` / `TutorialProgressView` /
//   `TutorialTrigger` 回答的正是「引导完成了吗 / 这一步做过了吗」—— 一旦 public,
//   任何玩法系统(病例生成 / 建造 / 出诊)都能据此设门,规则五立刻退化成口头承诺。
//
// 谁是合法消费者(仅此两处):
//   · `Gameplay.Boot` —— 装配层的链驱动 `TutorialChainDriver`(投影 → 边沿 → 调度 → 投递);
//   · `Sim.Contracts.Tests` —— EditMode 夹具(投影 / 触发引擎的判据)。
//     ⚠️ 给**测试**开 IVT 有先例(`Sim.Codec` → `Sim.Contracts.Tests`),且它不扩大**运行期**可达面。
//
// ⚠️ 新增第三处 IVT 之前先问一句:它是不是在为「按教学进度做事」开旁路?是 ⇒ 拒绝。

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Gameplay.Boot")]
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
