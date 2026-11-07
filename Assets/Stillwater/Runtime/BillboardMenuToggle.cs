using UdonSharp;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BillboardMenuToggle : UdonSharpBehaviour
{
    public BillboardPlayer controls;
    public override void Interact() { if (controls != null) controls.ToggleMenu(); }
}
