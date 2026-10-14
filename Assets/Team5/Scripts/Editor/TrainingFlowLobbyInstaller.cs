#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        [MenuItem("Team 5/Training Flow/2 Create Or Refresh Lobby")]
        public static void BuildLobby()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=DemoPath)throw new InvalidOperationException("Build the lobby from DemoScene in Edit Mode.");
            var demo=SceneManager.GetActiveScene();if(demo.isDirty)throw new InvalidOperationException("Save DemoScene before creating the lobby.");
            var sourceSystems=Named("00_XR_SYSTEMS");
            var lobby=System.IO.File.Exists(LobbyPath)?EditorSceneManager.OpenScene(LobbyPath,OpenSceneMode.Additive):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(lobby);
            if(Named("00_XR_SYSTEMS")==null)
            {
                var systems=UnityEngine.Object.Instantiate(sourceSystems.gameObject);systems.name="00_XR_SYSTEMS";
                SceneManager.MoveGameObjectToScene(systems,lobby);
                foreach(var probe in systems.GetComponentsInChildren<PPEHandProbe>(true))UnityEngine.Object.DestroyImmediate(probe.gameObject);
                foreach(var wear in systems.GetComponentsInChildren<CoatWearZone>(true))UnityEngine.Object.DestroyImmediate(wear.gameObject);
            }
            var origin=Named("XR Origin (XR Rig)");origin.position=Vector3.zero;origin.rotation=Quaternion.identity;
            var root=Named("LOBBY_ENVIRONMENT")?.gameObject??new GameObject("LOBBY_ENVIRONMENT");
            var floorMaterial=TrainingMaterial("M_LobbyFloor",new Color(.83f,.9f,.89f),0,.35f);
            Primitive(root.transform,"Reference_Floor",PrimitiveType.Cylinder,new Vector3(0,-.1f,0),new Vector3(7,.1f,7),floorMaterial,true);
            foreach(var old in new[]{"Practice_Table","Practice_Table_Leg","Practice_Grab_Object"})
                if(root.transform.Find(old)!=null)root.transform.Find(old).gameObject.SetActive(false);
            var teal=TrainingMaterial("M_LobbyTeal",Teal);
            var dna=Child(root.transform,"Slow_DNA_Decoration");dna.transform.position=new Vector3(1.7f,1.65f,2.65f);Ensure<SlowDnaRotation>(dna);
            for(var strand=0;strand<2;strand++)
            {
                var line=Ensure<LineRenderer>(Child(dna.transform,"Strand_"+strand));line.useWorldSpace=false;line.positionCount=60;line.startWidth=line.endWidth=.025f;line.sharedMaterial=teal;
                for(var i=0;i<60;i++){var phase=i/59f*Mathf.PI*4f+strand*Mathf.PI;line.SetPosition(i,new Vector3(Mathf.Cos(phase)*.28f,i/59f*2.4f-1.2f,Mathf.Sin(phase)*.28f));}
            }
            for(var i=0;i<18;i++)
            {
                var phase=i/17f*Mathf.PI*4f;var a=new Vector3(Mathf.Cos(phase)*.28f,i/17f*2.4f-1.2f,Mathf.Sin(phase)*.28f);var b=new Vector3(-a.x,a.y,-a.z);
                var rung=Primitive(dna.transform,"Base_Pair_"+i,PrimitiveType.Cylinder,dna.transform.position+(a+b)/2,new Vector3(.018f,(b-a).magnitude/2,.018f),TrainingMaterial("M_LobbyBasePairs",new Color(.55f,.77f,.73f)));
                rung.transform.localPosition=(a+b)/2;rung.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }
            var light=Ensure<Light>(Child(root.transform,"Lobby_Key_Light"));light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(45,-30,0);light.shadows=LightShadows.None;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.8f,.8f);
            RenderSettings.skybox=null;Named("Main Camera").GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;Named("Main Camera").GetComponent<Camera>().backgroundColor=new Color(.86f,.93f,.92f);
            var audioRoot=Child(root.transform,"LOBBY_AUDIO");var audio=Ensure<AudioSource>(audioRoot);audio.playOnAwake=false;
            var narration=Ensure<NarrationController>(audioRoot);narration.Configure(audio,Array.Empty<TMP_Text>(),Array.Empty<TMP_Text>(),Array.Empty<Slider>(),Array.Empty<UnityEngine.UI.Toggle>(),Array.Empty<UnityEngine.UI.Button>(),BuildCues());
            var controller=Ensure<LobbyController>(root);
            var canvas=Canvas(root.transform,"Lobby_Welcome_Canvas",new Vector3(0,1.50f,1.65f),new Vector2(1000,900),.001f);
            Image(canvas.transform,"Background",Vector2.zero,new Vector2(1000,900),Paper);
            Text(canvas.transform,"Brand",new Vector2(0,383),new Vector2(880,100),44,"Welcome to the T5 XR\nGenomics Laboratory").alignment=TextAlignmentOptions.Center;
            var entry=Rect(canvas.transform,"Nickname_Entry",Vector2.zero,new Vector2(1000,680));
            Image(entry,"Input_Background",new Vector2(0,245),new Vector2(880,76),Soft);
            var name=Text(entry,"Nickname",new Vector2(0,245),new Vector2(840,60),32,"Enter your nickname");name.alignment=TextAlignmentOptions.Center;name.richText=false;
            var rows=new[]{"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"};
            for(var row=0;row<rows.Length;row++)for(var col=0;col<rows[row].Length;col++)
            {
                var letter=rows[row][col].ToString();var button=Button(entry,"Key_"+letter,new Vector2((col-(rows[row].Length-1)/2f)*88,135-row*79),new Vector2(76,64),letter);
                button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();UnityEventTools.AddStringPersistentListener(button.onClick,controller.AppendCharacter,letter);
            }
            var space=Button(entry,"Space",new Vector2(-165,-195),new Vector2(440,64),"Space");space.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();UnityEventTools.AddStringPersistentListener(space.onClick,controller.AppendCharacter," ");
            Click(Button(entry,"Backspace",new Vector2(190,-195),new Vector2(220,64),"Backspace"),controller.Backspace);
            Click(Button(entry,"Clear",new Vector2(375,-195),new Vector2(130,64),"Clear"),controller.ClearName);
            Click(Button(entry,"Continue",new Vector2(0,-276),new Vector2(880,64),"Continue"),controller.ConfirmNickname);
            var practice=Rect(canvas.transform,"Controls_Practice",Vector2.zero,new Vector2(1000,680));
            Text(practice,"Instructions",new Vector2(0,65),new Vector2(850,255),32,"Explore real-time PCR, one clear step at a time.\n\nFirst, prepare safely with your coat, handwashing and gloves. Then inspect, load and run your prepared plate.");
            if(practice.Find("Practice_Select")!=null)practice.Find("Practice_Select").gameObject.SetActive(false);
            var enter=Button(practice,"Enter_Laboratory",new Vector2(0,-230),new Vector2(850,64),"Enter Laboratory");enter.interactable=false;Click(enter,controller.EnterLaboratory);
            var feedback=Text(canvas.transform,"Feedback",new Vector2(0,-370),new Vector2(880,75),26,"Your nickname is used only for this session.");feedback.richText=false;
            Set(controller,"nicknameText",name);Set(controller,"feedbackText",feedback);Set(controller,"entryPanel",entry.gameObject);Set(controller,"welcomePanel",practice.gameObject);Set(controller,"enterButton",enter);Set(controller,"narration",narration);Set(controller,"transition",BuildFade(Named("Main Camera")));
            practice.gameObject.SetActive(false);
            BuildSettings(root.transform,narration,new Vector3(0,.94f,1.65f));
            RepairNavigation();
            EditorSceneManager.SaveScene(lobby,LobbyPath);SceneManager.SetActiveScene(demo);EditorSceneManager.CloseScene(lobby,true);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(LobbyPath,true),new EditorBuildSettingsScene(DemoPath,true)};
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
