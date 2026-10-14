#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Team5.qPCR.Editor
{
    internal static class TrainingUiBuilder
    {
        public static readonly Color Paper = new Color32(244,247,243,255);
        public static readonly Color Navy = new Color32(22,43,59,255);
        public static readonly Color Teal = new Color32(19,125,128,255);
        public static readonly Color Soft = new Color32(225,241,243,255);
        private static Sprite rounded;
        public static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            // Use Unity's overloaded null check: native components can be fake-null in the Editor.
            return component != null ? component : go.AddComponent<T>();
        }
        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash+1));
        }
        public static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            Folder("Assets/Team5/UI");
            const string path = "Assets/Team5/UI/RoundedCard.asset";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path)) if (obj is Sprite sprite) return rounded = sprite;
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false) { name = "RoundedCardTexture", filterMode = FilterMode.Bilinear };
            for (var y=0;y<64;y++) for(var x=0;x<64;x++)
            {
                var dx=Mathf.Max(12-x, x-51); var dy=Mathf.Max(12-y, y-51);
                var outside=dx>0&&dy>0&&dx*dx+dy*dy>144;
                texture.SetPixel(x,y,outside?Color.clear:Color.white);
            }
            texture.Apply(); AssetDatabase.CreateAsset(texture,path);
            rounded=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));
            rounded.name="RoundedCard"; AssetDatabase.AddObjectToAsset(rounded,texture); return rounded;
        }
        public static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var child=parent.Find(name);
            var r=child==null?new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>():child.GetComponent<RectTransform>();
            if(r==null) throw new InvalidOperationException(name+" must have a RectTransform.");
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        public static Image Image(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var r=Rect(parent,name,position,size);var image=Ensure<UnityEngine.UI.Image>(r.gameObject);
            image.color=color;image.sprite=Rounded();image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=false;return image;
        }
        public static TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,float font,string value,Color? color=null)
        {
            var r=Rect(parent,name,position,size);var text=Ensure<TextMeshProUGUI>(r.gameObject);
            text.font=TMP_Settings.defaultFontAsset;text.fontSize=font;text.enableAutoSizing=false;text.color=color??Navy;
            text.alignment=TextAlignmentOptions.TopLeft;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;text.text=value;
            return text;
        }
        public static Button Button(Transform parent,string name,Vector2 position,Vector2 size,string label,Color? color=null)
        {
            var image=Image(parent,name,position,size,color??Teal);image.raycastTarget=true;
            var button=Ensure<UnityEngine.UI.Button>(image.gameObject);button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.84f,.98f,.96f);colors.pressedColor=new Color(.65f,.88f,.86f);colors.disabledColor=new Color(.6f,.66f,.67f);button.colors=colors;
            var t=Text(image.transform,"Label",Vector2.zero,size-new Vector2(16,8),24,label,Color.white);t.alignment=TextAlignmentOptions.Center;
            return button;
        }
        public static void Click(Button button,UnityEngine.Events.UnityAction action)
        { button.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddVoidPersistentListener(button.onClick,action); }
        public static GameObject Canvas(Transform parent,string name,Vector3 position,Vector2 size,float scale=.0009f)
        {
            var r=Rect(parent,name,Vector2.zero,size);r.position=position;r.localScale=Vector3.one*scale;
            var canvas=Ensure<UnityEngine.Canvas>(r.gameObject);canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go=>go.GetComponentsInChildren<Camera>(true)).FirstOrDefault(camera=>camera.CompareTag("MainCamera"));
            Ensure<CanvasScaler>(r.gameObject).dynamicPixelsPerUnit=14;
            Ensure<TrackedDeviceGraphicRaycaster>(r.gameObject);
            // XR raycasting owns pointer dispatch; avoid a second raycaster duplicating events.
            var regular=r.GetComponent<GraphicRaycaster>();if(regular!=null) regular.enabled=false;
            return r.gameObject;
        }
        public static void Set(UnityEngine.Object target,string name,UnityEngine.Object value)
        { var so=new SerializedObject(target);var p=so.FindProperty(name);if(p==null) throw new InvalidOperationException(target.name+" missing "+name);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
        public static void Set(UnityEngine.Object target,string name,bool value)
        {var so=new SerializedObject(target);so.FindProperty(name).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        public static void Set(UnityEngine.Object target,string name,float value)
        {var so=new SerializedObject(target);so.FindProperty(name).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        public static void Set(UnityEngine.Object target,string name,Vector3 value)
        {var so=new SerializedObject(target);so.FindProperty(name).vector3Value=value;so.ApplyModifiedPropertiesWithoutUndo();}
        public static void Objects(UnityEngine.Object target,string name,UnityEngine.Object[] values)
        {var so=new SerializedObject(target);var p=so.FindProperty(name);p.arraySize=values.Length;for(var i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
        public static Slider Volume(Transform parent,Vector2 position)
        {
            var track=Image(parent,"Voice_Volume",position,new Vector2(210,24),Soft);track.raycastTarget=true;
            var slider=Ensure<Slider>(track.gameObject);slider.minValue=0;slider.maxValue=1;slider.value=.8f;
            var handle=Image(track.transform,"Handle",Vector2.zero,new Vector2(24,38),Teal);handle.raycastTarget=true;
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;return slider;
        }
        public static Toggle Toggle(Transform parent,string name,Vector2 position,string label)
        {
            var root=Rect(parent,name,position,new Vector2(240,48));var toggle=Ensure<UnityEngine.UI.Toggle>(root.gameObject);
            var box=Image(root,"Box",new Vector2(-95,0),new Vector2(38,38),Soft);box.raycastTarget=true;
            var tick=Image(box.transform,"Tick",Vector2.zero,new Vector2(24,24),Teal);
            toggle.targetGraphic=box;toggle.graphic=tick;Text(root,"Label",new Vector2(30,0),new Vector2(180,40),22,label);return toggle;
        }
    }
}
#endif
