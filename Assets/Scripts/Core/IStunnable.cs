/// <summary>
/// Anything that can be stunned by a thrown object. The creature (Sylvia, B7) implements this.
/// A thrown crate (Aprial, A4) calls it on whatever it hits, without needing the creature's code:
///
///     var stunnable = collision.collider.GetComponentInParent&lt;IStunnable&gt;();
///     if (stunnable != null) stunnable.Stun(3f);
/// </summary>
public interface IStunnable
{
    void Stun(float seconds);
}
