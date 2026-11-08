// ---------------------------------------------------------------------------
// ChaseState  (placeholder from B2, filled in by B4)
//
// Follows the player at chase speed while it can see them.
// For now it does nothing, so the state machine can be tested with keys 1-5.
// ---------------------------------------------------------------------------
public class ChaseState : CreatureState
{
    public ChaseState(CreatureController creature) : base(creature) { }

    public override string Name => "Chase";

    public override void Enter()
    {
        // B4
    }

    public override void Tick()
    {
        // B4
    }

    public override void Exit()
    {
        // B4
    }
}
