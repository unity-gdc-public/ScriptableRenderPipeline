using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace UnityEditor.Rendering.LookDev
{
    [CreateAssetMenu(fileName = "Environment", menuName = "LookDev/Environment", order = 1)]
    public class Environment : ScriptableObject
    {
        //[TODO: check if the shadow/sky split worth the indirection]
        //Note: multi-edition is not supported as we cannot draw multiple HDRI
        [Serializable]
        public class Shadow
        {
            public Cubemap cubemap;
            // Setup default position to be on the sun in the default HDRI.
            // This is important as the defaultHDRI don't call the set brightest spot function on first call.
            [SerializeField]
            private float m_Latitude = 60.0f; // [-90..90]
            [SerializeField]
            private float m_Longitude = 299.0f; // [0..360]
            [field: SerializeField]
            public float intensity { get; set; } = 1.0f;
            [field: SerializeField]
            public Color color { get; set; } = Color.white;

            public float latitude
            {
                get => m_Latitude;
                //[TODO: check why it was originally clamped to 89 instead of 90]
                set => m_Latitude = Mathf.Clamp(value, -90, 90);
            }

            public float longitude
            {
                get => m_Longitude; 
                set
                {
                    // Clamp longitude to [0..360]
                    m_Longitude = value % 360f;
                    if (m_Longitude < 0.0)
                        m_Longitude = 360f + m_Longitude;
                }
            }
        }

        [Serializable]
        public class Sky
        {
            public Cubemap cubemap;
            public float angleOffset = 0.0f;
        }

        public Sky sky = new Sky();
        public Shadow shadow = new Shadow();
    }

    [CustomEditor(typeof(Environment))]
    class EnvironmentEditor : Editor
    {
        EnvironmentElement m_EnvironmentElement;

        public sealed override VisualElement CreateInspectorGUI()
            => m_EnvironmentElement = new EnvironmentElement(target as Environment);

        // Don't use ImGUI
        public sealed override void OnInspectorGUI() { }

        override public Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
            => EnvironmentElement.GetLatLongThumbnailTexture(target as Environment, width);
    }

    interface IBendable<T>
    {
        void Bind(T data);
    }
    public class EnvironmentElement : VisualElement, IBendable<Environment>
    {
        internal const int k_SkyThumbnailWidth = 200;
        internal const int k_SkyThumbnailHeight = 100;
        const int k_SkadowThumbnailWidth = 60;
        const int k_SkadowThumbnailHeight = 30;
        const int k_SkadowThumbnailXPosition = 130;
        const int k_SkadowThumbnailYPosition = 10;
        static Material s_cubeToLatlongMaterial;
        static Material cubeToLatlongMaterial
            => s_cubeToLatlongMaterial ?? (s_cubeToLatlongMaterial = new Material(Shader.Find("Hidden/LookDev/CubeToLatlong")));
        
        VisualElement environmentParams;
        Environment environment;

        Image latlong;
        ObjectField skyCubemapField;
        Slider skyRotationOffset;
        ObjectField shadowCubemapField;
        FloatField shadowLatitude;
        FloatField shadowLongitude;
        FloatField shadowIntensity;
        ColorField shadowColor;

        public Environment target => environment;

        public EnvironmentElement() => Create(withPreview: true);
        public EnvironmentElement(bool withPreview) => Create(withPreview);

        public EnvironmentElement(Environment environment)
        {
            Create(withPreview: true);
            Bind(environment);
        }

        void Create(bool withPreview)
        {
            if (withPreview)
            {
                latlong = new Image();
                latlong.style.width = k_SkyThumbnailWidth;
                latlong.style.height = k_SkyThumbnailHeight;
                Add(latlong);
            }

            environmentParams = GetDefaultInspector();
            Add(environmentParams);
        }

        public void Bind(Environment environment)
        {
            this.environment = environment;
            if (environment == null || environment.Equals(null))
                return;

            if (latlong != null && !latlong.Equals(null))
                latlong.image = GetLatLongThumbnailTexture();
            skyCubemapField.SetValueWithoutNotify(environment.sky.cubemap);
            skyRotationOffset.SetValueWithoutNotify(environment.sky.angleOffset);
            shadowCubemapField.SetValueWithoutNotify(environment.shadow.cubemap);
            shadowLatitude.SetValueWithoutNotify(environment.shadow.latitude);
            shadowLongitude.SetValueWithoutNotify(environment.shadow.longitude);
            shadowIntensity.SetValueWithoutNotify(environment.shadow.intensity);
            shadowColor.SetValueWithoutNotify(environment.shadow.color);
        }

        public void Bind(Environment environment, Image deportedLatlong)
        {
            latlong = deportedLatlong;
            Bind(environment);
        }

        public Texture2D GetLatLongThumbnailTexture()
            => GetLatLongThumbnailTexture(environment, k_SkyThumbnailWidth);

        public static Texture2D GetLatLongThumbnailTexture(Environment environment, int width)
        {
            int height = width >> 1;
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture temporaryRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture.active = temporaryRT;
            cubeToLatlongMaterial.SetTexture("_MainTex", environment.sky.cubemap);
            cubeToLatlongMaterial.SetVector("_WindowParams",
                new Vector4(
                    height, //height
                    -1000f, //y position, -1000f to be sure to not have clipping issue (we should not clip normally but don't want to create a new shader)
                    2f,      //margin value
                    1f));   //Pixel per Point
            cubeToLatlongMaterial.SetVector("_CubeToLatLongParams",
                new Vector4(
                    Mathf.Deg2Rad * environment.sky.angleOffset,    //rotation of the environment in radian
                    1f,     //alpha
                    1f,     //intensity
                    0f));   //LOD
            cubeToLatlongMaterial.SetPass(0);
            GL.LoadPixelMatrix(0, width, height, 0);
            GL.Clear(true, true, Color.black);
            Rect skyRect = new Rect(0, 0, width, height);
            Renderer.DrawFullScreenQuad(skyRect);

            if (environment.shadow.cubemap != null)
            {
                cubeToLatlongMaterial.SetTexture("_MainTex", environment.shadow.cubemap);
                cubeToLatlongMaterial.SetVector("_WindowParams",
                    new Vector4(
                        height, //height
                        -1000f, //y position, -1000f to be sure to not have clipping issue (we should not clip normally but don't want to create a new shader)
                        2f,      //margin value
                        1f));   //Pixel per Point
                cubeToLatlongMaterial.SetVector("_CubeToLatLongParams",
                    new Vector4(
                        Mathf.Deg2Rad * environment.sky.angleOffset,    //rotation of the environment in radian
                        1f,   //alpha
                        0.3f,   //intensity
                        0f));   //LOD
                cubeToLatlongMaterial.SetPass(0);
                int shadowWidth = (int)(width * (k_SkadowThumbnailWidth / (float)k_SkyThumbnailWidth));
                int shadowXPosition = (int)(width * (k_SkadowThumbnailXPosition / (float)k_SkyThumbnailWidth));
                int shadowYPosition = (int)(width * (k_SkadowThumbnailYPosition / (float)k_SkyThumbnailWidth));
                Rect shadowRect = new Rect(
                    shadowXPosition,
                    shadowYPosition,
                    shadowWidth,
                    shadowWidth >> 1);
                Renderer.DrawFullScreenQuad(shadowRect);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            result.Apply(false);
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(temporaryRT);
            return result;
        }
        
        public VisualElement GetDefaultInspector()
        {
            VisualElement inspector = new VisualElement() { name = "Inspector" };
            Foldout skyFoldout = new Foldout()
            {
                text = "Sky"
            };
            skyCubemapField = new ObjectField("Cubemap");
            skyCubemapField.allowSceneObjects = false;
            skyCubemapField.objectType = typeof(Cubemap);
            skyCubemapField.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.sky.cubemap = evt.newValue as Cubemap;
                if (latlong != null && !latlong.Equals(null))
                    latlong.image = GetLatLongThumbnailTexture(environment, k_SkyThumbnailWidth);
                EditorUtility.SetDirty(environment);
            });
            skyFoldout.Add(skyCubemapField);

            skyRotationOffset = new Slider("Angle Offset", 0f, 360f);
            skyRotationOffset.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.sky.angleOffset = evt.newValue;
                if (latlong != null && !latlong.Equals(null))
                    latlong.image = GetLatLongThumbnailTexture(environment, k_SkyThumbnailWidth);
                EditorUtility.SetDirty(environment);
            });
            skyFoldout.Add(skyRotationOffset);
            var style = skyFoldout.Q<Toggle>().style;
            style.marginLeft = 3;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            inspector.Add(skyFoldout);

            Foldout shadowFoldout = new Foldout()
            {
                text = "Shadow"
            };
            shadowCubemapField = new ObjectField("Cubemap");
            shadowCubemapField.allowSceneObjects = false;
            shadowCubemapField.objectType = typeof(Cubemap);
            shadowCubemapField.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.shadow.cubemap = evt.newValue as Cubemap;
                if (latlong != null && !latlong.Equals(null))
                    latlong.image = GetLatLongThumbnailTexture(environment, k_SkyThumbnailWidth);
                EditorUtility.SetDirty(environment);
            });
            shadowFoldout.Add(shadowCubemapField);

            shadowLatitude = new FloatField("Latitude");
            shadowLatitude.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.shadow.latitude = evt.newValue;
                //clamping code occurred. Reassign clamped value
                shadowLatitude.SetValueWithoutNotify(environment.shadow.latitude);
                EditorUtility.SetDirty(environment); //
            });
            shadowFoldout.Add(shadowLatitude);

            shadowLongitude = new FloatField("Longitude");
            shadowLongitude.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.shadow.longitude = evt.newValue;
                //clamping code occurred. Reassign clamped value
                shadowLongitude.SetValueWithoutNotify(environment.shadow.longitude);
                EditorUtility.SetDirty(environment);
            });
            shadowFoldout.Add(shadowLongitude);

            shadowIntensity = new FloatField("Intensity");
            shadowIntensity.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.shadow.intensity = evt.newValue;
                EditorUtility.SetDirty(environment);
            });
            shadowFoldout.Add(shadowIntensity);

            shadowColor = new ColorField("Color");
            shadowColor.RegisterValueChangedCallback(evt =>
            {
                if (environment == null || environment.Equals(null))
                    return;
                environment.shadow.color = evt.newValue;
                EditorUtility.SetDirty(environment);
            });
            shadowFoldout.Add(shadowColor);
            style = shadowFoldout.Q<Toggle>().style;
            style.marginLeft = 3;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            inspector.Add(shadowFoldout);

            return inspector;
        }
    }
}
