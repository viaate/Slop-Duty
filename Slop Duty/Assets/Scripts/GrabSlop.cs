using UnityEngine;

// Put this on the ladle (SlopLadle). Give the ladle a Collider2D with
// "Is Trigger" checked (and a kinematic Rigidbody2D so 2D trigger events fire),
// and make sure each Slop bucket also has a Collider2D.
//
// The old version tried to read a color field that was never assigned
// (the line that set it was commented out) and only checked for a mouse
// click inside OnCollisionEnter2D, which only fires on the single physics
// frame the ladle first touches the bucket -- so a click a moment later
// never registered. Simplest correct behavior: touching a bucket with the
// ladle selects it, using the same selection system Slop.cs/SlopLogic
// already use, so IndividualStudent's TryServeStudent() picks it up too.
public class GrabSlop : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Slop slop = other.GetComponent<Slop>();
        if (slop == null) return;

        slop.SelectSlop();
    }
}
