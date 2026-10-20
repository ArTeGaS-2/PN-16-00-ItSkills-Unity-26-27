using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace ItFriends.ZombieValley.Editor
{
public sealed class ValleyTerrace
{
    public readonly string Name;
    public readonly Rect Footprint;
    public readonly float Height;
    public ValleyTerrace(string name, float x, float z, float width, float depth, float height)
    { Name=name; Footprint=new Rect(x,z,width,depth); Height=height; }
    public bool Contains(float x,float z) { return Footprint.Contains(new Vector2(x,z)); }
}

public sealed class ValleyPlan
{
    public readonly List<ValleyTerrace> Terraces=new List<ValleyTerrace>
    {
        new ValleyTerrace("Central plaza",-24,-18,48,36,3),
        new ValleyTerrace("West settlement",-48,-14,20,32,6),
        new ValleyTerrace("East settlement",28,-14,20,32,6),
        new ValleyTerrace("North settlement",-26,22,52,14,6),
        new ValleyTerrace("Upper landmark",-12,38,24,12,9),
        new ValleyTerrace("Southwest yard",-50,-36,18,16,3),
        new ValleyTerrace("Southeast yard",32,-36,18,16,3)
    };
    public readonly Vector3[] Entrances={new Vector3(0,0,-46),new Vector3(-42,0,-44),new Vector3(42,0,-44),new Vector3(-57,0,0),new Vector3(57,0,0),new Vector3(-45,0,42),new Vector3(45,0,42),new Vector3(0,0,55)};
    public float Surface(float x,float z)
    { float h=0; foreach(var t in Terraces) if(t.Contains(x,z))h=Mathf.Max(h,t.Height);return h; }
}

public sealed class ValleyPart
{
    public GameObject Root;
    public bool Collision;
    public ValleyPart(GameObject root,bool collision){Root=root;Collision=collision;}
}

public sealed class ValleyPrefabFactory
{
    public readonly List<ValleyPart> Parts=new List<ValleyPart>();
    private readonly Dictionary<string,GameObject> assets=new Dictionary<string,GameObject>();
    private readonly Dictionary<string,Bounds> bounds=new Dictionary<string,Bounds>();
    private readonly Material dryGround;
    private readonly Scene scene;
    public readonly Transform Sources;
    public readonly Transform Map;
    public ValleyPrefabFactory(Scene target,Transform map,Transform existingSources=null)
    {
        scene=target;Map=map;Sources=existingSources!=null?existingSources:Group("Editable source modules",map);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/LowPolyConstructor/Prefabs"}))
        {
            string p=AssetDatabase.GUIDToAssetPath(guid);string key=System.IO.Path.GetFileNameWithoutExtension(p);
            if(!assets.ContainsKey(key))assets[key]=AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        string matPath="Assets/ZombieValley/Materials/Valley_DryGround.mat";
        dryGround=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(dryGround==null)
        {
            dryGround=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dryGround.name="Valley dry ground";dryGround.SetColor("_BaseColor",new Color(.56f,.49f,.35f));
            dryGround.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(dryGround,matPath);
        }
    }
    public void RestoreSourceParts()
    {
        foreach(var t in Sources.GetComponentsInChildren<Transform>(true))
            if(assets.ContainsKey(t.name)&&t.childCount==1&&PrefabUtility.IsAnyPrefabInstanceRoot(t.GetChild(0).gameObject))
                Parts.Add(new ValleyPart(t.gameObject,t.gameObject.layer!=2));
        if(Parts.Count==0)throw new Exception("No editable source modules found");
    }
    public Transform Group(string name,Transform parent)
    { var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(parent,false);return go.transform; }
    private Bounds BoundsFor(string name)
    {
        if(bounds.TryGetValue(name,out var b))return b;
        if(!assets.TryGetValue(name,out var prefab))throw new Exception("Missing prefab: "+name);
        var rr=prefab.GetComponentsInChildren<Renderer>(true);if(rr.Length==0)throw new Exception("No renderers: "+name);
        b=rr[0].bounds;for(int i=1;i<rr.Length;i++)b.Encapsulate(rr[i].bounds);bounds[name]=b;return b;
    }
    public GameObject Fit(string name,Transform parent,Vector3 bottom,Vector3 dimensions,float yaw=0,bool collision=true,bool ground=false)
    { return Fit(name,parent,bottom,dimensions,Quaternion.Euler(0,yaw,0),collision,ground); }
    public GameObject Fit(string name,Transform parent,Vector3 bottom,Vector3 dimensions,Quaternion rotation,bool collision=true,bool ground=false)
    {
        Bounds b=BoundsFor(name);var wrapper=Group(name,parent);wrapper.position=bottom;wrapper.rotation=rotation;wrapper.gameObject.layer=collision?0:2;
        var child=(GameObject)PrefabUtility.InstantiatePrefab(assets[name],scene);child.transform.SetParent(wrapper,false);
        Vector3 s=new Vector3(dimensions.x/b.size.x,dimensions.y/b.size.y,dimensions.z/b.size.z);
        child.transform.localScale=Vector3.Scale(child.transform.localScale,s);
        child.transform.localPosition=new Vector3(-b.center.x*s.x,-b.min.y*s.y,-b.center.z*s.z);
        if(ground)foreach(var r in child.GetComponentsInChildren<MeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>dryGround).ToArray();
        foreach(var c in child.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var mb in child.GetComponentsInChildren<MonoBehaviour>(true))mb.enabled=false;
        Parts.Add(new ValleyPart(wrapper.gameObject,collision));return wrapper.gameObject;
    }
    public GameObject Raw(string name,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale,bool collision=true)
    {
        BoundsFor(name);var wrapper=Group(name,parent);wrapper.position=position;wrapper.rotation=rotation;wrapper.gameObject.layer=collision?0:2;
        var go=(GameObject)PrefabUtility.InstantiatePrefab(assets[name],scene);go.transform.SetParent(wrapper,false);
        go.transform.localScale=Vector3.Scale(go.transform.localScale,scale);
        foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var mb in go.GetComponentsInChildren<MonoBehaviour>(true))mb.enabled=false;
        Parts.Add(new ValleyPart(wrapper.gameObject,collision));return wrapper.gameObject;
    }
    public void Beam(Transform parent,Vector3 a,Vector3 b,float thickness=.18f)
    {
        Vector3 d=b-a;var r=Quaternion.FromToRotation(Vector3.forward,d.normalized);
        Fit("P02_Beam_2m",parent,(a+b)*.5f-r*Vector3.up*(thickness*.5f),new Vector3(thickness,thickness,d.magnitude),r,true);
    }
    public void Floor(Transform parent,Vector3 center,float width,float depth,float height,bool wood=false,float yaw=0)
    { Fit(wood?"D29_Pier_Deck_2m":"T01_Ground_Flat_2m",parent,new Vector3(center.x,height-(wood?.26f:.20f),center.z),new Vector3(width,wood?.26f:.20f,depth),yaw,true,!wood); }
    public void Tree(string name,Transform parent,Vector3 position,float scale)
    {
        BoundsFor(name);var go=(GameObject)PrefabUtility.InstantiatePrefab(assets[name],scene);go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale*=scale;
    }
}

public sealed class ValleyTerrainBuilder
{
    private readonly ValleyPrefabFactory f;private readonly ValleyPlan plan;
    public ValleyTerrainBuilder(ValleyPrefabFactory factory,ValleyPlan layout){f=factory;plan=layout;}
    public void Build()
    {
        var root=f.Group("01 Terrain and terraces",f.Sources);
        for(float x=-75;x<75;x+=10)for(float z=-65;z<65;z+=10)f.Floor(root,new Vector3(x+5,0,z+5),10.02f,10.02f,0);
        foreach(var t in plan.Terraces)
        {
            var group=f.Group(t.Name,root);var r=t.Footprint;
            int nx=Mathf.CeilToInt(r.width/6),nz=Mathf.CeilToInt(r.height/6);float w=r.width/nx,d=r.height/nz;
            for(int ix=0;ix<nx;ix++)for(int iz=0;iz<nz;iz++)
            {
                float x=r.x+(ix+.5f)*w,z=r.y+(iz+.5f)*d;
                f.Fit("T04_Cliff_Straight_2m",group,new Vector3(x,-.02f,z),new Vector3(w+.02f,t.Height,d+.02f));
                f.Floor(group,new Vector3(x,t.Height,z),w+.02f,d+.02f,t.Height+.10f);
            }
        }
        var rocks=f.Group("Faceted valley walls",root);var random=new System.Random(41);
        for(int side=0;side<4;side++)for(int i=0;i<13;i++)
        {
            float a=-58+i*9.6f;float x=side<2?(side==0?-65:65):a;float z=side<2?a*.85f:(side==2?-54:58);
            bool entrance=plan.Entrances.Any(e=>Vector2.Distance(new Vector2(e.x,e.z),new Vector2(x,z))<10);
            if(entrance)continue;
            float h=8+(float)random.NextDouble()*7;float w=8+(float)random.NextDouble()*4;
            f.Fit("N01_Rock_Boulder",rocks,new Vector3(x,-.5f,z),new Vector3(w,h,9),random.Next(-18,19));
            f.Fit("N05_Rock_Slab",rocks,new Vector3(x+(side<2?0:2),h*.55f,z+(side<2?2:0)),new Vector3(w*.9f,2.2f,7),random.Next(0,360));
        }
        foreach(var t in plan.Terraces)
        {
            var r=t.Footprint;for(int i=0;i<3;i++)
            {
                float x=r.x+(i+.5f)*r.width/3;
                f.Fit("N01_Rock_Boulder",rocks,new Vector3(x,-.15f,r.y+r.height+.1f),new Vector3(4,t.Height*.85f,3.4f),i*23);
            }
        }
    }
}

public sealed class ValleyTraversalBuilder
{
    private readonly ValleyPrefabFactory f;private readonly Transform root;private int rampNumber;
    public readonly List<object> Routes=new List<object>();
    public ValleyTraversalBuilder(ValleyPrefabFactory factory){f=factory;root=f.Group("02 Stairs bridges and elevated loops",f.Sources);}
    private Vector3 Local(Transform t,Vector3 p){return t.TransformPoint(p);}
    public void Stairs(string name,Vector3 low,Vector3 high,float width=4)
    {
        Vector3 flat=high-low;flat.y=0;float run=flat.magnitude,rise=high.y-low.y;float yaw=Mathf.Atan2(flat.x,flat.z)*Mathf.Rad2Deg;
        var group=f.Group(name,root);group.position=low;group.rotation=Quaternion.Euler(0,yaw,0);
        int n=Mathf.Max(1,Mathf.RoundToInt(rise));float h=rise/n,d=run/n;
        // Native steps climb toward -X; local Z is their width.
        var landscape=new OrganicValleyLayout();
        for(int i=0;i<n;i++)
        {
            f.Fit("P19_Stairs_5Steps",group,Local(group,new Vector3(0,i*h,(i+.5f)*d)),new Vector3(d,h,width),yaw+90,false);
            var centre=Local(group,new Vector3(0,0,(i+.5f)*d));var start=Local(group,new Vector3(0,0,i*d));var end=Local(group,new Vector3(0,0,(i+1)*d));
            float ground=Mathf.Min(landscape.Height(centre.x,centre.z),Mathf.Min(landscape.Height(start.x,start.z),landscape.Height(end.x,end.z)))-.15f;
            float baseTop=low.y+i*h;
            if(baseTop-ground>.20f)
            {
                f.Fit("P01_Block_1m",group,new Vector3(centre.x,ground,centre.z),new Vector3(width-.20f,baseTop-ground,d+.015f),yaw);
                foreach(int side in new[]{-1,1})
                {
                    var wall=Local(group,new Vector3(side*(width*.5f-.11f),0,(i+.5f)*d));
                    f.Fit("A01_Wall_Solid_2m",group,new Vector3(wall.x,ground,wall.z),new Vector3(d,baseTop-ground,.22f),yaw+90);
                }
            }
        }
        RampCollider(name,low,high,width);
        foreach(int side in new[]{-1,1})
        {
            f.Beam(group,Local(group,new Vector3(side*width*.5f,1,0)),Local(group,new Vector3(side*width*.5f,rise+1,run)),.12f);
            for(int i=0;i<=n;i++)f.Beam(group,Local(group,new Vector3(side*width*.5f,i*h,i*d)),Local(group,new Vector3(side*width*.5f,i*h+1,i*d)),.12f);
        }
        Routes.Add(new{name,kind="stairs",low=new[]{low.x,low.y,low.z},high=new[]{high.x,high.y,high.z},width});
    }
    private void RampCollider(string name,Vector3 low,Vector3 high,float width)
    {
        var dir=high-low;dir.y=0;var right=Vector3.Cross(Vector3.up,dir.normalized)*(width*.5f);
        var mesh=new Mesh();mesh.name=name+" walking ramp";
        mesh.vertices=new[]{low-right,low+right,high+right,high-right};mesh.triangles=new[]{0,3,1,1,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
        string p="Assets/ZombieValley/GeneratedMeshes/walk_ramp_"+(rampNumber++).ToString("00")+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(p);
        mesh=ValleyMeshAsset.Store(mesh,p);
        var go=f.Group(name+" | smooth collision",f.Map);var collider=go.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
    }
    public void Bridge(string name,Vector3 a,Vector3 b,float width,float gap=0)
    {
        var delta=b-a;float length=new Vector2(delta.x,delta.z).magnitude;float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
        var g=f.Group(name,root);g.position=a;g.rotation=Quaternion.Euler(0,yaw,0);
        int n=Mathf.CeilToInt(length/.30f);float step=length/n;
        for(int i=0;i<n;i++)
        {
            float z=(i+.5f)*step;if(gap>0&&Mathf.Abs(z-length*.5f)<gap*.5f)continue;
            float lift=delta.y*z/length+.35f*Mathf.Sin(z/length*Mathf.PI);
            f.Fit("D36_Bridge_Plank_1_5m",g,Local(g,new Vector3(0,lift-.15f,z)),new Vector3(width,.15f,step-.018f),yaw);
        }
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*(width*.5f-.12f);
            var spans=gap>0?new[]{new Vector2(0,(length-gap)*.5f),new Vector2((length+gap)*.5f,length)}:new[]{new Vector2(0,length)};
            foreach(var span in spans)
            {
                f.Beam(g,Local(g,new Vector3(x,-.24f+delta.y*span.x/length,span.x)),Local(g,new Vector3(x,-.24f+delta.y*span.y/length,span.y)),.22f);
                f.Beam(g,Local(g,new Vector3(x,1.10f+delta.y*span.x/length,span.x)),Local(g,new Vector3(x,1.10f+delta.y*span.y/length,span.y)),.12f);
                int posts=Mathf.Max(1,Mathf.CeilToInt((span.y-span.x)/2.5f));
                for(int i=0;i<=posts;i++)
                {
                    float z=Mathf.Lerp(span.x,span.y,(float)i/posts);
                    float elevation=delta.y*z/length;
                    f.Beam(g,Local(g,new Vector3(x,-.28f+elevation,z)),Local(g,new Vector3(x,1.10f+elevation,z)),.14f);
                    if(i==0||i==posts)f.Beam(g,Local(g,new Vector3(x,-a.y,z)),Local(g,new Vector3(x,-.28f+elevation,z)),.25f);
                }
            }
        }
        Routes.Add(new{name,kind=gap>2?"broken bridge with ground bypass":gap>0?"jump bridge":"complete bridge",from=new[]{a.x,a.y,a.z},to=new[]{b.x,b.y,b.z},width,gap});
    }
    public void Deck(string name,Rect footprint,float height)
    {
        var g=f.Group(name,root);int nx=Mathf.CeilToInt(footprint.width/4),nz=Mathf.CeilToInt(footprint.height/2);
        for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            f.Floor(g,new Vector3(footprint.x+(x+.5f)*footprint.width/nx,height,footprint.y+(z+.5f)*footprint.height/nz),footprint.width/nx,footprint.height/nz,height,true);
        for(int i=0;i<=nx;i++)for(int j=0;j<2;j++)
        {
            Vector3 top=new Vector3(footprint.x+i*footprint.width/nx,height-.2f,footprint.y+j*footprint.height);
            f.Beam(g,new Vector3(top.x,3,top.z),top,.26f);
        }
    }
    public void Build()
    {
        Stairs("South main stairs",new Vector3(0,0,-25),new Vector3(0,3,-18),5);
        Stairs("West ground approach",new Vector3(-60,0,-23),new Vector3(-50,3,-23),4);
        Stairs("East ground approach",new Vector3(60,0,-23),new Vector3(50,3,-23),4);
        Stairs("West terrace stairs",new Vector3(-20,3,-5),new Vector3(-28,6,-5),4);
        Stairs("East terrace stairs",new Vector3(20,3,-5),new Vector3(28,6,-5),4);
        Stairs("Northwest courtyard stairs",new Vector3(-18,3,15),new Vector3(-18,6,22),4);
        Stairs("Northeast courtyard stairs",new Vector3(18,3,15),new Vector3(18,6,22),4);
        Stairs("Outer west climb",new Vector3(-60,0,7),new Vector3(-48,6,7),4);
        Stairs("Outer east climb",new Vector3(60,0,7),new Vector3(48,6,7),4);
        Stairs("North ground climb",new Vector3(20,0,49),new Vector3(20,6,36),4);
        Stairs("Upper west stairs",new Vector3(-20,6,33),new Vector3(-8,9,38),4);
        Stairs("Upper east stairs",new Vector3(20,6,33),new Vector3(8,9,38),4);
        Deck("Central elevated gallery",new Rect(-14,10,28,6),6);
        Stairs("Gallery west access",new Vector3(-10,3,3),new Vector3(-10,6,10),3.5f);
        Stairs("Gallery east access",new Vector3(10,3,3),new Vector3(10,6,10),3.5f);
        Bridge("West gallery bridge",new Vector3(-29,6,12),new Vector3(-14,6,12),3.6f);
        Bridge("East gallery jump bridge",new Vector3(14,6,12),new Vector3(29,6,12),3.6f,1.35f);
        Bridge("Northwest full bridge",new Vector3(-38,6,17),new Vector3(-23,6,25),3.5f);
        Bridge("Northeast full bridge",new Vector3(38,6,17),new Vector3(23,6,25),3.5f);
        Bridge("Southeast full bridge",new Vector3(23,3,-13),new Vector3(34,3,-23),3.5f);
        Bridge("Southwest broken bridge",new Vector3(-23,3,-13),new Vector3(-34,3,-23),3.5f,3.6f);
    }
}

public sealed class ValleySettlementBuilder
{
    private readonly ValleyPrefabFactory f;private readonly Transform root;
    public ValleySettlementBuilder(ValleyPrefabFactory factory){f=factory;root=f.Group("03 Modular settlement",f.Sources);}
    private Vector3 At(Transform t,float x,float y,float z){return t.TransformPoint(new Vector3(x,y,z));}
    public void House(string name,Vector3 origin,float width,float depth,float yaw,bool open=false)
    {
        var g=f.Group(name,root);g.position=origin;g.rotation=Quaternion.Euler(0,yaw,0);float h=3;
        int nx=Mathf.RoundToInt(width/2),nz=Mathf.RoundToInt(depth/2);
        for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)f.Floor(g,At(g,-width*.5f+(i+.5f)*2,0,-depth*.5f+(j+.5f)*2),2,2,origin.y,true,yaw);
        for(int i=0;i<nx;i++)
        {
            float x=-width*.5f+(i+.5f)*2;
            if(!open||i==0||i==nx-1)f.Fit(i==nx/2?"A05_Wall_Door_2m":"A04_Wall_Window_2m",g,At(g,x,0,-depth*.5f),new Vector3(2,h,.34f),yaw);
            f.Fit(i%2==0?"A01_Wall_Solid_2m":"A04_Wall_Window_2m",g,At(g,x,0,depth*.5f),new Vector3(2,h,.34f),yaw);
        }
        for(int j=0;j<nz;j++)for(int side=-1;side<=1;side+=2)
        {
            float z=-depth*.5f+(j+.5f)*2;
            f.Fit(j==nz/2?"A04_Wall_Window_2m":"A01_Wall_Solid_2m",g,At(g,side*width*.5f,0,z),new Vector3(2,h,.34f),yaw+90);
        }
        float roofY=1.18f;
        f.Raw("A13_Roof_Slope_2m",g,At(g,-width*.5f,h,-depth*.5f),g.rotation,new Vector3(width/4,roofY,depth/2));
        f.Raw("A13_Roof_Slope_2m",g,At(g,width*.5f,h,depth*.5f),g.rotation*Quaternion.Euler(0,180,0),new Vector3(width/4,roofY,depth/2));
        f.Raw("A14_Roof_Ridge_2m",g,At(g,0,h+1.4f*roofY,-depth*.5f),g.rotation,new Vector3(1,1,depth/2));
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)f.Beam(g,At(g,sx*width*.5f,0,sz*depth*.5f),At(g,sx*width*.5f,h,sz*depth*.5f),.15f);
        for(int i=0;i<nx;i++)f.Floor(g,At(g,-width*.5f+(i+.5f)*2,0,-depth*.5f-1),2,2,origin.y,true,yaw);
        if(open)
        {
            f.Raw("A13_Roof_Slope_2m",g,At(g,-width*.5f,h*.80f,-depth*.5f-2.4f),g.rotation,new Vector3(width/2,.28f,1.2f));
            for(int s=-1;s<=1;s+=2)f.Beam(g,At(g,s*(width*.5f-.3f),0,-depth*.5f-2.2f),At(g,s*(width*.5f-.3f),h*.80f,-depth*.5f-2.2f),.16f);
        }
    }
    public void Build()
    {
        House("West cottage",new Vector3(-40,6,-7),6,6,-90);
        House("West open workshop",new Vector3(-40,6,10),8,6,-90,true);
        House("East cottage",new Vector3(40,6,-7),6,6,90);
        House("East open workshop",new Vector3(40,6,10),8,6,90,true);
        House("Northwest cottage",new Vector3(-18,6,29),6,6,0);
        House("Northeast cottage",new Vector3(18,6,29),6,6,0);
        House("Highest cottage landmark",new Vector3(0,9,44),8,6,0);
        House("Southwest cottage",new Vector3(-42,3,-28),6,6,-90);
        House("Southeast cottage",new Vector3(42,3,-28),6,6,90);
        House("Plaza workshop",new Vector3(-16,3,-10),6,4,0,true);
    }
}

