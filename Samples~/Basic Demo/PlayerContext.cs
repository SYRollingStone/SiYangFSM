// PlayerContext.cs
using UnityEngine;

/// <summary>
/// 玩家状态机共享的数据（黑板 / 上下文）。
/// </summary>
public class PlayerContext
{
    public Vector2 MoveInput;   // 移动输入
    public bool IsSprinting;    // 是否在按“冲刺”（Shift 等）
    public bool IsDead;         // 是否死亡
    public float Speed;         // 当前移动速度（给动画 / 移动逻辑用）
}