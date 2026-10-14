#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Team5.qPCR.Editor.TrainingUiBuilder;

namespace Team5.qPCR.Editor
{
    public static partial class TrainingFlowSceneInstaller
    {
        [MenuItem("Team 5/Training Flow/3 Polish Laboratory Materials And Lighting")]
        public static void PolishLab()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=DemoPath)throw new System.InvalidOperationException("Polish requires DemoScene in Edit Mode.");
            var cache=new Dictionary<string,Material>();
            var structuralRoots=new[]{Named("10_LAB_STRUCTURE"),Named("20_LAB_FURNITURE")};
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(renderer.gameObject.scene!=SceneManager.GetActiveScene()||!renderer.gameObject.activeInHierarchy)continue;
                var name=renderer.name.ToLowerInvariant();var materials=renderer.sharedMaterials;
                for(var i=0;i<materials.Length;i++)
                {
                    var original=materials[i];if(original==null||original.shader==null||!original.shader.name.Contains("Universal Render Pipeline"))continue;
                    var role=name.Contains("glass")&&!name.Contains("touchscreen")?"Glass":name.Contains("wall")?"Wall":name=="epoxy_floor"?"Floor":name.Contains("worktop")?(original.name.ToLowerInvariant().Contains("titanuim")?"Steel":"Worktop"):name.Contains("handle")?"Steel":name.Contains("cabinet")?"Cabinet":null;
                    if(role==null)continue;
                    // Scope every finish to T5, including materials from an earlier polish pass.
                    // Existing model and shared training materials are never edited in place.
                    var key=original.name.StartsWith("T5_")&&original.name.EndsWith("_"+role)
                        ?original.name:"T5_"+original.name+"_"+role;
                    if(!cache.TryGetValue(key,out var material))
                    {
                        Folder("Assets/Team5/Materials/Training");var path="Assets/Team5/Materials/Training/"+key+".mat";
                        material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(material==null){material=new Material(original){name=key};AssetDatabase.CreateAsset(material,path);}
                        if(role=="Wall"){material.color=Paper;material.SetFloat("_Smoothness",.16f);material.SetFloat("_BumpScale",.12f);}
                        if(role=="Floor"){material.SetFloat("_Smoothness",.28f);material.SetFloat("_Metallic",0);material.SetFloat("_BumpScale",.18f);if(material.mainTextureScale==Vector2.one)material.mainTextureScale=new Vector2(6,4);}
                        if(role=="Worktop"){material.color=new Color(.10f,.14f,.17f);material.SetTexture("_BaseMap",null);material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.3f);material.SetFloat("_Metallic",.05f);}
                        if(role=="Steel"){material.color=new Color(.67f,.7f,.73f);material.SetFloat("_Metallic",.85f);material.SetFloat("_Smoothness",.38f);}
                        if(role=="Cabinet"){material.color=Paper;material.SetFloat("_Smoothness",.25f);}
                        if(role=="Glass")material.SetFloat("_Smoothness",.75f);
                        cache[key]=material;
                    }
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
                if(structuralRoots.Any(r=>r!=null&&renderer.transform.IsChildOf(r)))
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)|StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.type==LightType.Point){light.lightmapBakeType=LightmapBakeType.Baked;light.intensity=1.6f;light.bounceIntensity=1;light.shadows=LightShadows.Soft;light.color=Color.white;light.useColorTemperature=true;light.colorTemperature=5000;}
                else if(light.type==LightType.Directional){light.lightmapBakeType=LightmapBakeType.Mixed;light.intensity=.45f;light.shadows=LightShadows.Soft;}
            }
            var lighting=Named("60_LIGHTING");var probes=Ensure<LightProbeGroup>(Child(lighting,"Training_Light_Probes"));var positions=new List<Vector3>();
            for(var x=-5f;x<=5;x+=2.5f)for(var z=-3f;z<=3;z+=2f)foreach(var y in new[]{.6f,1.5f,2.3f})positions.Add(new Vector3(x,y,z));
            probes.probePositions=positions.ToArray();
            var reflection=Named("Laboratory_Reflection_Probe")?.GetComponent<ReflectionProbe>();
            if(reflection==null)reflection=Ensure<ReflectionProbe>(Child(lighting,"Training_Reflection_Probe"));
            reflection.transform.position=new Vector3(0,1.5f,0);reflection.size=new Vector3(12,3,8);reflection.mode=ReflectionProbeMode.Baked;reflection.resolution=256;reflection.boxProjection=true;reflection.intensity=.85f;
            const string lightingPath="Assets/Team5/UI/TrainingLighting.lighting";
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if(settings==null){settings=new LightingSettings(){name="TrainingLighting"};AssetDatabase.CreateAsset(settings,lightingPath);}
            Lightmapping.lightingSettings=settings;
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapResolution=20;settings.lightmapMaxSize=2048;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        [MenuItem("Team 5/Training Flow/Bake Laboratory Lighting")]
        public static void BakeLab()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=DemoPath)throw new System.InvalidOperationException("Bake requires DemoScene in Edit Mode.");
            if(!Lightmapping.isRunning)Lightmapping.BakeAsync();
        }
        [MenuItem("Team 5/Training Flow/Refine Laboratory Lighting Only")]
        public static void RefineTrainingLighting()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=DemoPath)throw new System.InvalidOperationException("Open DemoScene in Edit Mode.");
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.gameObject.scene!=SceneManager.GetActiveScene())continue;
                if(light.type==LightType.Point)
                {light.lightmapBakeType=LightmapBakeType.Baked;light.intensity=1.6f;light.color=Color.white;light.useColorTemperature=true;light.colorTemperature=5000;light.shadows=LightShadows.Soft;}
                if(light.type==LightType.Directional){light.lightmapBakeType=LightmapBakeType.Mixed;light.intensity=.45f;light.shadows=LightShadows.Soft;}
            }
            foreach(var root in new[]{Named("10_LAB_STRUCTURE"),Named("20_LAB_FURNITURE")})
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                    if(!(renderer is LineRenderer)&&!(renderer is ParticleSystemRenderer))GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)|StaticEditorFlags.ContributeGI);
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Team5/UI/TrainingLighting.lighting");
            if(settings==null)throw new System.InvalidOperationException("The training lighting settings are missing.");
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapResolution=20;settings.lightmapMaxSize=2048;Lightmapping.lightingSettings=settings;
            foreach(var reflection in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {reflection.mode=ReflectionProbeMode.Baked;reflection.resolution=256;reflection.boxProjection=true;}
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
    }
}
#endif
