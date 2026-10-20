using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
public sealed class RockShelf
{
    public readonly string Name;
    public readonly float Height;
    public readonly Vector2[] Outline;
    public RockShelf(string name,float height,params float[] coordinates)
    {Name=name;Height=height;Outline=Enumerable.Range(0,coordinates.Length/2).Select(i=>new Vector2(coordinates[i*2],coordinates[i*2+1])).ToArray();}
    public float SignedDistance(Vector2 p)
    {
        bool inside=false;float distance=float.MaxValue;
        for(int i=0,j=Outline.Length-1;i<Outline.Length;j=i++)
        {
            var a=Outline[j];var b=Outline[i];var d=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
            distance=Mathf.Min(distance,Vector2.Distance(p,a+t*d));
            if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside?distance:-distance;
    }
}
public sealed class ValleyStairRoute
{
    public readonly string Name;public readonly Vector3 Low,High;public readonly float Width;
    public ValleyStairRoute(string name,Vector3 low,Vector3 high,float width=3.8f){Name=name;Low=low;High=high;Width=width;}
}
public sealed class OrganicValleyLayout
{
    public readonly RockShelf[] Shelves={
        new RockShelf("Central courtyard mesa",10,-32,-19,-30,-34,-11,-38,12,-34,31,-24,39,-8,32,5,28,21,11,26,-8,23,-26,11,-36,-5),
        new RockShelf("Western inhabited ledge",17,-70,-3,-65,-28,-46,-30,-39,-16,-39,5,-43,26,-60,29,-72,17),
        new RockShelf("Eastern inhabited ledge",17,40,-17,48,-29,66,-28,78,-10,75,16,60,32,42,27,38,11),
        new RockShelf("Northern broken citadel",18,-35,32,-17,29,11,32,37,36,37,55,15,60,-15,61,-40,51),
        new RockShelf("Highest watch settlement",25,-10,62,12,57,28,68,21,82,-9,83,-19,72),
        new RockShelf("Southwest lower village",7,-62,-47,-52,-61,-35,-57,-30,-45,-36,-33,-54,-34),
        new RockShelf("Southeast lower village",8,34,-45,41,-59,60,-54,67,-41,57,-30,38,-33),
        new RockShelf("Western wild rock spur",38,-148,24,-141,-14,-123,-22,-109,-8,-105,20,-117,53,-135,56),
        new RockShelf("Eastern wild rock spur",43,113,29,124,13,144,18,157,48,146,82,117,90,106,64),
        new RockShelf("Southwest broken cliffs",29,-125,-104,-111,-128,-88,-123,-76,-103,-85,-84,-111,-84),
        new RockShelf("Southeast stone outcrop",26,103,-101,114,-122,137,-110,140,-91,127,-76,108,-82),
        new RockShelf("Northern gorge continuation",49,-50,119,-28,104,3,114,18,135,0,157,-28,160,-52,144)
    };
    public readonly List<ValleyStairRoute> Stairs=new List<ValleyStairRoute>();
    public OrganicValleyLayout()
    {
        Add("South courtyard climb",0,-62,0,-32,5);
        Add("Southwest village climb",-76,-42,-44,-39);
        Add("Southeast village climb",84,-52,62,-37);
        Add("West plaza ascent",-22,-7,-45,-7);
        Add("East plaza ascent",24,-26,52,-24);
        Add("North courtyard ascent",-12,17,-12,38,4.5f);
        Add("West valley ascent",-93,8,-64,8);
        Add("East valley ascent",98,10,68,10);
        Add("North valley ascent",56,78,30,49);
        Stairs.Add(new ValleyStairRoute("Upper western climb",new Vector3(-19,18,41),new Vector3(-11,25,59.25f)));
        Add("Upper eastern climb",21,47,19,68);
        Stairs.Add(new ValleyStairRoute("West balcony access",new Vector3(-19,10,-7),new Vector3(-28,17,6.25f),3.5f));
        Stairs.Add(new ValleyStairRoute("East balcony access",new Vector3(18,10,-3),new Vector3(30,17,10.25f),3.5f));
    }
    private void Add(string name,float ax,float az,float bx,float bz,float width=3.8f)
    {Stairs.Add(new ValleyStairRoute(name,new Vector3(ax,UncutHeight(ax,az),az),new Vector3(bx,UncutHeight(bx,bz),bz),width));}
    public float UncutHeight(float x,float z)
    {
        float h=.35f+Mathf.PerlinNoise((x+711)*.018f,(z+519)*.018f)*.8f;
        foreach(var shelf in Shelves)
        {
            float edge=shelf.SignedDistance(new Vector2(x,z));
            float jag=.5f*(Mathf.PerlinNoise((x+331)*.19f,(z+921)*.19f)-.5f);
            float t=Mathf.Clamp01((edge+jag+2.8f)/3.4f);
            h=Mathf.Max(h,Mathf.Lerp(h,shelf.Height,t*t*(3-2*t)));
        }
        // Independent overlapping ridges continue for kilometres beyond the village.
        float mountain=0;
        var peaks=new[]{new Vector4(-230,100,120,72),new Vector4(235,165,158,87),new Vector4(-95,310,185,98),new Vector4(350,-190,142,95),new Vector4(-340,-205,168,101),new Vector4(160,-455,205,125),new Vector4(-415,450,265,144),new Vector4(510,350,240,150),new Vector4(-720,-380,320,210),new Vector4(810,-400,290,205),new Vector4(30,860,390,240),new Vector4(-730,880,360,230),new Vector4(850,930,395,255)};
        foreach(var p in peaks)
        {
            float dx=(x-p.x)/p.w,dz=(z-p.y)/(p.w*.75f);
            mountain+=p.z*Mathf.Exp(-(dx*dx+dz*dz)*1.25f);
        }
        float distance=new Vector2(x,z).magnitude;
        float outer=Mathf.SmoothStep(0,1,Mathf.InverseLerp(105,185,distance));
        float noise= .78f+Mathf.PerlinNoise((x+813)*.011f,(z+375)*.011f)*.35f+Mathf.PerlinNoise((x+920)*.049f,(z+299)*.049f)*.12f;
        float rolling=Mathf.PerlinNoise((x+720)*.023f,(z+820)*.023f)*12*Mathf.SmoothStep(0,1,Mathf.InverseLerp(95,190,distance));
        return Mathf.Max(h+rolling,mountain*noise*outer);
    }
    public float Height(float x,float z)
    {
        float height=UncutHeight(x,z);var p=new Vector2(x,z);
        foreach(var s in Stairs)
        {
            var a=new Vector2(s.Low.x,s.Low.z);var d=new Vector2(s.High.x-s.Low.x,s.High.z-s.Low.z);
            float t=Vector2.Dot(p-a,d)/d.sqrMagnitude;if(t<0||t>1)continue;
            float distance=Vector2.Distance(p,a+d*t);
            if(distance<s.Width*.5f+1.2f)
            {
                float profile=Mathf.Lerp(s.Low.y,s.High.y,t)-.12f;
                float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.Width*.5f+.1f,s.Width*.5f+1.2f,distance));
                height=Mathf.Lerp(height,Mathf.Min(height,profile),blend);
            }
        }
        return height;
    }
}