public sealed class ValleyRuinsBuilder
{
    private readonly ValleyPrefabFactory f;private readonly ValleyPlan plan;private readonly Transform root;
    public ValleyRuinsBuilder(ValleyPrefabFactory factory,ValleyPlan p){f=factory;plan=p;root=f.Group("04 Ruins and entrance arches",f.Sources);}
    private void Arch(Transform g,Vector3 pos,float yaw,float height=3.4f)
    {
        var q=Quaternion.Euler(0,yaw,0);
        f.Fit("A21_Ruin_Arch_Half",g,pos+q*new Vector3(-1,0,0),new Vector3(2,height,.5f),yaw);
        f.Fit("A21_Ruin_Arch_Half",g,pos+q*new Vector3(1,0,0),new Vector3(2,height,.5f),yaw+180);
    }
    public void Build()
    {
        var central=f.Group("Central courtyard ruins",root);
        for(int i=0;i<6;i++)
        {
            float x=-9+i*3.6f;
            if(i!=2&&i!=4)f.Fit("A18_Ruin_Wall_2m",central,new Vector3(x,3,-1.5f),new Vector3(3.4f,1.2f+(i%3)*.5f,.55f));
            if(i!=1&&i!=4)f.Fit("A18_Ruin_Wall_2m",central,new Vector3(x,3,9.8f),new Vector3(3.4f,3,.6f));
        }
        Arch(central,new Vector3(0,3,9.8f),0,3.0f);
        Arch(central,new Vector3(5,3,-1.5f),0,3.4f);
        for(int i=0;i<3;i++)f.Fit("A18_Ruin_Wall_2m",central,new Vector3(-13.5f,3,1+i*2.8f),new Vector3(2.8f,2,.6f),90);
        for(int i=0;i<2;i++)f.Fit("A19_Ruin_Corner",central,new Vector3(9+i*2,3,3+i*3),new Vector3(3.2f,2.2f,2.6f),i*90);
        f.Fit("A22_Rubble_Cluster",central,new Vector3(-6,3,6),new Vector3(2.8f,.55f,2.4f),30);
        f.Fit("A22_Rubble_Cluster",central,new Vector3(13,3,-9),new Vector3(2.2f,.45f,2),80);
        for(int i=0;i<plan.Entrances.Length;i++)
        {
            var e=plan.Entrances[i];float yaw=i==3||i==4?90:0;Arch(root,e,yaw,3.8f);
            var marker=f.Group("Future enemy entrance "+(i+1),f.Map);marker.position=e+Vector3.up*.1f;
        }
        var spawn=f.Group("Player spawn",f.Map);spawn.position=new Vector3(0,1.05f,-34);
    }
}

