// ---------------------------------------------------------------------------
// AttackState  (placeholder from B2, filled in by B6)
//
// Stops, hits the player with TakeDamage(1), waits for a cooldown.
// For now it does nothing, so the state machine can be tested with keys 1-5.
// ---------------------------------------------------------------------------
public class AttackState : CreatureState
{
    public AttackState(CreatureController creature) : base(creature) { }

    public override string Name => "Attack";

    public override void Enter()
    {
        // B6
    }

    public override void Tick()
    {
        // B6
    }

    public override void Exit()
    {
        // B6
    }
}