public sealed class OrganicValleyTerrain
{
    private readonly OrganicValleyLayout layout;
    private readonly Transform root;
    private readonly Material[] materials;
    private int index;
    public int Triangles;
    public OrganicValleyTerrain(OrganicValleyLayout plan,Transform parent)
    {
        layout=plan;root=new GameObject("Continuous rocky valley | 2048 metres").transform;root.SetParent(parent,false);
        const string palettePath="Assets/ZombieValley/Materials/Organic_Palette.asset";
        var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
        if(palette==null){palette=new Texture2D(3,1,TextureFormat.RGBA32,false){name="Valley ground rock grass palette"};AssetDatabase.CreateAsset(palette,palettePath);}
        palette.SetPixels(new[]{new Color(.57f,.52f,.40f),new Color(.43f,.45f,.46f),new Color(.47f,.50f,.35f)});palette.filterMode=FilterMode.Point;palette.wrapMode=TextureWrapMode.Clamp;palette.Apply();EditorUtility.SetDirty(palette);
        var material=Material("Surface",Color.white);material.SetTexture("_BaseMap",palette);materials=new[]{material};
    }
    private Material Material(string name,Color color)
    {
        string path="Assets/ZombieValley/Materials/Organic_"+name.Replace(" ","_")+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",0);EditorUtility.SetDirty(m);return m;
    }
    public void Build()
    {
        // Fine geometry and collision near the playable shelves; coarse distant geography.
        for(int x=-160;x<160;x+=32)for(int z=-160;z<160;z+=32)Chunk(x,z,32,2,true);
        for(int x=-1024;x<1024;x+=256)for(int z=-1024;z<1024;z+=256)
            Chunk(x,z,256,16,false,true);
    }
    private Vector3 Vertex(float x,float z,float step)
    {
        // Boundary coordinates stay fixed so neighbouring chunks share an identical seam.
        bool boundary=Mathf.Abs(x%32)<.01f||Mathf.Abs(z%32)<.01f||step>2;
        float ox=boundary?0:(Mathf.PerlinNoise(x*.7f+621,z*.7f+911)-.5f)*.85f;
        float oz=boundary?0:(Mathf.PerlinNoise(x*.7f+381,z*.7f+451)-.5f)*.85f;
        return new Vector3(x+ox,layout.Height(x+ox,z+oz),z+oz);
    }
    private void Chunk(int x,int z,int size,int step,bool collision,bool omitCentre=false)
    {
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();var colors=new List<Color>();
        var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
        Action<Vector3,Vector3,Vector3> face=(a,b,c)=>{
            var normal=Vector3.Cross(b-a,c-a).normalized;var centre=(a+b+c)/3;
            int material=normal.y<.65f||centre.y>65?1:Mathf.PerlinNoise((centre.x+471)*.056f,(centre.z+112)*.056f)>.68f?2:0;
            int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            foreach(var p in new[]{a,b,c}){uvs.Add(new Vector2((material+.5f)/3,.5f));colors.Add(Color.white);}
            triangles[material].Add(first);triangles[material].Add(first+1);triangles[material].Add(first+2);Triangles++;
        };
        for(int ix=x;ix<x+size;ix+=step)for(int iz=z;iz<z+size;iz+=step)
        {
            if(omitCentre&&ix>=-160&&ix<160&&iz>=-160&&iz<160)continue;
            var a=Vertex(ix,iz,step);var b=Vertex(ix+step,iz,step);var c=Vertex(ix+step,iz+step,step);var d=Vertex(ix,iz+step,step);
            if(((ix/step+iz/step)&1)==0){face(a,d,b);face(b,d,c);}else{face(a,d,c);face(a,c,b);}
        }
        if(vertices.Count==0)return;
        var mesh=new Mesh{name="Organic valley terrain "+index,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.subMeshCount=1;
        mesh.SetTriangles(triangles.SelectMany(t=>t).ToList(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path="Assets/ZombieValley/GeneratedMeshes/landscape_"+(index++).ToString("000")+".asset";
        mesh=ValleyMeshAsset.Store(mesh,path);
        var go=new GameObject((collision?"Near collision land ":"Distant mountain land ")+x+" "+z);go.transform.SetParent(root,false);go.isStatic=true;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=collision?ShadowCastingMode.On:ShadowCastingMode.Off;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
}

public static class OrganicValleyAuthoring
{
    private const string ScenePath="Assets/Scenes/ZombieValley.unity";
    private static string Output(string name){return Path.Combine(Path.GetDirectoryName(Application.dataPath),"Concepts/ZombieValley/"+name);}
    private static void Guard()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode");
        var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new Exception("Open the generated ZombieValley scene");
        if(scene.isDirty)throw new Exception("Save the scene before rebuilding");
    }
    [MenuItem("IT-FRIENDS/Zombie Valley/Rework 1 - Organic landscape")]
    public static void BuildLandscape()
    {
        Guard();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var map=new GameObject("Zombie Valley | Selected concept 01");var layout=new OrganicValleyLayout();
        var terrain=new OrganicValleyTerrain(layout,map.transform);terrain.Build();
        var lightGo=new GameObject("Valley daylight");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.color=new Color(1,.94f,.82f);light.shadows=LightShadows.Soft;lightGo.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=light;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.57f,.64f,.73f);RenderSettings.ambientEquatorColor=new Color(.38f,.42f,.43f);RenderSettings.ambientGroundColor=new Color(.22f,.20f,.17f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=350;RenderSettings.fogEndDistance=1450;RenderSettings.fogColor=new Color(.58f,.67f,.75f);
        var player=GameObject.CreatePrimitive(PrimitiveType.Capsule);player.name="Player | existing PlayerController";player.transform.position=new Vector3(0,layout.Height(0,-74)+1.06f,-74);
        var rb=player.AddComponent<Rigidbody>();rb.mass=1;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.constraints=RigidbodyConstraints.FreezeRotation;
        var pc=player.AddComponent<PlayerController>();pc.moveSpeed=7;pc.jumpForce=6;pc.firstPersonPosition=new Vector3(0,.65f,0);pc.thirdPersonOffset=new Vector3(0,2.2f,-4.5f);
        var camera=Camera("Player Camera",new Vector3(0,layout.Height(0,-74)+1.7f,-74),new Vector3(0,15,0),75,true);camera.gameObject.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();
        Camera("Concept Overview Camera | disabled during play",new Vector3(111,90,-128),new Vector3(0,12,12),40,false);
        Save(scene);Report("landscape-result.json",new{phase="landscape",terrainTriangles=terrain.Triangles,shelfHeights=layout.Shelves.Select(x=>x.Height).ToArray(),worldWidthMetres=2048});
    }
    private static Camera Camera(string name,Vector3 position,Vector3 lookAt,float fov,bool enabled)
    {
        var go=new GameObject(name);go.transform.position=position;go.transform.LookAt(lookAt);var camera=go.AddComponent<Camera>();camera.fieldOfView=fov;camera.nearClipPlane=.07f;camera.farClipPlane=1600;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;camera.enabled=enabled;go.AddComponent<UniversalAdditionalCameraData>();return camera;
    }
    private static void Save(Scene scene){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();}
    private static void Report(string file,object data){File.WriteAllText(Output(file),Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));}
    public static void BuildSettlement()
    {
        Guard();var scene=SceneManager.GetActiveScene();var map=scene.GetRootGameObjects().First(x=>x.name.StartsWith("Zombie Valley |"));
        if(map.transform.Find("Editable source modules")!=null)throw new Exception("Settlement already exists");
        var plan=new OrganicValleyLayout();var f=new ValleyPrefabFactory(scene,map.transform);f.Sources.gameObject.tag="EditorOnly";var traversal=new ValleyTraversalBuilder(f);
        foreach(var s in plan.Stairs)traversal.Stairs(s.Name,s.Low,s.High,s.Width);
        traversal.Deck("West courtyard balcony",new Rect(-35,6,11,9),17);
        traversal.Deck("East courtyard balcony",new Rect(25,10,12,8),17);
        traversal.Deck("Northern watch balcony",new Rect(-22,59,40,5),25);
        traversal.Deck("Collapsed hall west gallery",new Rect(-10,-1,7,5),13);
        traversal.Deck("Collapsed hall east gallery",new Rect(3,-4,7,5),13);
        traversal.Stairs("Courtyard ruin west climb",new Vector3(-7,10,-10),new Vector3(-7,13,-1),3);
        traversal.Stairs("Courtyard ruin east climb",new Vector3(7,10,8),new Vector3(7,13,1.05f),3);
        traversal.Bridge("Broken hall jump shortcut",new Vector3(-3,13,1),new Vector3(3,13,-1),2.5f,.95f);
        traversal.Bridge("West full suspended bridge",new Vector3(-46,17,15),new Vector3(-30,17,12),3.6f);
        traversal.Bridge("East jump suspended bridge",new Vector3(32,17,15),new Vector3(46,17,19),3.6f,1.35f);
        traversal.Bridge("Western northern bridge",new Vector3(-48,17,23),new Vector3(-31,18,39),3.5f);
        traversal.Bridge("Eastern northern bridge",new Vector3(53,17,25),new Vector3(32,18,42),3.5f);
        traversal.Bridge("Southwest broken bridge",new Vector3(-28,10,-20),new Vector3(-39,7,-38),3.6f,4.1f);
        traversal.Bridge("Southeast descending bridge",new Vector3(28,10,-19),new Vector3(45,8,-36),3.6f);
        var houses=new ValleySettlementBuilder(f);
        houses.House("West gate cottage",new Vector3(-55,17,-17),8,6,-73);
        houses.House("Western balcony workshop",new Vector3(-56,17,17),8,8,15,true);
        houses.House("East cliff cottage",new Vector3(61,17,-17),6,8,62);
        houses.House("East ruined workshop",new Vector3(64,17,19),8,6,132,true);
        houses.House("Northwest terrace cottage",new Vector3(-27,18,46),6,6,22);
        houses.House("Northern roof route cottage",new Vector3(17,18,42),8,6,-14);
        houses.House("Highest watch cottage",new Vector3(4,25,73),8,8,-12);
        houses.House("Southwest lower cottage",new Vector3(-47,7,-51),6,6,-42);
        houses.House("Southeast lower cottage",new Vector3(53,8,-48),6,8,31);
        houses.House("Courtyard west workshop",new Vector3(-22,10,-20),6,6,-17,true);
        houses.House("Courtyard east shelter",new Vector3(25,10,-12),6,8,104,true);
        houses.House("Old north court cottage",new Vector3(3,10,15),6,6,11);
        Ruins(f,plan);Dressing(f,plan);RockDetails(f,plan);
        var baker=new ValleyStaticMeshBaker(f,scene);baker.Bake();Save(scene);
        Report("organic-settlement-result.json",new{phase="settlement",buildings=12,sourceModules=f.Parts.Count,staticRenderers=baker.RendererCount,staticTriangles=baker.Triangles,routes=traversal.Routes});
    }
    private static void Ruins(ValleyPrefabFactory f,OrganicValleyLayout layout)
    {
        var root=f.Group("04 Irregular ruined courtyards",f.Sources);
        Action<Vector3,float,float> arch=(p,yaw,height)=>{
            var q=Quaternion.Euler(0,yaw,0);f.Fit("A21_Ruin_Arch_Half",root,p+q*new Vector3(-1,0,0),new Vector3(2,height,.55f),yaw);f.Fit("A21_Ruin_Arch_Half",root,p+q*new Vector3(1,0,0),new Vector3(2,height,.55f),yaw+180);
        };
        // A collapsed courtyard and a larger roofless hall leave several crossing routes.
        var walls=new[]{new Vector4(-10,-15,20,1.4f),new Vector4(-7,-14,20,2.1f),new Vector4(-4,-13,20,1.2f),new Vector4(8,-8,-12,1.4f),new Vector4(11,-9,-12,2.4f),new Vector4(14,-10,-12,2),new Vector4(-14,3,83,3),new Vector4(-14,6,83,2),new Vector4(-13,9,83,3),new Vector4(-7,11,0,3.5f),new Vector4(-4,11,0,3),new Vector4(14,4,100,1.4f),new Vector4(15,1,100,1.8f)};
        foreach(var w in walls)f.Fit("A18_Ruin_Wall_2m",root,new Vector3(w.x,10,w.y),new Vector3(2.9f,w.w,.55f),w.z);
        for(int i=0;i<4;i++)
        {
            f.Fit("A18_Ruin_Wall_2m",root,new Vector3(-10.2f,10,-.2f+i*2.0f),new Vector3(2,2.3f+(i%2)*.5f,.5f),90);
            f.Fit("A18_Ruin_Wall_2m",root,new Vector3(10.2f,10,-3.2f+i*2.0f),new Vector3(2,2.3f+(i%3)*.3f,.5f),90);
        }
        for(int i=0;i<3;i++)f.Fit("A18_Ruin_Wall_2m",root,new Vector3(-9+i*2,10,4),new Vector3(2,2.6f,.5f));
        foreach(var deck in new[]{new Rect(-35,6,11,9),new Rect(25,10,12,8),new Rect(-22,59,40,5)})
        {
            float h=deck.width>25?25:17;
            foreach(float x in new[]{deck.x+1.2f,deck.xMax-1.2f})foreach(float z in new[]{deck.y,deck.yMax})
                f.Fit("D03_Fence_Pickets_2m",root,new Vector3(x,h,z),new Vector3(2.3f,1.1f,.18f));
        }
        arch(new Vector3(-.5f,10,11),0,3.8f);arch(new Vector3(11,10,-15),-12,3.7f);
        for(int i=0;i<4;i++)f.Fit("A19_Ruin_Corner",root,new Vector3(-26+i*6,18,53),new Vector3(3.4f,3.4f,3),i*71);
        f.Fit("A22_Rubble_Cluster",root,new Vector3(-8,10,7),new Vector3(2.5f,.5f,2.5f),40);
        var entrances=new[]{new Vector2(0,-88),new Vector2(-89,-51),new Vector2(91,-50),new Vector2(-102,8),new Vector2(108,10),new Vector2(-70,63),new Vector2(73,67),new Vector2(26,97)};
        for(int i=0;i<entrances.Length;i++)
        {
            var e=entrances[i];var p=new Vector3(e.x,layout.Height(e.x,e.y),e.y);arch(p,i==3||i==4?90:i*17,4.4f);
            var marker=f.Group("Future enemy entrance "+(i+1),f.Map);marker.position=p;
        }
        var spawn=f.Group("Player spawn",f.Map);spawn.position=new Vector3(0,layout.Height(0,-74)+1.06f,-74);
    }
    private static void Dressing(ValleyPrefabFactory f,OrganicValleyLayout layout)
    {
        var root=f.Group("05 Scattered salvage and ledge details",f.Sources);
        var points=new[]{new Vector2(-18,-29),new Vector2(-15,-26),new Vector2(20,-24),new Vector2(26,4),new Vector2(-24,3),new Vector2(-5,-7),new Vector2(7,-17),new Vector2(-54,-10),new Vector2(-60,22),new Vector2(53,-6),new Vector2(69,18),new Vector2(-28,40),new Vector2(24,41),new Vector2(-8,75),new Vector2(13,75),new Vector2(-55,-48),new Vector2(57,-40)};
        for(int i=0;i<points.Length;i++)
        {
            var p=points[i];float h=layout.Height(p.x,p.y);
            f.Fit(i%3==0?"Barrel_Closed":"Crate_Closed",root,new Vector3(p.x,h,p.y),i%3==0?new Vector3(.9f,1.1f,.9f):new Vector3(1.2f,1,1.2f),i*31);
            if(i%4==0)f.Fit("Crate_Closed",root,new Vector3(p.x+1.3f,h,p.y+.4f),new Vector3(1,.8f,1),i*13);
        }
        f.Fit("N07_Log",root,new Vector3(5,10,-25),new Vector3(.6f,.5f,3),72);
        var trees=f.Group("Sparse trees with original LOD",f.Map);
        var treePoints=new[]{new Vector2(-28,-30),new Vector2(29,12),new Vector2(-62,1),new Vector2(69,-8),new Vector2(-27,53),new Vector2(28,53),new Vector2(-11,76),new Vector2(-51,-39),new Vector2(59,-37),new Vector2(-78,-70),new Vector2(80,-78),new Vector2(-110,-23),new Vector2(115,-29),new Vector2(-100,52),new Vector2(118,59),new Vector2(-30,103),new Vector2(12,-98),new Vector2(-43,-100),new Vector2(51,-98)};
        for(int i=0;i<treePoints.Length;i++){var p=treePoints[i];f.Tree(i%2==0?"GR_Tree_Broad":"GR_Tree_Slim",trees,new Vector3(p.x,layout.Height(p.x,p.y),p.y),1.2f+(i%3)*.16f);}
    }
    private static void RockDetails(ValleyPrefabFactory f,OrganicValleyLayout layout)
    {
        var root=f.Group("06 Geological buttresses and background crags",f.Sources);var random=new System.Random(77);
        foreach(var shelf in layout.Shelves)
        {
            for(int i=0;i<shelf.Outline.Length;i++)
            {
                var a=shelf.Outline[i];var b=shelf.Outline[(i+1)%shelf.Outline.Length];int pieces=Mathf.CeilToInt(Vector2.Distance(a,b)/8);
                for(int j=0;j<pieces;j++)
                {
                var p=Vector2.Lerp(a,b,(j+.2f+(float)random.NextDouble()*.55f)/pieces);
                if(layout.Stairs.Any(s=>{var low=new Vector2(s.Low.x,s.Low.z);var d=new Vector2(s.High.x-s.Low.x,s.High.z-s.Low.z);return Vector2.Distance(p,low+d*Mathf.Clamp01(Vector2.Dot(p-low,d)/d.sqrMagnitude))<5;}))continue;
                float w=6+(float)random.NextDouble()*5,h=shelf.Height*(.60f+(float)random.NextDouble()*.27f);
                f.Fit("N01_Rock_Boulder",root,new Vector3(p.x,shelf.Height-h-1,p.y),new Vector3(w,h,6+(float)random.NextDouble()*4),Quaternion.Euler(random.Next(-17,18),random.Next(360),random.Next(-14,15)));
                if(shelf.Height>27)f.Fit("N01_Rock_Boulder",root,new Vector3(p.x,.2f,p.y),new Vector3(w*1.3f,shelf.Height*.65f,10),Quaternion.Euler(random.Next(-12,13),random.Next(360),random.Next(-12,13)));
                }
            }
        }
        var crags=new[]{new Vector3(-177,0,54),new Vector3(-208,0,99),new Vector3(-194,0,154),new Vector3(161,0,125),new Vector3(207,0,178),new Vector3(265,0,130),new Vector3(-89,0,222),new Vector3(22,0,262),new Vector3(85,0,241),new Vector3(-200,0,-155),new Vector3(260,0,-145),new Vector3(-331,0,306)};
        for(int i=0;i<crags.Length;i++)
        {
            var p=crags[i];float height=22+random.Next(29),width=27+random.Next(25);p.y=layout.Height(p.x,p.z)-height*.65f;
            f.Fit("N01_Rock_Boulder",root,p,new Vector3(width,height,width*.8f),random.Next(360),false);
        }
        for(int i=0;i<44;i++)
        {
            float x=random.Next(-100,101),z=random.Next(-78,91);var p=new Vector3(x,layout.Height(x,z),z);
            if(layout.Stairs.Any(s=>Vector3.Distance(s.Low,p)<6||Vector3.Distance(s.High,p)<6))continue;
            f.Fit("N05_Rock_Slab",root,p,new Vector3(1.4f+(float)random.NextDouble()*1.5f,.25f,1.2f),random.Next(360));
        }
    }
    public static void BakeNavigation()
    {
        Guard();var scene=SceneManager.GetActiveScene();var map=scene.GetRootGameObjects().First(x=>x.name.StartsWith("Zombie Valley |"));
        var surface=map.GetComponent<NavMeshSurface>()??map.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(0,40,8);surface.size=new Vector3(235,90,230);surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.15f;
        surface.RemoveData();Physics.SyncTransforms();surface.BuildNavMesh();var data=surface.navMeshData;if(data==null)throw new Exception("No navigation data");
        const string path="Assets/ZombieValley/ZombieValley_NavMesh.asset";var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(saved!=null){EditorUtility.CopySerialized(data,saved);EditorUtility.SetDirty(saved);surface.RemoveData();surface.navMeshData=saved;surface.AddData();}else AssetDatabase.CreateAsset(data,path);
        Save(scene);Report("organic-nav-result.json",new{saved=true});
    }
}

public static class ValleyMeshAsset
{
    public static Mesh Store(Mesh source,string path)
    {
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(source,path);return source;}
        // Mesh setters update native vertex/index buffers; serialized copying leaves stale GPU data.
        saved.Clear();saved.indexFormat=source.indexFormat;saved.name=source.name;
        saved.vertices=source.vertices;saved.normals=source.normals;saved.tangents=source.tangents;
        saved.uv=source.uv;saved.uv2=source.uv2;saved.colors=source.colors;saved.subMeshCount=source.subMeshCount;
        for(int i=0;i<source.subMeshCount;i++)saved.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);
        saved.bounds=source.bounds;saved.UploadMeshData(false);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(source);return saved;
    }
}
}