public sealed class ValleyDressingBuilder
{
    private readonly ValleyPrefabFactory f;private readonly ValleyPlan plan;
    public ValleyDressingBuilder(ValleyPrefabFactory factory,ValleyPlan p){f=factory;plan=p;}
    public void Build()
    {
        var props=f.Group("05 Sparse props and obstacles",f.Sources);
        var positions=new[]{new Vector2(-20,-14),new Vector2(-11,-14),new Vector2(16,-13),new Vector2(20,8),new Vector2(-20,5),new Vector2(-7,-8),new Vector2(6,-9),new Vector2(-35,1),new Vector2(-45,15),new Vector2(35,2),new Vector2(45,15),new Vector2(-23,32),new Vector2(23,32),new Vector2(-8,45),new Vector2(8,45),new Vector2(-47,-32),new Vector2(47,-32),new Vector2(-28,-28),new Vector2(27,-27)};
        for(int i=0;i<positions.Length;i++)
        {
            var p=positions[i];float y=plan.Surface(p.x,p.y);
            f.Fit(i%3==0?"Barrel_Closed":"Crate_Closed",props,new Vector3(p.x,y,p.y),i%3==0?new Vector3(.9f,1.1f,.9f):new Vector3(1.25f,1,1.25f),i*31);
            if(i%4==0)f.Fit("Crate_Closed",props,new Vector3(p.x+1.4f,y,p.y+.5f),new Vector3(1,.8f,1),i*16);
        }
        f.Fit("N07_Log",props,new Vector3(8,3,-13),new Vector3(.6f,.5f,3),70);
        f.Fit("N07_Log",props,new Vector3(-28,0,-36),new Vector3(.6f,.5f,3),40);
        foreach(var t in plan.Terraces)
        {
            var r=t.Footprint;
            for(int i=0;i<Mathf.FloorToInt(r.width/5);i++)
            {
                if(i%3==1)continue;float x=r.x+2+i*5,z=r.y+r.height-.35f;
                f.Fit("D03_Fence_Pickets_2m",props,new Vector3(x,t.Height,z),new Vector3(2.8f,1.1f,.16f));
            }
        }
        var trees=f.Group("Trees with original LOD",f.Map);
        var treePos=new[]{new Vector2(-54,-35),new Vector2(55,-35),new Vector2(-53,28),new Vector2(53,28),new Vector2(-32,38),new Vector2(31,40),new Vector2(-24,-39),new Vector2(24,-40),new Vector2(-43,16),new Vector2(45,-11),new Vector2(-22,15)};
        for(int i=0;i<treePos.Length;i++){var p=treePos[i];f.Tree(i%2==0?"GR_Tree_Broad":"GR_Tree_Slim",trees,new Vector3(p.x,plan.Surface(p.x,p.y),p.y),1.4f);}
    }
}

