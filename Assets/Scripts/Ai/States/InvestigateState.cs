// ---------------------------------------------------------------------------
// InvestigateState  (placeholder from B2, filled in by B5)
//
// Walks to a noise or the player's last seen position, looks around, then returns to Patrol.
// For now it does nothing, so the state machine can be tested with keys 1-5.
// ---------------------------------------------------------------------------
public class InvestigateState : CreatureState
{
    public InvestigateState(CreatureController creature) : base(creature) { }

    public override string Name => "Investigate";

    public override void Enter()
    {
        // B5
    }

    public override void Tick()
    {
        // B5
    }

    public override void Exit()
    {
        // B5
    }
}
