// PlayerStateMachineFactory.cs

using SiYangFSM;
using UnityEngine;

public static class PlayerStateMachineFactory
{
    public static StateMachine<EnumCodeInput> CreatePlayerStateMachine()
    {
        var ctx = new PlayerContext();

        // 1. 子状态机：移动
        var locomotion = BuildLocomotionStateMachine(ctx);

        // 2. 死亡状态
        var deadState = new SimpleState<EnumCodeInput>(
            EnumCodeInput.Dead,
            onEnter: () =>
            {
                Debug.Log("Enter Dead");
                ctx.Speed = 0f;
            });

        // 3. 顶层状态机
        var root = new StateMachine<EnumCodeInput>(EnumCodeInput.PlayerRoot, ctx);

        root.AddState(locomotion, setAsDefault: true);
        root.AddState(deadState);

        // AnyState -> Dead
        root.AddTransition(
            from: null,
            to: deadState,
            condition: () => ctx.IsDead,
            priority: 10);

        // Dead -> Locomotion（复活）
        root.AddTransition(
            from: deadState,
            to: locomotion,
            condition: () => !ctx.IsDead);

        root.OnStateChanged += (prev, cur) =>
        {
            Debug.Log($"[Root] {prev?.StateKey.ToString() ?? "None"} -> {cur?.StateKey.ToString()}");
        };

        return root;
    }

    private static StateMachine<EnumCodeInput> BuildLocomotionStateMachine(PlayerContext ctx)
    {
        var sm = new StateMachine<EnumCodeInput>(EnumCodeInput.Locomotion, ctx);

        var idle = new SimpleState<EnumCodeInput>(
            EnumCodeInput.Idle,
            onEnter: () =>
            {
                Debug.Log("Enter Idle");
                ctx.Speed = 0f;
            });

        var walk = new SimpleState<EnumCodeInput>(
            EnumCodeInput.Walk,
            onEnter: () =>
            {
                Debug.Log("Enter Walk");
                ctx.Speed = 2f;
            });

        var run = new SimpleState<EnumCodeInput>(
            EnumCodeInput.Run,
            onEnter: () =>
            {
                Debug.Log("Enter Run");
                ctx.Speed = 5f;
            });

        sm.AddState(idle, setAsDefault: true);
        sm.AddState(walk);
        sm.AddState(run);

        // ===== 子状态机内部转换，全部用 ctx =====

        // Idle -> Walk : 有输入 & 不冲刺
        sm.AddTransition(
            from: idle,
            to: walk,
            condition: () =>
                ctx.MoveInput.sqrMagnitude > 0.01f &&
                !ctx.IsSprinting);

        // Idle -> Run : 有输入 & 冲刺
        sm.AddTransition(
            from: idle,
            to: run,
            condition: () =>
                ctx.MoveInput.sqrMagnitude > 0.01f &&
                ctx.IsSprinting);

        // Walk -> Idle : 无输入
        sm.AddTransition(
            from: walk,
            to: idle,
            condition: () => ctx.MoveInput.sqrMagnitude <= 0.01f);

        // Run -> Idle : 无输入
        sm.AddTransition(
            from: run,
            to: idle,
            condition: () => ctx.MoveInput.sqrMagnitude <= 0.01f);

        // Walk -> Run : 有输入 & 冲刺
        sm.AddTransition(
            from: walk,
            to: run,
            condition: () =>
                ctx.IsSprinting &&
                ctx.MoveInput.sqrMagnitude > 0.01f);

        // Run -> Walk : 有输入 & 不冲刺
        sm.AddTransition(
            from: run,
            to: walk,
            condition: () =>
                !ctx.IsSprinting &&
                ctx.MoveInput.sqrMagnitude > 0.01f);

        sm.OnStateChanged += (prev, cur) =>
        {
            Debug.Log($"[Locomotion] {prev?.StateKey.ToString() ?? "None"} -> {cur?.StateKey.ToString()}");
        };

        return sm;
    }
}
