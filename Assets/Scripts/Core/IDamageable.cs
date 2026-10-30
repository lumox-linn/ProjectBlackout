/// <summary>
/// Anything that can be hurt. The player's PlayerHealth  implements this.
/// The creature's Attack state calls it without needing to know about PlayerHealth:
///
///     var target = player.GetComponent&lt;IDamageable&gt;();
///     if (target != null) target.TakeDamage(1);
/// </summary>
public interface IDamageable
{
    void TakeDamage(int amount);
}