public sealed class ValleyStaticMeshBaker
{
    private readonly ValleyPrefabFactory f;private readonly Scene scene;
    private sealed class Batch
    { public Material Material;public bool Collision;public readonly List<CombineInstance> Pieces=new List<CombineInstance>(); }
    public int RendererCount;public long Triangles;
    public ValleyStaticMeshBaker(ValleyPrefabFactory factory,Scene target){f=factory;scene=target;}
    public void Bake()
    {
        var batches=new Dictionary<string,Batch>();
        foreach(var part in f.Parts)
        {
            foreach(var mf in part.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr=mf.GetComponent<MeshRenderer>();if(mr==null||mf.sharedMesh==null||!mr.enabled)continue;
                Vector3 c=mr.bounds.center;int cx=Mathf.FloorToInt((c.x+80)/24),cz=Mathf.FloorToInt((c.z+80)/24);
                var mats=mr.sharedMaterials;
                for(int sm=0;sm<mf.sharedMesh.subMeshCount;sm++)
                {
                    var m=mats[Mathf.Min(sm,mats.Length-1)];if(m==null)continue;
                    string key=cx+"_"+cz+"_"+m.GetInstanceID()+"_"+part.Collision;
                    if(!batches.TryGetValue(key,out var batch)){batch=new Batch{Material=m,Collision=part.Collision};batches[key]=batch;}
                    batch.Pieces.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=sm,transform=mf.transform.localToWorldMatrix});
                }
            }
        }
        var previous=f.Map.Find("Static render chunks");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var root=f.Group("Static render chunks",f.Map);int index=0;
        foreach(var pair in batches)
        {
            var b=pair.Value;var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Valley chunk "+index};
            mesh.CombineMeshes(b.Pieces.ToArray(),true,true,false);mesh.RecalculateBounds();
            string assetPath="Assets/ZombieValley/GeneratedMeshes/chunk_"+index.ToString("000")+".asset";
            mesh=ValleyMeshAsset.Store(mesh,assetPath);
            var go=f.Group("Chunk "+index+" | "+b.Material.name,root).gameObject;go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=b.Material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(b.Collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
            Triangles+=(long)mesh.GetIndexCount(0)/3;index++;
        }
        RendererCount=index;f.Sources.gameObject.SetActive(false);
    }
}

