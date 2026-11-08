// ---------------------------------------------------------------------------
// StunnedState  (placeholder from B2, filled in by B7)
//
// Stops moving for the stun time, then goes to Investigate.
// For now it does nothing, so the state machine can be tested with keys 1-5.
// ---------------------------------------------------------------------------
public class StunnedState : CreatureState
{
    public StunnedState(CreatureController creature) : base(creature) { }

    public override string Name => "Stunned";

    public override void Enter()
    {
        // B7
    }

    public override void Tick()
    {
        // B7
    }

    public override void Exit()
    {
        // B7
    }
}
