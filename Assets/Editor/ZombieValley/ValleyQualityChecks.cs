using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ItFriends.ZombieValley.Editor
{
public static class ValleyQualityChecks
{
    private static string Output(string file){return Path.Combine(Path.GetDirectoryName(Application.dataPath),"Concepts/ZombieValley/"+file);}
    public static void Preview(string view)
    {
        var camera=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.name.StartsWith("Concept Overview"));
        Vector3 position,target;float fov;
        switch(view)
        {
            case "ground":position=new Vector3(0,2.1f,-72);target=new Vector3(0,13,-22);fov=75;break;
            case "highest":position=new Vector3(0,26.7f,61);target=new Vector3(0,9,-85);fov=75;break;
            case "stairs":position=new Vector3(10,6.6f,-54);target=new Vector3(0,5.5f,-48);fov=53;break;
            case "upper-stairs":position=new Vector3(-1,23,56);target=new Vector3(-11,22,59);fov=58;break;
            case "jump-bridge":position=new Vector3(42,21,8);target=new Vector3(39,17.2f,17);fov=58;break;
            case "house":position=new Vector3(-39,20,-27);target=new Vector3(-55,19,-17);fov=65;break;
            default:position=new Vector3(111,90,-128);target=new Vector3(0,12,12);fov=40;break;
        }
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;
    }
    public static void RuntimeTraversal()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode is required for gameplay verification");
        var pc=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var rb=pc.GetComponent<Rigidbody>();var layout=new OrganicValleyLayout();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var move=typeof(PlayerController).GetMethod("MovePlayer",flags);var jump=typeof(PlayerController).GetMethod("Jump",flags);var grounded=typeof(PlayerController).GetMethod("IsGrounded",flags);
        var vertical=typeof(PlayerController).GetField("verticalInput",flags);var horizontal=typeof(PlayerController).GetField("horizontalInput",flags);
        var position=rb.position;var rotation=rb.rotation;var velocity=rb.linearVelocity;bool enabled=pc.enabled;var mode=Physics.simulationMode;
        var results=new List<object>();
        try
        {
            pc.enabled=false;Physics.simulationMode=SimulationMode.Script;horizontal.SetValue(pc,0f);
            Action<Vector3,Vector3> place=(point,direction)=>{rb.position=point;rb.rotation=Quaternion.LookRotation(direction);rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;Physics.SyncTransforms();vertical.SetValue(pc,0f);for(int k=0;k<30;k++){move.Invoke(pc,null);Physics.Simulate(.02f);}};
            var stairRoutes=new List<ValleyStairRoute>(layout.Stairs);
            stairRoutes.Add(new ValleyStairRoute("Courtyard ruin west climb",new Vector3(-7,10,-10),new Vector3(-7,13,-1),3));
            stairRoutes.Add(new ValleyStairRoute("Courtyard ruin east climb",new Vector3(7,10,8),new Vector3(7,13,1.05f),3));
            foreach(var route in stairRoutes)
            {
                Vector3 low=route.Low,high=route.High;
                var flat=high-low;flat.y=0;var direction=flat.normalized;
                place(low-direction*.65f+Vector3.up*1.06f,direction);vertical.SetValue(pc,1f);float maxY=rb.position.y;
                int steps=Mathf.CeilToInt((flat.magnitude/pc.moveSpeed*1.65f+1f)/.02f);
                for(int k=0;k<steps;k++)
                {
                    move.Invoke(pc,null);Physics.Simulate(.02f);maxY=Mathf.Max(maxY,rb.position.y);
                    if(Vector3.Dot(rb.position-low,direction)>flat.magnitude+.65f)break;
                }
                var end=rb.position;var distance=high-end;distance.y=0;
                results.Add(new{name=route.Name,kind="actual Rigidbody climb",passed=distance.magnitude<2&&end.y>high.y+.85f,remainingMetres=distance.magnitude,expectedFootHeight=high.y,actualFootHeight=end.y-1,maxY});
            }
            place(new Vector3(0,11.05f,-24),Vector3.forward);float startY=rb.position.y;bool canJump=(bool)grounded.Invoke(pc,null);jump.Invoke(pc,null);float peak=startY;
            for(int k=0;k<110;k++){move.Invoke(pc,null);Physics.Simulate(.02f);peak=Mathf.Max(peak,rb.position.y);}
            results.Add(new{name="Jump and land on courtyard",passed=canJump&&peak-startY>1.5f&&Mathf.Abs(rb.position.y-startY)<.2f,groundedBefore=canJump,jumpHeight=peak-startY,landingDelta=rb.position.y-startY});
            var a=new Vector3(32,17,15);var b=new Vector3(46,17,19);var axis=(b-a).normalized;float length=(b-a).magnitude;
            place(a+axis*(length*.5f-2.2f)+Vector3.up*1.39f,axis);vertical.SetValue(pc,1f);bool jumped=false;float jumpAt=0;
            for(int k=0;k<95;k++)
            {
                float along=Vector3.Dot(rb.position-a,axis);
                if(!jumped&&along>length*.5f-1.3f){jumpAt=along;jump.Invoke(pc,null);jumped=true;}
                move.Invoke(pc,null);Physics.Simulate(.02f);
            }
            results.Add(new{name="Jump over 1.35m bridge break",passed=jumped&&Vector3.Dot(rb.position-a,axis)>length*.5f+.7f&&rb.position.y>17.7f,jumpAt,finish= new[]{rb.position.x,rb.position.y,rb.position.z}});
            File.WriteAllText(Output("play-mode-traversal.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{mode="Unity Play Mode, existing movement methods and PhysX simulation; input sampled by test harness",results},Newtonsoft.Json.Formatting.Indented));
        }
        finally
        {
            vertical.SetValue(pc,0f);horizontal.SetValue(pc,0f);rb.position=position;rb.rotation=rotation;rb.linearVelocity=velocity;Physics.SyncTransforms();Physics.simulationMode=mode;pc.enabled=enabled;
        }
    }
}
}
