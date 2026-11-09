using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A floor plate that opens a door while something heavy sits on it.
/// Needs a Box Collider with "Is Trigger" ticked to detect what's on top.
/// With "Stay Open" ticked, the door stays open for good after the first press (tutorial plate).
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Tooltip("The door this plate opens.")]
    [SerializeField] private Door door;

    [Tooltip("Objects lighter than this (Rigidbody mass) don't press the plate.")]
    [SerializeField] private float requiredMass = 5f;

    [Tooltip("Tick for one-time plates: the door stays open even if the crate is taken off.")]
    [SerializeField] private bool stayOpen = false;

    // Every heavy object on the plate right now. A HashSet stores each one only once.
    private readonly HashSet<Rigidbody> heavyThings = new HashSet<Rigidbody>();
    private bool lockedOpen;

    public bool IsPressed => heavyThings.Count > 0;

    private void OnTriggerEnter(Collider other)
    {
        // attachedRigidbody is null for the player and walls, so they're ignored.
        Rigidbody body = other.attachedRigidbody;
        if (body != null && body.mass >= requiredMass)
        {
            heavyThings.Add(body);
            UpdateDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body != null && heavyThings.Remove(body))
            UpdateDoor();
    }

    private void UpdateDoor()
    {
        // A "stay open" plate that was already pressed ignores everything after that.
        if (lockedOpen)
            return;

        if (door != null)
            door.SetOpen(IsPressed);

        if (IsPressed && stayOpen)
            lockedOpen = true;

        Debug.Log($"[PressurePlate] {name} pressed = {IsPressed}");
    }
}