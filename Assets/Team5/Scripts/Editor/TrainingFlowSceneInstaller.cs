#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        private const string DemoPath="Assets/Team5/Scenes/DemoScene.unity";
        private const string LobbyPath="Assets/Team5/Scenes/LobbyScene.unity";
        private static Transform Named(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&t.name==name);
        private static GameObject Child(Transform parent,string name)
        {var t=parent.Find(name);if(t!=null)return t.gameObject;var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        [MenuItem("Team 5/Training Flow/1 Install Flow In DemoScene")]
        public static void InstallDemo()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=DemoPath)throw new InvalidOperationException("Open DemoScene in Edit Mode first.");
            var lesson=UnityEngine.Object.FindFirstObjectByType<QpcrLessonController>(FindObjectsInactive.Include);
            if(lesson==null)throw new InvalidOperationException("Keep the existing qPCR lesson installed before adding the training flow.");
            var audioRoot=Named("QPCR_AUDIO");var narration=audioRoot.GetComponent<NarrationController>();
            var voice=audioRoot.GetComponents<AudioSource>().Last();
            narration.Configure(voice,Array.Empty<TMP_Text>(),Array.Empty<TMP_Text>(),Array.Empty<Slider>(),Array.Empty<Toggle>(),Array.Empty<UnityEngine.UI.Button>(),BuildCues());
            var system=Ensure<LabSessionCoordinator>(Child(Named("50_LESSON_AND_UI"),"LAB_SESSION_COORDINATOR"));
            var ppe=Named("PPE_LessonSystem").GetComponent<PPELessonController>();
            Set(lesson,"requiredPpe",ppe);Set(lesson,"beginOnStart",false);Set(ppe,"beginOnStart",false);Set(ppe,"rubHandsSeconds",20f);
            var card=Named("Plate_Instruction_Canvas");StyleQpcr(card,narration);
            var ppeCard=BuildPpe(ppe,narration);
            var fade=BuildFade(Named("Main Camera"));
            Set(system,"ppe",ppe);Set(system,"qpcr",lesson);Set(system,"ppeCard",ppeCard);Set(system,"qpcrCard",card.gameObject);
            Set(system,"origin",Named("XR Origin (XR Rig)"));Set(system,"ppeSpawn",new Vector3(5.15f,0f,-3.65f));Set(system,"spawnYaw",0f);Set(system,"transition",fade);
            Set(system,"continueButton",ppeCard.transform.Find("Continue_qPCR").GetComponent<UnityEngine.UI.Button>());
            Click(ppeCard.transform.Find("Continue_qPCR").GetComponent<UnityEngine.UI.Button>(),system.ContinueToQpcr);
            var indicator=Child(Named("Main Camera"),"Coat_Worn_Indicator");indicator.transform.localPosition=new Vector3(0,-.42f,.28f);
            var indicatorCanvas=Canvas(indicator.transform,"Coat_Status",indicator.transform.position,new Vector2(280,55),.0007f);
            indicatorCanvas.transform.localPosition=Vector3.zero;indicatorCanvas.transform.localRotation=Quaternion.identity;
            Image(indicatorCanvas.transform,"Background",Vector2.zero,new Vector2(280,55),Soft);
            Text(indicatorCanvas.transform,"Status",Vector2.zero,new Vector2(250,45),24,"Lab coat worn").alignment=TextAlignmentOptions.Center;
            Set(system,"coatWornIndicator",indicator.transform);indicator.SetActive(false);
            var recap=Canvas(Named("QPCR_STATION_UI"),"Session_Recap",new Vector3(2.45f,1.57f,2.94f),new Vector2(900,650),.00075f);
            Ensure<QpcrWorldSpacePanel>(recap);Image(recap.transform,"Background",Vector2.zero,new Vector2(900,650),Paper);
            Text(recap.transform,"Title",new Vector2(0,250),new Vector2(810,60),44,"You completed the lesson");
            var recapText=Text(recap.transform,"Recap",new Vector2(0,45),new Vector2(810,310),28,"");
            Click(Button(recap.transform,"Restart_qPCR",new Vector2(-190,-240),new Vector2(320,64),"Restart qPCR"),system.RestartQpcr);
            Click(Button(recap.transform,"Return_Lobby",new Vector2(190,-240),new Vector2(320,64),"Return to Lobby"),system.ReturnToLobby);
            Set(system,"recapPanel",recap);Set(system,"recapText",recapText);recap.SetActive(false);
            BuildVideo(lesson,narration,card);
            BuildSettings(Named("50_LESSON_AND_UI"),narration,new Vector3(4.9f,1.1f,-2.75f));
            AddReachFilters(ppe,lesson);
            RepairNavigation(); RepairPpeGeometry();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
        private static void StyleQpcr(Transform card,NarrationController narration)
        {
            card.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,700);card.localScale=Vector3.one*.00068f;
            card.position=new Vector3(2.35f,1.70f,3.06f);
            var ui=card.GetComponent<QpcrLessonUI>();
            Image(card,"Background",Vector2.zero,new Vector2(1000,700),Paper);
            Image(card,"Blue_Accent",new Vector2(-486,0),new Vector2(8,630),Teal);
            Text(card,"Lesson_Step",new Vector2(0,312),new Vector2(900,34),24,"qPCR guided lesson");
            Text(card,"Lesson_Title",new Vector2(0,254),new Vector2(900,60),44,"Start your qPCR lesson");
            Text(card,"Current_Target",new Vector2(0,197),new Vector2(900,40),28,"",Teal);
            Text(card,"Persistent_Instruction",new Vector2(0,122),new Vector2(900,105),28,"");
            Text(card,"Scientific_Explanation",new Vector2(0,15),new Vector2(900,100),28,"");
            Image(card,"Action_Hint_Background",new Vector2(0,-83),new Vector2(900,60),Soft);
            Text(card,"Action_Hint",new Vector2(0,-83),new Vector2(860,50),24,"").alignment=TextAlignmentOptions.Center;
            Text(card,"Temporary_Feedback",new Vector2(0,-150),new Vector2(900,60),24,"");
            Text(card,"Progress",new Vector2(-270,-221),new Vector2(360,40),24,"Plate inspection: 0 / 4");
            Button(card,"Start_qPCR_Lesson",new Vector2(170,-217),new Vector2(450,64),"Start qPCR Lesson");
            Button(card,"Confirm_Visual_Check",new Vector2(170,-217),new Vector2(450,64),"Confirm observation");
            Click(Button(card,"Replay_Narration",new Vector2(-35,-305),new Vector2(290,64),"Replay Instruction"),narration.ReplayInstruction);
            Click(Button(card,"Reset_qPCR_Lesson",new Vector2(370,-305),new Vector2(170,64),"Reset"),ui.ResetLesson);
            Click(Button(card,"Why_Button",new Vector2(-340,-305),new Vector2(270,64),"Why this step?"),ui.ToggleWhy);
            Click(Button(card,"Mute_Button",new Vector2(190,-305),new Vector2(140,64),"Mute"),narration.ToggleMuted);
            var why=Named("Why_This_Step");
            why.GetComponent<RectTransform>().sizeDelta=new Vector2(900,700);why.localScale=Vector3.one*.00068f;why.position=new Vector3(1.65f,1.7f,3.06f);
            Image(why,"Background",Vector2.zero,new Vector2(900,700),Paper);
            Text(why,"Heading",new Vector2(0,275),new Vector2(810,60),44,"Understand this step");
            Text(why,"Why_Text",new Vector2(0,40),new Vector2(810,360),28,"");
            Click(Button(why,"Read_Aloud",new Vector2(-210,-275),new Vector2(260,64),"Read Aloud"),ui.ReadExplanation);
            Click(Button(why,"Stop_Reading",new Vector2(90,-275),new Vector2(260,64),"Stop Reading"),ui.StopReading);
            Click(Button(why,"Close",new Vector2(310,275),new Vector2(130,64),"Close"),ui.ToggleWhy);
            why.gameObject.SetActive(false);
            var protocol=Named("QPCR_TOUCHSCREEN_V2").Find("Protocol_Panel");
            var screen=protocol.parent.GetComponent<QpcrTouchscreenController>();
            var page=Ensure<QpcrProtocolPages>(protocol.gameObject);
            var fields=new List<UnityEngine.Object>();
            var order=new[]{QpcrProtocolParameter.ReactionVolume,QpcrProtocolParameter.LidTemperature,QpcrProtocolParameter.InitialDenaturation,QpcrProtocolParameter.InitialHoldSeconds,
                QpcrProtocolParameter.AmplificationCycles,QpcrProtocolParameter.Denaturation,QpcrProtocolParameter.DenaturationSeconds,QpcrProtocolParameter.Annealing,
                QpcrProtocolParameter.AnnealingSeconds,QpcrProtocolParameter.Extension,QpcrProtocolParameter.ExtensionSeconds,QpcrProtocolParameter.FluorescenceAcquisition,
                QpcrProtocolParameter.MeltCurveStart,QpcrProtocolParameter.MeltCurveEnd,QpcrProtocolParameter.TemperatureIncrement,QpcrProtocolParameter.PostRunHold};
            foreach(QpcrProtocolParameter parameter in order)
            {
                var t=protocol.Find("Field_"+parameter);fields.Add(t.gameObject);
                var i=fields.Count-1;t.GetComponent<RectTransform>().anchoredPosition=new Vector2(-155,180-(i%4)*96);
                t.GetComponent<RectTransform>().sizeDelta=new Vector2(530,80);
                var image=t.GetComponent<UnityEngine.UI.Image>();image.sprite=Rounded();image.type=UnityEngine.UI.Image.Type.Sliced;
                Text(t,"Label",new Vector2(-90,8),new Vector2(320,64),27,QpcrProtocolValidator.LabelFor(parameter));
                Text(t,"Value",new Vector2(172,0),new Vector2(155,64),27,"--").alignment=TextAlignmentOptions.MidlineRight;
            }
            Objects(page,"fieldObjects",fields.ToArray());
            Set(page,"pageLabel",Text(protocol,"Page_Label",new Vector2(-160,-175),new Vector2(260,40),24,"Protocol 1 / 4",Color.white));
            var prev=Button(protocol,"Previous_Page",new Vector2(-355,-180),new Vector2(125,64),"Back");Click(prev,page.Previous);Set(page,"previous",prev);
            var next=Button(protocol,"Next_Page",new Vector2(45,-180),new Vector2(125,64),"Next");Click(next,page.Next);Set(page,"next",next);
            var keypad=protocol.Find("Numeric_Keypad");
            Set(page,"touchscreen",screen);Set(page,"keypad",keypad.gameObject);Set(page,"validate",protocol.Find("Validate_Protocol").GetComponent<UnityEngine.UI.Button>());
            var review=Rect(protocol,"Protocol_Review",new Vector2(-100,20),new Vector2(700,500));
            Image(review,"Background",Vector2.zero,new Vector2(700,500),Paper);
            var reviewText=Text(review,"Review_Text",new Vector2(0,10),new Vector2(650,470),21,"");
            Set(page,"reviewPanel",review.gameObject);Set(page,"reviewText",reviewText);review.gameObject.SetActive(false);
            Click(Button(protocol,"Edit_Protocol",new Vector2(-110,-180),new Vector2(280,64),"Edit Values"),screen.EditProtocol);
            // Only display Edit Values during review, so it does not overlap page navigation.
            var edit=protocol.Find("Edit_Protocol");edit.SetParent(review,false);edit.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-230);
            reviewText.GetComponent<RectTransform>().sizeDelta=new Vector2(650,435);reviewText.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,35);
            keypad.GetComponent<RectTransform>().anchoredPosition=new Vector2(294,20);
            foreach(var button in keypad.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {var r=button.GetComponent<RectTransform>();r.sizeDelta=new Vector2(r.sizeDelta.x,52);button.GetComponent<UnityEngine.UI.Image>().sprite=Rounded();button.GetComponent<UnityEngine.UI.Image>().type=UnityEngine.UI.Image.Type.Sliced;}
            Text(protocol,"Header",new Vector2(-215,285),new Vector2(440,45),32,"Configure qPCR",Color.white);
            var assay=protocol.Find("Assay_Label");if(assay!=null){assay.GetComponent<RectTransform>().anchoredPosition=new Vector2(-150,242);assay.GetComponent<TMP_Text>().fontSize=19;}
            var validation=protocol.Find("Validation");validation.GetComponent<RectTransform>().anchoredPosition=new Vector2(-130,-262);validation.GetComponent<RectTransform>().sizeDelta=new Vector2(540,85);validation.GetComponent<TMP_Text>().fontSize=22;
            // The final review action remains Validate Protocol and checks all 16 pages' fields.
        }
        private static TrainingSceneFade BuildFade(Transform camera)
        {
            var cover=Canvas(camera,"Training_Fade",camera.position,new Vector2(2000,2000),.001f);
            cover.transform.localPosition=new Vector3(0,0,.32f);cover.transform.localRotation=Quaternion.identity;
            cover.GetComponent<UnityEngine.Canvas>().sortingOrder=1000;
            Image(cover.transform,"Black",Vector2.zero,new Vector2(2000,2000),Color.black).sprite=null;
            var group=Ensure<CanvasGroup>(cover);group.alpha=0;group.blocksRaycasts=false;
            var fade=Ensure<TrainingSceneFade>(cover);Set(fade,"blackCover",group);return fade;
        }
        private static void BuildSettings(Transform parent,NarrationController narration,Vector3 position)
        {
            var canvas=Canvas(parent,"Training_Audio_Settings",position,new Vector2(1000,95),.0007f);
            Ensure<QpcrWorldSpacePanel>(canvas);Image(canvas.transform,"Background",Vector2.zero,new Vector2(1000,95),Paper);
            Text(canvas.transform,"Volume_Label",new Vector2(-400,0),new Vector2(150,45),22,"Voice volume");
            var volume=Volume(canvas.transform,new Vector2(-200,0));var mute=Toggle(canvas.transform,"Mute",new Vector2(100,0),"Mute voice");
            var simulator=Toggle(canvas.transform,"Simulator",new Vector2(365,0),"PC simulator");
            simulator.gameObject.SetActive(false);
            var settings=Ensure<TrainingAudioSettings>(canvas);Set(settings,"narration",narration);Set(settings,"volume",volume);Set(settings,"mute",mute);Set(settings,"simulator",simulator);
            Objects(settings,"simulatorObjects",UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&t.name=="XR Interaction Simulator").Select(t=>(UnityEngine.Object)t.gameObject).ToArray());
        }
        private const string AdeolaAudioFolder = "Assets/Team5/Audio/Narration/Adeola";

        private static NarrationCue[] BuildCues()
        {
            Folder(AdeolaAudioFolder);var cues=new List<NarrationCue>();
            foreach(QpcrLessonState state in Enum.GetValues(typeof(QpcrLessonState)))
            {cues.Add(Cue("Instruction_Qpcr_"+state,QpcrLessonController.GetPresentation(state).Instruction));cues.Add(Cue("Why_Qpcr_"+state,QpcrTeachingContent.Explain(state)));}
            foreach(PPELessonStep state in Enum.GetValues(typeof(PPELessonStep)))
            {cues.Add(Cue("Instruction_Ppe_"+state,PPELessonController.InstructionFor(state)));cues.Add(Cue("Why_Ppe_"+state,PPELessonUI.Explanation(state)));}
            foreach(var state in new[]{PPELessonStep.WearLabCoat,PPELessonStep.TurnOnTap,PPELessonStep.WetHands,PPELessonStep.TurnOffTap,
                PPELessonStep.DryHands,PPELessonStep.WearLeftGlove,PPELessonStep.WearRightGlove,PPELessonStep.Complete})
            {cues.Add(Cue("Instruction_Ppe_Presentation_"+state,PPELessonController.InstructionFor(state,PPELessonMode.Presentation)));
             cues.Add(Cue("Why_Ppe_Presentation_"+state,PPELessonUI.Explanation(state,PPELessonMode.Presentation)));}
            foreach(QpcrProtocolParameter p in Enum.GetValues(typeof(QpcrProtocolParameter)))cues.Add(Cue("Parameter_"+p,QpcrProtocolValidator.LabelFor(p)+"\n\n"+QpcrTeachingContent.Explain(p)));
            cues.Add(Cue("Instruction_Lobby_Welcome","Welcome to the T5 XR Genomics Laboratory. Enter a nickname, then select Continue."));
            cues.Add(Cue("Instruction_Lobby_Ready","Your laboratory is ready. Select Enter Laboratory. First, prepare safely. Then you will load and run Q P C R."));
            return cues.ToArray();
        }
        private static NarrationCue Cue(string key,string caption)
        {
            return new NarrationCue {Key=key,Caption=caption,Clip=FindAdeolaClip(key)};
        }
        private static AudioClip FindAdeolaClip(string key)
        {
            // These two Presentation explanations have identical approved wording.
            // Prefer an exact recording; otherwise share the wet-hands explanation.
            // The original post-run recording says 4 C. Only the revised 15 C
            // recording may serve this cue; keep the original asset untouched.
            var candidates = key == "Parameter_PostRunHold"
                ? new[] { "Parameter_PostRunHold_15C" }
                : key == "Why_Ppe_Presentation_TurnOnTap"
                    ? new[] { key, "Why_Ppe_Presentation_WetHands" }
                    : new[] { key };
            foreach (var candidate in candidates)
            {
                var paths = AssetDatabase.FindAssets(candidate + " t:AudioClip", new[] { AdeolaAudioFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), candidate,
                        StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (paths.Length > 1)
                    throw new InvalidOperationException("Ambiguous Adeola recording for " + candidate + ": " + string.Join(", ", paths));
                if (paths.Length == 1) return AssetDatabase.LoadAssetAtPath<AudioClip>(paths[0]);
            }
            return null;
        }
        [MenuItem("Team 5/Training Flow/Refresh Narration Clip Assignments")]
        public static void RefreshTayo()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Refresh narration in Edit Mode after compilation finishes.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DemoPath && scene.path != LobbyPath)
                throw new InvalidOperationException("Open LobbyScene or DemoScene before refreshing narration.");
            var controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NarrationController>(true)).ToArray();
            if (controllers.Length != 1)
                throw new InvalidOperationException("Expected exactly one NarrationController in " + scene.name + ". Found " + controllers.Length + ".");

            // Resolve first, so ambiguity cannot leave a partially refreshed scene.
            var available = BuildCues();
            var narration = controllers[0];
            var serialized = new SerializedObject(narration);
            var entries = serialized.FindProperty("cues");
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < entries.arraySize; index++)
            {
                var key = entries.GetArrayElementAtIndex(index).FindPropertyRelative("Key").stringValue;
                if (string.IsNullOrWhiteSpace(key) || indices.ContainsKey(key))
                    throw new InvalidOperationException("Blank or duplicate narration cue in " + scene.name + ": " + key);
                indices.Add(key, index);
            }
            var source = serialized.FindProperty("audioSource").objectReferenceValue as AudioSource;
            if (source == null) throw new InvalidOperationException("Narration AudioSource is missing in " + scene.name + ".");
            Undo.RecordObjects(new UnityEngine.Object[] { narration, source }, "Refresh Adeola narration only");
            foreach (var cue in available)
            {
                if (!indices.TryGetValue(cue.Key, out var index))
                {
                    index = entries.arraySize++;
                    var added = entries.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("Key").stringValue = cue.Key;
                    added.FindPropertyRelative("Caption").stringValue = cue.Caption;
                }
                // Existing captions and all UI/AudioSource bindings stay intact.
                entries.GetArrayElementAtIndex(index).FindPropertyRelative("Clip").objectReferenceValue = cue.Clip;
            }
            serialized.ApplyModifiedProperties();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
            var missing = available.Where(cue => cue.Clip == null).Select(cue => cue.Key).ToArray();
            var message = scene.name + ": assigned " + (available.Length - missing.Length) + "/" + available.Length + " Adeola narration cues.";
            if (missing.Length == 0) Debug.Log(message);
            else Debug.LogWarning(message + " Missing recordings (no voice substitution): " + string.Join(", ", missing));
        }
        [MenuItem("Team 5/Training Flow/Prepare Adeola Audio For Quest")]
        public static void PrepareAdeolaAudioForQuest()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Prepare audio in Edit Mode after compilation finishes.");
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AdeolaAudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .8f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SetOverrideSampleSettings("Android", settings);
                importer.forceToMono = true;
                importer.SaveAndReimport();
                count++;
            }
            Debug.Log("Prepared " + count + " Adeola clips for Quest. Source recordings and non-narration sounds were preserved.");
        }
        [MenuItem("Team 5/Training Flow/Export Narration Recording Script")]
        public static void ExportSpeech()
        {
            var lines=BuildCues().Select(c=>new {key=c.Key,caption=c.Caption,spokenText=c.Caption.Replace("qPCR","Q P C R").Replace("A1","A one").Replace("µL","microlitres").Replace("°C","degrees Celsius")});
            System.IO.Directory.CreateDirectory("Temp/TrainingFlow");
            System.IO.File.WriteAllText("Temp/TrainingFlow/AdeolaRecordingScript.json",Newtonsoft.Json.JsonConvert.SerializeObject(lines,Newtonsoft.Json.Formatting.Indented));
        }
        private static void BuildVideo(QpcrLessonController lesson,NarrationController narration,Transform card)
        {
            Folder("Assets/Team5/Videos/Teaching");Folder("Assets/Team5/UI");
            var root=Child(Named("50_LESSON_AND_UI"),"QPCR_VIDEO_SYSTEM");var controller=Ensure<LessonVideoController>(root);
            var player=Ensure<VideoPlayer>(root);var audio=Ensure<AudioSource>(root);audio.playOnAwake=false;audio.volume=.65f;
            const string texturePath="Assets/Team5/UI/TeachingVideo.renderTexture";var texture=AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if(texture==null){texture=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32){name="TeachingVideo",antiAliasing=1,useMipMap=false,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};AssetDatabase.CreateAsset(texture,texturePath);}
            var panel=Canvas(root.transform,"Teaching_Video_Panel",new Vector3(3.75f,1.68f,2.94f),new Vector2(1100,860),.0007f);Ensure<QpcrWorldSpacePanel>(panel);
            Image(panel.transform,"Background",Vector2.zero,new Vector2(1100,860),Paper);
            var title=Text(panel.transform,"Title",new Vector2(0,362),new Vector2(970,65),44,"Teaching video");
            var raw=Ensure<RawImage>(Rect(panel.transform,"Picture",new Vector2(0,55),new Vector2(1024,576)).gameObject);raw.texture=texture;raw.raycastTarget=false;
            var caption=Text(panel.transform,"Caption",new Vector2(0,-265),new Vector2(970,60),24,"");
            var status=Text(panel.transform,"Status",new Vector2(0,-320),new Vector2(970,45),22,"");
            var play=Button(panel.transform,"Play_Pause",new Vector2(-375,-385),new Vector2(230,64),"Pause");Click(play,controller.TogglePlayback);
            Click(Button(panel.transform,"Replay",new Vector2(-120,-385),new Vector2(230,64),"Replay"),controller.Replay);
            var sound=Button(panel.transform,"Sound",new Vector2(135,-385),new Vector2(230,64),"Sound On");Click(sound,controller.ToggleSound);
            Click(Button(panel.transform,"Close",new Vector2(390,-385),new Vector2(230,64),"Close"),controller.Close);
            var watch=Button(card,"Watch_Video",new Vector2(290,310),new Vector2(280,64),"Watch Video");Click(watch,controller.Open);watch.interactable=false;
            Set(controller,"lesson",lesson);Set(controller,"narration",narration);Set(controller,"player",player);Set(controller,"videoAudio",audio);Set(controller,"videoTexture",texture);Set(controller,"panel",panel);Set(controller,"picture",raw);Set(controller,"title",title);Set(controller,"caption",caption);Set(controller,"status",status);Set(controller,"playLabel",play.GetComponentInChildren<TMP_Text>());Set(controller,"soundLabel",sound.GetComponentInChildren<TMP_Text>());Set(controller,"watchButton",watch);
            panel.SetActive(false);
        }
    }
}
#endif
