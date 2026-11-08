// ---------------------------------------------------------------------------
// InvestigateState  (placeholder from B2, sight check added in B4, filled in by B5)
//
// B5: walks to a noise or the player's last seen position, looks around,
//     then returns to Patrol.
// For now: the creature stops where it lost the player (Chase was already
// heading there), and switches back to Chase if it sees the player again.
// To get it patrolling again before B5, press 1 or use "Debug: Force Patrol".
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
        // Transition: Investigate -> Chase when the creature sees the player again.
        if (creature.Senses.CanSeePlayer)
        {
            creature.Fsm.ChangeState(creature.Chase);
            return;
        }

        // B5: walk to the target, look around, return to Patrol.
    }

    public override void Exit()
    {
        // B5
    }
}