public sealed class ZombieValleySceneBuilder
{
    private const string ScenePath="Assets/Scenes/ZombieValley.unity";
    private static void Folder(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
    public object Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before building");
        foreach(var s in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt))if(s.isDirty)throw new Exception("Preserve unsaved scene: "+s.name);
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new Exception("ZombieValley scene already exists; do not overwrite");
        Folder("Assets","ZombieValley");Folder("Assets/ZombieValley","Materials");Folder("Assets/ZombieValley","GeneratedMeshes");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var map=new GameObject("Zombie Valley | Selected concept 01");var plan=new ValleyPlan();var factory=new ValleyPrefabFactory(scene,map.transform);
        new ValleyTerrainBuilder(factory,plan).Build();var traversal=new ValleyTraversalBuilder(factory);traversal.Build();
        new ValleySettlementBuilder(factory).Build();new ValleyRuinsBuilder(factory,plan).Build();new ValleyDressingBuilder(factory,plan).Build();
        var baker=new ValleyStaticMeshBaker(factory,scene);baker.Bake();
        var lightGo=new GameObject("Valley daylight");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.91f,.78f);light.intensity=1.25f;light.shadows=LightShadows.Soft;lightGo.transform.rotation=Quaternion.Euler(48,-35,0);
        RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.62f,.72f);RenderSettings.ambientEquatorColor=new Color(.37f,.41f,.44f);RenderSettings.ambientGroundColor=new Color(.20f,.18f,.15f);RenderSettings.fog=false;RenderSettings.fogColor=new Color(.64f,.72f,.76f);
        var player=GameObject.CreatePrimitive(PrimitiveType.Capsule);player.name="Player | existing PlayerController";player.transform.position=new Vector3(0,1.05f,-34);
        var rb=player.AddComponent<Rigidbody>();rb.mass=1;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.constraints=RigidbodyConstraints.FreezeRotation;
        var controller=player.AddComponent<PlayerController>();controller.moveSpeed=7;controller.jumpForce=6;controller.firstPersonPosition=new Vector3(0,.65f,0);controller.thirdPersonOffset=new Vector3(0,2.2f,-4.5f);
        var cameraGo=new GameObject("Player Camera");cameraGo.tag="MainCamera";cameraGo.transform.position=player.transform.position+controller.firstPersonPosition;cameraGo.transform.rotation=Quaternion.Euler(4,0,0);var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.07f;camera.farClipPlane=230;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;cameraGo.AddComponent<AudioListener>();cameraGo.AddComponent<UniversalAdditionalCameraData>();
        var overviewGo=new GameObject("Concept Overview Camera | disabled during play");overviewGo.transform.position=new Vector3(68,53,-80);overviewGo.transform.LookAt(new Vector3(0,4,3));var overview=overviewGo.AddComponent<Camera>();overview.fieldOfView=43;overview.farClipPlane=320;overview.backgroundColor=RenderSettings.fogColor;overview.clearFlags=CameraClearFlags.SolidColor;overview.enabled=false;overviewGo.AddComponent<UniversalAdditionalCameraData>();
        var surface=map.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.15f;
        Physics.SyncTransforms();surface.BuildNavMesh();if(surface.navMeshData==null)throw new Exception("NavMesh bake produced no data");AssetDatabase.CreateAsset(surface.navMeshData,"Assets/ZombieValley/ZombieValley_NavMesh.asset");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        var view=SceneView.lastActiveSceneView;if(view!=null){view.pivot=new Vector3(0,4,3);view.rotation=overviewGo.transform.rotation;view.size=105;view.orthographic=false;view.Repaint();}
        var report=new{scene=ScenePath,sourceModules=factory.Parts.Count,activeStaticRenderers=baker.RendererCount,staticTriangles=baker.Triangles,colliders=map.GetComponentsInChildren<Collider>().Length,buildings=10,entrances=plan.Entrances.Length,routes=traversal.Routes,navMeshSaved=surface.navMeshData!=null};
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath),"Concepts/ZombieValley/build-result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
}

