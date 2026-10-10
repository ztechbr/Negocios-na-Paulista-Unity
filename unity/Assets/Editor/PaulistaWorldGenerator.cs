// Paulista Business World: Unity editor procedural prototype.
// Place under Assets/Editor in a VRChat Creator Companion Worlds project.
// Menu: Tools > Paulista Business World > Generate MVP Scene
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public static class PaulistaWorldGenerator {
    static readonly string[] Names={"Banco Horizonte","Vida Sao Paulo","Lex & Partners","Paulista Retail","Nexa Analytics","Nova Energia","Conecta Telecom","Capital Seguros","MetroLog","Educa Digital","BioLab Paulista","Urban Places"};
    static readonly string[] Sectors={"Financeiro","Saude","Juridico","Varejo","Tecnologia","Energia","Telecom","Seguros","Logistica","Educacao","Industria","Imobiliario"};
    static Material MakeMat(string name,Color color){var m=new Material(Shader.Find("Standard"));m.name=name;m.color=color;return m;}
    static GameObject Block(Transform parent,string name, Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.localPosition=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    static GameObject Sphere(Transform parent,string name,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(parent);g.transform.localPosition=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    static void Sign(Transform parent,string title,Vector3 p,float fontSize=1.1f){var go=new GameObject("LABEL - "+title);go.transform.SetParent(parent);go.transform.localPosition=p;var t=go.AddComponent<TextMesh>();t.text=title;t.fontSize=70;t.characterSize=.028f*fontSize;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;go.transform.localRotation=Quaternion.Euler(0,180,0);}
    [MenuItem("Tools/Paulista Business World/Generate MVP Scene")]
    public static void Generate(){
        string scenePath="Assets/PaulistaBusinessWorld/Scenes";Directory.CreateDirectory(scenePath);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("PAULISTA BUSINESS WORLD - MVP").transform;
        var asphalt=MakeMat("Asfalto",new Color(.13f,.18f,.23f)),sidewalk=MakeMat("Calcada",new Color(.72f,.75f,.74f)),green=MakeMat("Arvores",new Color(.20f,.50f,.30f)),trunk=MakeMat("Troncos",new Color(.35f,.27f,.21f)),white=MakeMat("Faixas",Color.white),glass=MakeMat("Janelas",new Color(.45f,.78f,.91f)),light=MakeMat("Concreto",new Color(.42f,.54f,.64f)),red=MakeMat("Museu vermelho",new Color(.83f,.21f,.17f));
        Block(root,"Piso",new Vector3(0,-.2f,0),new Vector3(480,.3f,175),green);
        Block(root,"Avenida Paulista",new Vector3(0,0,0),new Vector3(460,.1f,20),asphalt);
        for(int side=-1;side<=1;side+=2){Block(root,"Calcada",new Vector3(0,.1f,side*14),new Vector3(460,.2f,8),sidewalk);
            for(int x=-210;x<=210;x+=14){Block(root,"Faixa rodoviaria",new Vector3(x,.08f,side*4.8f),new Vector3(6,.04f,.22f),white);
                Block(root,"Tronco",new Vector3(x,2.7f,side*21),new Vector3(.5f,5,.5f),trunk);
                Sphere(root,"Copa",new Vector3(x,6.2f,side*21),new Vector3(4,4.6f,4),green);
            }
        }
        for(int i=0;i<Names.Length;i++){
            int side=i%2==0?-1:1;int x=-145+i*26;float height=22+(i*11)%36;
            var building=new GameObject("EMPRESA "+Sectors[i]+" - "+Names[i]);building.transform.SetParent(root);
            Block(building.transform,"Edificio",new Vector3(x,height/2,side*38),new Vector3(20,height,19),MakeMat("Fachada "+i,Color.Lerp(new Color(.22f,.35f,.48f),light.color,(i%5)/6f)));
            for(float y=4;y<height-2;y+=4)for(int dx=-7;dx<9;dx+=4){Block(building.transform,"Janela",new Vector3(x+dx,y,side*28.42f),new Vector3(2.2f,1.8f,.14f),glass);}
            Sign(building.transform,Names[i]+" | "+Sectors[i],new Vector3(x,4.2f,side*27.9f),.7f);
        }
        // Abstract museum-inspired cultural node. Does not reproduce the actual architectural model.
        var museum=new GameObject("CULTURA - MUSEU ESTILIZADO");museum.transform.SetParent(root);
        foreach(int x in new[]{-10,10})foreach(int z in new[]{-3,3})Block(museum.transform,"Pilar vermelho",new Vector3(x,7,z-70),new Vector3(1.1f,14,1.1f),red);
        Block(museum.transform,"Volume suspenso",new Vector3(0,13,-70),new Vector3(28,9,15),light);Sign(museum.transform,"CULTURA",new Vector3(0,14,-62),1f);
        // Meeting hub has furniture. Voice chat and actual avatars are provided by the VRChat runtime.
        var hub=new GameObject("MEETING HUB - Sala para reunioes");hub.transform.SetParent(root);
        Block(hub.transform,"Piso da sala",new Vector3(185,.35f,-4),new Vector3(24,.7f,19),sidewalk);
        Block(hub.transform,"Mesa",new Vector3(185,2,-4),new Vector3(9,.35f,4),trunk);
        for(int j=0;j<8;j++){float a=j*Mathf.PI/4;Block(hub.transform,"Cadeira "+j,new Vector3(185+Mathf.Cos(a)*6,1,-4+Mathf.Sin(a)*4.5f),new Vector3(1.6f,1.7f,1.5f),light);}
        foreach(int x in new[]{173,197})foreach(int z in new[]{-13,5})Block(hub.transform,"Pilar",new Vector3(x,4.6f,z),new Vector3(.5f,8,.5f),light);
        Block(hub.transform,"Cobertura",new Vector3(185,9,-4),new Vector3(25,.6f,20),light);Sign(hub.transform,"MEETING HUB",new Vector3(185,10,-4));
        var sun=new GameObject("Directional Light");var l=sun.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
        var spawn=new GameObject("SPAWN - attach VRC Spawn prefab / Scene Descriptor here");spawn.transform.position=new Vector3(-165,1,0);spawn.transform.rotation=Quaternion.Euler(0,90,0);
        // VRChat requires a VRCSceneDescriptor. Set it up using the SDK's official VRCWorld prefab.
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene,scenePath+"/PaulistaBusinessWorld.unity");
        EditorUtility.DisplayDialog("Paulista Business World","Scene generated successfully. Add the VRChat VRCWorld prefab / VRCSceneDescriptor and configure its spawn points before testing or publishing.","OK");
    }
}
