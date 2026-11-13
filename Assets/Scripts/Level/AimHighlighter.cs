using System.Collections.Generic;
using UnityEngine;
using ProjectBlackout.PlayerTest;

/// <summary>
/// Goes on the player's camera. Outlines what the player is aiming at when they can use it:
/// cyan = grab with the gauntlet (magnetic objects), yellow = use with E (IInteractable, gauntlet pickup).
/// Works by temporarily adding an outline material to the target's renderers.
/// </summary>
public class AimHighlighter : MonoBehaviour
{
    [SerializeField] private Material grabOutline;
    [SerializeField] private Material useOutline;
    [SerializeField] private float useReach = 3f;
    [SerializeField] private LayerMask mask = ~0;

    private BlackoutMagnetGlove glove;
    private GameObject current;
    private readonly Dictionary<Renderer, Material[]> original = new Dictionary<Renderer, Material[]>();

    private void Start() { glove = FindFirstObjectByType<BlackoutMagnetGlove>(); }

    private void Update()
    {
        Material outline = null;
        GameObject target = GameEvents.CurrentState == GameState.Playing ? FindTarget(out outline) : null;

        if (target == current) return;
        ClearOutline();
        current = target;
        if (current != null) AddOutline(current, outline);
    }

    private GameObject FindTarget(out Material outline)
    {
        outline = null;
        float range = glove != null ? Mathf.Max(glove.pickupRange, useReach) : useReach;
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
            return null;

        // 1) Grabbable with the gauntlet (only if you have it and your hands are free).
        var magnetic = hit.collider.GetComponentInParent<BlackoutMagneticObject>();
        if (magnetic != null && magnetic.canBePickedUp && !magnetic.IsHeld &&
            glove != null && glove.HasGlove && !glove.IsHolding && hit.distance <= glove.pickupRange)
        {
            outline = grabOutline;
            return magnetic.gameObject;
        }

        if (hit.distance > useReach) return null;

        // 2) Usable with E (generator, ...).
        var usable = hit.collider.GetComponentInParent<IInteractable>();
        if (usable != null && usable.CanInteract)
        {
            outline = useOutline;
            return ((Component)usable).gameObject;
        }

        // 3) The gauntlet pickup.
        var pickup = hit.collider.GetComponentInParent<BlackoutGlovePickup>();
        if (pickup != null)
        {
            outline = useOutline;
            return pickup.gameObject;
        }
        return null;
    }

    private void AddOutline(GameObject go, Material outline)
    {
        if (outline == null) return;
        var custom = go.GetComponent<HighlightRenderers>();
        Renderer[] list = custom != null ? custom.renderers : go.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in list)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Material[] mats = r.sharedMaterials;
            original[r] = mats;
            var withOutline = new Material[mats.Length + 1];
            mats.CopyTo(withOutline, 0);
            withOutline[mats.Length] = outline;
            r.sharedMaterials = withOutline;
        }
    }

    private void ClearOutline()
    {
        foreach (var pair in original)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        original.Clear();
    }

    private void OnDisable() { ClearOutline(); current = null; }
}