#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        private static void EnumValue(UnityEngine.Object target,string field,int value)
        {var so=new SerializedObject(target);so.FindProperty(field).intValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static Material TrainingMaterial(string name,Color color,float metallic=0,float smoothness=.3f)
        {
            Folder("Assets/Team5/Materials/Training");var path="Assets/Team5/Materials/Training/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);return material;
        }
        private static AudioSource PpeAudio(Transform parent,string name,PpeSoundKind kind,bool loop=false)
        {
            var root=Child(parent,name);var source=Ensure<AudioSource>(root);source.playOnAwake=false;source.loop=loop;source.volume=loop?.25f:.45f;source.spatialBlend=1;source.minDistance=.4f;source.maxDistance=3;
            EnumValue(Ensure<PpeSoundEffect>(root),"kind",(int)kind);return source;
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool collider=false)
        {
            var existing=parent.Find(name);var go=existing==null?GameObject.CreatePrimitive(type):existing.gameObject;
            go.name=name;go.transform.SetParent(parent,true);go.transform.position=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider&&go.GetComponent<Collider>()!=null)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static GameObject BuildPpe(PPELessonController ppe,NarrationController narration)
        {
            var left=Named("Left Controller");var right=Named("Right Controller");
            if(left==null||right==null)throw new InvalidOperationException("Both controller roots are required.");
            foreach(var controller in new[]{left,right})
            {
                var isLeft=controller==left;var probe=Child(controller,isLeft?"LeftWashProbe":"RightWashProbe");
                probe.transform.localPosition=new Vector3(0,-.02f,-.06f);probe.transform.localScale=Vector3.one;
                var sphere=Ensure<SphereCollider>(probe);sphere.radius=.055f;sphere.isTrigger=true;
                var body=Ensure<Rigidbody>(probe);body.useGravity=false;body.isKinematic=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var hand=Ensure<PPEHandProbe>(probe);Set(hand,"lesson",ppe);EnumValue(hand,"side",isLeft?0:1);
            }
            var coat=Named("LabCoat_Grab");var grab=coat.GetComponent<XRGrabInteractable>();var coatBox=coat.GetComponent<BoxCollider>();
            var bounds=coat.GetComponentInChildren<Renderer>().bounds;
            coatBox.center=coat.InverseTransformPoint(bounds.center);
            coatBox.size=new Vector3(bounds.size.x/coat.lossyScale.x,bounds.size.y/coat.lossyScale.y,bounds.size.z/coat.lossyScale.z)*.94f;
            coatBox.isTrigger=false;grab.movementType=XRBaseInteractable.MovementType.Kinematic;grab.trackScale=false;grab.throwOnDetach=false;
            grab.useDynamicAttach=true;grab.retainTransformParent=false;grab.colliders.Clear();grab.colliders.Add(coatBox);
            coat.GetComponent<Rigidbody>().isKinematic=true;coat.GetComponent<Rigidbody>().useGravity=false;
            var zone=Named("CoatWearZone");zone.SetParent(Named("Main Camera"),false);zone.localPosition=new Vector3(0,-.45f,.35f);zone.localScale=Vector3.one;
            var wear=Ensure<CoatWearZone>(zone.gameObject);Set(wear,"lesson",ppe);Set(wear,"coatRoot",coat);Set(wear,"coatGrab",grab);
            Set(wear,"wearAudio",PpeAudio(zone,"Coat_Wear_Sound",PpeSoundKind.Coat));
            var chest=Ensure<BoxCollider>(zone.gameObject);chest.isTrigger=true;chest.center=Vector3.zero;chest.size=new Vector3(.6f,.65f,.35f);

            var handwash=Named("HANDWASH_STATION");var tap=Named("Tap_Handle");tap.position=new Vector3(2.56f,1.35f,-2.01f);
            var steel=TrainingMaterial("M_PpeSteel",new Color(.66f,.7f,.73f),.85f,.55f);
            Primitive(tap,"Tap_Handle_Visible",PrimitiveType.Cylinder,tap.position,new Vector3(.06f,.012f,.06f),steel);
            var tapBox=tap.GetComponent<BoxCollider>();tapBox.center=Vector3.zero;tapBox.size=new Vector3(.09f,.06f,.09f);tapBox.isTrigger=false;
            var water=Child(handwash,"Running_Water");water.transform.position=new Vector3(2.28f,1.26f,-2.01f);water.transform.rotation=Quaternion.Euler(90,0,0);
            var particles=Ensure<ParticleSystem>(water);var main=particles.main;main.playOnAwake=false;main.loop=true;main.startLifetime=.18f;main.startSpeed=1.15f;main.startSize=.012f;main.maxParticles=120;main.gravityModifier=.2f;main.startColor=new Color(.68f,.9f,1f,.7f);
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=4;shape.radius=.01f;
            var emission=particles.emission;emission.rateOverTime=100;
            var particleMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Team5/Materials/Training/M_WaterParticles.mat");
            if(particleMaterial==null){particleMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(particleMaterial,"Assets/Team5/Materials/Training/M_WaterParticles.mat");}
            water.GetComponent<ParticleSystemRenderer>().sharedMaterial=particleMaterial;
            var waterAudio=Ensure<AudioSource>(water);waterAudio.playOnAwake=false;waterAudio.loop=true;waterAudio.volume=.22f;waterAudio.spatialBlend=1;waterAudio.minDistance=.5f;waterAudio.maxDistance=3;
            waterAudio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Team5/Scripts/Audio/NewScene/water-running-faucet-by-alex-jauk-royalty-free-music-940002.wav");
            var tapController=Ensure<TapController>(tap.gameObject);Set(tapController,"lesson",ppe);Set(tapController,"interactable",tap.GetComponent<XRSimpleInteractable>());Set(tapController,"waterParticles",particles);Set(tapController,"waterAudio",waterAudio);Set(tapController,"movingHandle",tap.Find("Tap_Handle_Visible"));
            Set(tapController,"clickAudio",PpeAudio(tap,"Tap_Click",PpeSoundKind.Tap));
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var wash=Named("HandWashVolume");wash.position=new Vector3(2.28f,1.06f,-2.01f);wash.GetComponent<BoxCollider>().size=new Vector3(.32f,.24f,.25f);wash.GetComponent<BoxCollider>().isTrigger=true;
            Set(Ensure<HandWashZone>(wash.gameObject),"lesson",ppe);
            var soap=Named("Soap_Dispenser");soap.position=new Vector3(2.83f,1.18f,-2.01f);
            var white=TrainingMaterial("M_Soap",Paper);
            Primitive(soap,"Soap_Bottle",PrimitiveType.Cube,soap.position,new Vector3(.10f,.23f,.10f),white);
            Primitive(soap,"Soap_Pump",PrimitiveType.Cube,soap.position+Vector3.up*.14f,new Vector3(.12f,.035f,.06f),steel);
            var soapBox=Ensure<BoxCollider>(soap.gameObject);soapBox.center=new Vector3(0,.13f,0);soapBox.size=new Vector3(.14f,.08f,.10f);
            var soapController=Ensure<SoapDispenserController>(soap.gameObject);Set(soapController,"lesson",ppe);Set(soapController,"interactable",Ensure<XRSimpleInteractable>(soap.gameObject));Set(soapController,"movingButton",soap.Find("Soap_Pump"));
            Set(soapController,"soapAudio",PpeAudio(soap,"Soap_Sound",PpeSoundKind.Soap));
            var dryerZone=Child(handwash,"Dryer_Hand_Zone");dryerZone.transform.position=new Vector3(3.47f,1.32f,-1.74f);
            var dryerBox=Ensure<BoxCollider>(dryerZone);dryerBox.isTrigger=true;dryerBox.size=new Vector3(.34f,.23f,.3f);
            Set(Ensure<HandDryerZone>(dryerZone),"lesson",ppe);
            Set(dryerZone.GetComponent<HandDryerZone>(),"dryerAudio",PpeAudio(dryerZone.transform,"Dryer_Sound",PpeSoundKind.Dryer,true));
            var appearance=Ensure<HandAppearanceController>(ppe.gameObject);
            Objects(appearance,"leftHandRenderers",Named("Left Hand Model").GetComponentsInChildren<Renderer>(true).Cast<UnityEngine.Object>().ToArray());
            Objects(appearance,"rightHandRenderers",Named("Right Hand Model").GetComponentsInChildren<Renderer>(true).Cast<UnityEngine.Object>().ToArray());
            var gloveMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Team5/Materials/RealisticLab/Redesign_Nitrile_Blue.mat")??TrainingMaterial("M_Glove",new Color(.08f,.58f,.76f));
            Set(appearance,"blueGloveMaterial",gloveMaterial);
            var pickups=new Transform[2];
            for(var i=0;i<2;i++)
            {
                var root=Named(i==0?"Left_Glove_Pickup":"Right_Glove_Pickup");pickups[i]=root;
                var mesh=Named(i==0?"Glove_rubber_low":"Glove_rubber_low.001").GetComponent<Renderer>();
                root.position=mesh.bounds.center;root.localScale=Vector3.one;
                var box=Ensure<BoxCollider>(root.gameObject);box.center=Vector3.zero;box.size=mesh.bounds.size+Vector3.one*.025f;
                var interactable=Ensure<XRSimpleInteractable>(root.gameObject);interactable.colliders.Clear();interactable.colliders.Add(box);
                var pickup=Ensure<GlovePickupController>(root.gameObject);Set(pickup,"lesson",ppe);Set(pickup,"handAppearance",appearance);EnumValue(pickup,"gloveSide",i);Set(pickup,"interactable",interactable);Set(pickup,"expectedControllerRoot",i==0?left:right);
                Objects(pickup,"pickupRenderers",new UnityEngine.Object[]{mesh});Objects(pickup,"pickupColliders",new UnityEngine.Object[]{box});
                Set(pickup,"gloveAudio",PpeAudio(root,"Glove_Sound",PpeSoundKind.Glove));
            }
            var canvas=Canvas(Named("50_LESSON_AND_UI"),"PPE_Instruction_Canvas",new Vector3(5.15f,1.6f,-2.72f),new Vector2(1000,740),.00075f);Ensure<QpcrWorldSpacePanel>(canvas);
            Image(canvas.transform,"Background",Vector2.zero,new Vector2(1000,740),Paper);
            var title=Text(canvas.transform,"Title",new Vector2(0,270),new Vector2(900,60),44,"Prepare for lab work");
            var step=Text(canvas.transform,"Step",new Vector2(0,330),new Vector2(900,35),24,"PPE preparation");
            var target=Text(canvas.transform,"Target",new Vector2(0,213),new Vector2(900,40),28,"",Teal);
            var instruction=Text(canvas.transform,"Instruction",new Vector2(0,100),new Vector2(900,155),28,"");
            var purpose=Text(canvas.transform,"Purpose",new Vector2(0,-33),new Vector2(900,90),28,"");
            var progress=Text(canvas.transform,"Progress",new Vector2(0,-132),new Vector2(900,50),26,"");
            var feedback=Text(canvas.transform,"Feedback",new Vector2(0,-191),new Vector2(900,70),24,"");
            Set(ppe,"instructionText",instruction);Set(ppe,"targetText",target);Set(ppe,"progressText",progress);Set(ppe,"feedbackText",feedback);Set(ppe,"narrationSource",(UnityEngine.Object)null);
            var ui=Ensure<PPELessonUI>(canvas);Set(ui,"lesson",ppe);Set(ui,"narration",narration);Set(ui,"title",title);Set(ui,"purpose",purpose);Set(ui,"step",step);
            var diagram=Rect(canvas.transform,"Hand_Surface_Reminders",new Vector2(0,-35),new Vector2(880,120));
            var labels=new[]{"Palms","Backs","Between fingers","Under nails"};
            for(var index=0;index<4;index++)
            {
                var icon=Rect(diagram,"Hand_"+index,new Vector2(-330+index*220,10),new Vector2(200,115));
                Image(icon,"Palm",new Vector2(0,-4),new Vector2(54,43),index<2?Teal:Soft);
                for(var finger=0;finger<5;finger++)
                {
                    Image(icon,"Finger_"+finger,new Vector2(-24+finger*12,27),new Vector2(9,33),Soft);
                    if(index==3)Image(icon,"Nail_"+finger,new Vector2(-24+finger*12,41),new Vector2(7,6),Teal);
                    if(index==2&&finger<4)Image(icon,"Gap_"+finger,new Vector2(-18+finger*12,20),new Vector2(3,26),Teal);
                }
                Text(icon,"Label",new Vector2(0,-46),new Vector2(200,32),20,labels[index]).alignment=TextAlignmentOptions.Center;
            }
            Set(ui,"handSurfaceDiagram",diagram.gameObject);diagram.gameObject.SetActive(false);
            var why=Canvas(canvas.transform,"PPE_Why",new Vector3(4.43f,1.6f,-2.72f),new Vector2(900,700),1f);why.transform.localScale=Vector3.one;
            // Nested canvas inherits the parent's metre-per-UI-unit scale.
            Image(why.transform,"Background",Vector2.zero,new Vector2(900,700),Paper);
            Text(why.transform,"Title",new Vector2(0,270),new Vector2(800,60),44,"Why this matters");
            var whyText=Text(why.transform,"Explanation",new Vector2(0,40),new Vector2(800,370),28,"");Set(ui,"whyPanel",why);Set(ui,"whyText",whyText);
            Click(Button(why.transform,"Read",new Vector2(-200,-260),new Vector2(290,64),"Read Aloud"),ui.ReadAloud);
            Click(Button(why.transform,"Stop",new Vector2(150,-260),new Vector2(290,64),"Stop Reading"),narration.StopReading);why.SetActive(false);
            Click(Button(canvas.transform,"Why",new Vector2(-290,-290),new Vector2(310,64),"Why this step?"),ui.ToggleWhy);
            Click(Button(canvas.transform,"Replay",new Vector2(80,-290),new Vector2(330,64),"Replay Instruction"),narration.ReplayInstruction);
            Click(Button(canvas.transform,"Reset",new Vector2(355,-290),new Vector2(190,64),"Reset PPE"),ppe.BeginLesson);
            Button(canvas.transform,"Continue_qPCR",new Vector2(0,-132),new Vector2(650,64),"PPE complete - Continue to qPCR");
            var pointer=Primitive(Named("30_PPE_AND_SAFETY"),"PPE_Target_Pointer",PrimitiveType.Sphere,coat.position+Vector3.up*.2f,Vector3.one*.035f,TrainingMaterial("M_PpeCue",Teal));Set(ui,"pointer",pointer.transform);
            Objects(ui,"targets",new UnityEngine.Object[]{coat,tap,wash,soap,Named("LeftWashProbe"),wash,tap,dryerZone.transform,pickups[0],pickups[1],null});
            return canvas;
        }
        private static void AddReachFilters(PPELessonController ppe,QpcrLessonController lesson)
        {
            var left=Named("Left Controller");var right=Named("Right Controller");var ppeRoot=Named("30_PPE_AND_SAFETY");var qpcrRoot=Named("40_QPCR_STATION");
            foreach(var interactable in UnityEngine.Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var isQpcr=interactable.transform.IsChildOf(qpcrRoot);if(!isQpcr&&!interactable.transform.IsChildOf(ppeRoot))continue;
                if(interactable.GetComponent<PlateInspectionHotspot>()!=null)continue;
                var filter=Ensure<NearbySelectionFilter>(interactable.gameObject);Set(filter,"leftController",left);Set(filter,"rightController",right);Set(filter,"ppe",ppe);Set(filter,"lesson",lesson);Set(filter,"requiresPpe",isQpcr);Set(filter,"maximumReach",.22f);
            }
        }
    }
}
#endif