public static class ZombieValleyAuthoringMenu
{
    [MenuItem("IT-FRIENDS/Zombie Valley/Build new scene")]
    public static void BuildNewScene() { new ZombieValleySceneBuilder().Build(); }
    [MenuItem("IT-FRIENDS/Zombie Valley/Apply edited source modules")]
    public static void ApplySourceEdits()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/ZombieValley.unity")throw new Exception("Open ZombieValley in Edit Mode");
        var map=scene.GetRootGameObjects().First(x=>x.name.StartsWith("Zombie Valley |"));
        var source=map.transform.Find("Editable source modules");
        var factory=new ValleyPrefabFactory(scene,map.transform,source);factory.RestoreSourceParts();
        var baker=new ValleyStaticMeshBaker(factory,scene);baker.Bake();
        var surface=map.GetComponent<NavMeshSurface>();surface.RemoveData();surface.BuildNavMesh();
        var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/ZombieValley/ZombieValley_NavMesh.asset");
        EditorUtility.CopySerialized(surface.navMeshData,saved);EditorUtility.SetDirty(saved);surface.RemoveData();surface.navMeshData=saved;surface.AddData();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath),"Concepts/ZombieValley/rebake-result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{sourceModules=factory.Parts.Count,activeStaticRenderers=baker.RendererCount,staticTriangles=baker.Triangles,navMeshSaved=true}));
    }
}
}
