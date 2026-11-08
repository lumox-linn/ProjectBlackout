// ---------------------------------------------------------------------------
// CreatureState  (Task B2)
//
// Base class for all 5 creature states. It implements IState and gives every
// state a reference to the CreatureController, so a state can reach the
// NavMeshAgent, the senses, the other states, etc. through "creature".
//
// Enter/Tick/Exit are "virtual" and empty here, so each state only overrides
// the ones it actually needs.
// ---------------------------------------------------------------------------
public abstract class CreatureState : IState
{
    protected readonly CreatureController creature;

    protected CreatureState(CreatureController creature)
    {
        this.creature = creature;
    }

    public abstract string Name { get; }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}
