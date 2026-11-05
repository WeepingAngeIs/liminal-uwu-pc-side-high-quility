using System;
using System.Linq;
using UnityEngine;
using UdonSharpEditor;
using VRC.Udon;

public static class GravityValidation
{
    // Run in edit mode on the saved scene before SDK export, on either target.
    public static void Validate()
    {
        Physics.SyncTransforms();
        var ports = UnityEngine.Object.FindObjectsOfType<StillwaterGravityPort>();
        Require(ports.Length == 8,"Expected eight field terminals; found "+ports.Length);
        var outbound = ports.Single(p => p.name == "Six direction ride");
        Transform origin = outbound.transform.parent.parent;
        foreach(var p in ports)
        {
            UdonBehaviour vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(p);
            Require(vm && vm.programSource && vm.programSource.SerializedProgramAsset,
                p.name+" lacks its serialized Udon program");
            var compiled = vm.programSource.SerializedProgramAsset.RetrieveProgram();
            Require(compiled != null && compiled.EntryPoints.HasExportedSymbol("_interact") &&
                    compiled.EntryPoints.HasExportedSymbol("CarryStep") && compiled.EntryPoints.HasExportedSymbol("_onPlayerRespawn"),
                    p.name+" lacks the interaction, movement or cleanup entry point");
            Transform wired;
            Require(vm.publicVariables.TryGetVariableValue("destination",out wired) &&
                    wired != null && wired == p.destination,p.name+" destination mismatch in Udon variable table");
            Require(p.GetComponent<Collider>() && vm.proximity >= 2,p.name+" lacks reachable interaction geometry");
            Require(Vector3.Dot(wired.up, Vector3.up) > .999f,p.name+" would roll the headset");
            StandingSpace(wired.position, p.name);
        }

        foreach(string name in new[]{"Ceiling walk","Wall walk"})
        {
            var entry = ports.Single(p=>p.name==name);
            Transform wing = entry.destination.parent;
            Require((wing.position-origin.position).sqrMagnitude > 10000, name+" overlaps central map");
            var returns = ports.Where(p=>p.transform.IsChildOf(wing) && p.destination.IsChildOf(origin)).ToArray();
            Require(returns.Length==2 && returns[0].destination!=returns[1].destination,name+" lacks the two independent return routes");
            Transform rotated = wing.GetChild(0);
            float roll = name=="Ceiling walk"?180:90;
            Require(Quaternion.Angle(rotated.localRotation,Quaternion.Euler(0,0,roll)) < .01f, name+" orientation");
            Require(rotated.Find("Floodplain weather windows/Architecture"),name+" lacks its rotated exterior view");
        }

        UdonBehaviour ride = UdonSharpEditorUtility.GetBackingUdonBehaviour(outbound);
        Transform circuit, exit;
        Transform[] stops;
        Require(ride.publicVariables.TryGetVariableValue("circuit",out circuit) && circuit==outbound.circuit,"Drift root mismatch");
        Require(ride.publicVariables.TryGetVariableValue("exit",out exit) && exit==outbound.exit && exit.IsChildOf(origin),"Drift return mismatch");
        Require(ride.publicVariables.TryGetVariableValue("stops",out stops) && stops != null && stops.Length==6 &&
                stops.SequenceEqual(outbound.stops),"Six-stop Udon itinerary mismatch");
        Vector3[] axes = {Vector3.up,Vector3.right,Vector3.forward,Vector3.down,Vector3.left,Vector3.back};
        Vector3 previous=outbound.destination.position;
        for(int i=0;i<stops.Length;i++)
        {
            Transform stop = stops[i];
            Require(stop && stop.IsChildOf(circuit),"Missing directional stop "+i);
            Vector3 delta=stop.position-previous;
            Require(delta.magnitude>5 && Vector3.Dot(delta.normalized,axes[i])>.995f,"Incorrect field direction "+i);
            Require(!Physics.CapsuleCast(previous+Vector3.up*.28f,previous+Vector3.up*1.40f,.25f,
                delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore),"Obstacle intersects passenger route "+i);
            previous=stop.position;
        }
        Debug.Log("GRAVITY_VALIDATION_PASS: 8 interactive Udon terminals, live serialized destinations, 2 mapped walks, 4 returns, 6 unobstructed signed-axis carry spans.");
    }

    static void StandingSpace(Vector3 arrival, string name)
    {
        RaycastHit hit;
        Require(Physics.Raycast(arrival+Vector3.up*.12f,Vector3.down,out hit,2.5f,~0,QueryTriggerInteraction.Ignore)
                && hit.normal.y>.75f,name+" lacks walkable floor beneath its landing");
        Require(!Physics.CheckCapsule(arrival+Vector3.up*.36f,arrival+Vector3.up*1.48f,.24f,~0,QueryTriggerInteraction.Ignore),
            name+" lands inside solid geometry");
    }

    static void Require(bool condition,string message)
    {
        if(!condition) throw new InvalidOperationException("Gravity validation: "+message);
    }
}
