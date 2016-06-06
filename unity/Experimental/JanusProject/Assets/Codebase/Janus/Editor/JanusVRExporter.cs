﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.FBX;
using UnityEngine.SceneManagement;

namespace JanusVR
{
    public class JanusVRExporter : EditorWindow
    {
        internal class ExportedData
        {
            internal List<ExportedObject> exportedObjs;
            internal List<ExportedMesh> lightMapMeshes;

            internal ExportedData()
            {
                exportedObjs = new List<ExportedObject>();
                lightMapMeshes = new List<ExportedMesh>();
            }
        }

        public JanusVRExporter()
        {
            this.titleContent = new GUIContent("Janus VR Exporter");
        }

        [MenuItem("Edit/JanusVR Exporter 2.0")]
        private static void Init()
        {
            // Get existing open window or if none, make a new one:
            JanusVRExporter window = EditorWindow.GetWindow<JanusVRExporter>();
            window.Show();
        }

        [SerializeField]
        private string exportPath = @"C:\janus";
        [SerializeField]
        private int maxLightMapResolution = 1024;
        [SerializeField]
        private float uniformScale = 1;
        [SerializeField]
        private ExportMeshFormat meshFormat;


        private Dictionary<int, List<GameObject>> lightmapped;

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Export Path");
            exportPath = EditorGUILayout.TextField(exportPath);
            if (GUILayout.Button("..."))
            {
                // search for a folder
                exportPath = EditorUtility.OpenFolderPanel("JanusVR Export Folder", exportPath, @"C:\");
            }
            EditorGUILayout.EndHorizontal();

            meshFormat = (ExportMeshFormat)EditorGUILayout.EnumPopup("Mesh Format", meshFormat);
            maxLightMapResolution = Math.Max(32, EditorGUILayout.IntField("Max Lightmap Resolution", maxLightMapResolution));
            uniformScale = EditorGUILayout.FloatField("Uniform Scale", uniformScale);

            if (GUILayout.Button("Export"))
            {
                DoExport();
            }
        }

        private void DoExport()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            lightmapped = new Dictionary<int, List<GameObject>>();
            ExportedData exported = new ExportedData();

            for (int i = 0; i < roots.Length; i++)
            {
                RecursiveSearch(roots[i], exported);
            }

            if (lightmapped.Count == 0)
            {
                return;
            }

            // only load shader now, so if the user is not exporting lightmaps
            // he doesn't need to have it on his project folder
            Shader lightMapShader = Shader.Find("Hidden/LightMapExtracter");
            Material lightMap = new Material(lightMapShader);
            lightMap.SetPass(0);

            string scenePath = scene.path;
            scenePath = Path.GetDirectoryName(scenePath);
            string lightMapsFolder = Path.Combine(scenePath, scene.name);

            // export lightmaps
            int exp = 0;
            foreach (var lightPair in lightmapped)
            {
                int id = lightPair.Key;
                List<GameObject> toRender = lightPair.Value;

                // get the path to the lightmap file
                string lightMapFile = Path.Combine(lightMapsFolder, "Lightmap-" + id + "_comp_light.exr");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(lightMapFile);
                lightMap.SetTexture("_LightMapTex", texture);

                for (int i = 0; i < toRender.Count; i++)
                {
                    GameObject obj = toRender[i];
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                    MeshFilter filter = obj.GetComponent<MeshFilter>();

                    Mesh mesh = filter.sharedMesh;

                    Transform trans = obj.transform;

                    Matrix4x4 world = Matrix4x4.TRS(trans.position, trans.rotation, trans.lossyScale);

                    Vector4 scaleOffset = renderer.lightmapScaleOffset;
                    float width = (1 - scaleOffset.z) * scaleOffset.x;
                    float height = (1 - scaleOffset.w) * scaleOffset.y;
                    float size = Math.Max(width, height);

                    int lightMapSize = (int)(maxLightMapResolution * size);
                    lightMapSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(lightMapSize) / Math.Log(2)));
                    lightMapSize = Math.Min(maxLightMapResolution, Math.Max(lightMapSize, 16));

                    RenderTexture renderTexture = RenderTexture.GetTemporary(lightMapSize, lightMapSize, 0, RenderTextureFormat.ARGB32);
                    Graphics.SetRenderTarget(renderTexture);
                    GL.Clear(true, true, Color.red);

                    lightMap.SetVector("_LightMapUV", renderer.lightmapScaleOffset);
                    lightMap.SetPass(0);
                    Graphics.DrawMeshNow(mesh, world);

                    // This is the only way to access data from a RenderTexture
                    Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height);
                    tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    byte[] bytes = tex.EncodeToPNG();
                    string lightName = "Lightmap" + exp;
                    File.WriteAllBytes(Path.Combine(exportPath, lightName + ".png"), bytes);

                    ExportedMesh me = new ExportedMesh();
                    me.mesh = mesh;
                    me.lightMapPath = lightName;
                    me.go = obj;
                    exported.lightMapMeshes.Add(me);

                    Graphics.SetRenderTarget(null);
                    RenderTexture.ReleaseTemporary(renderTexture);
                    UnityEngine.Object.DestroyImmediate(tex);

                    exp++;
                }
            }
            UnityEngine.Object.DestroyImmediate(lightMap);


            // Make the index.html file
            StringBuilder index = new StringBuilder("<html>\n\t<head>\n\t\t<title>Unreal Export</title>\n\t</head>\n\t<body>\n\t\t<FireBoxRoom>\n\t\t\t<Assets>");

            List<string> written = new List<string>();

            for (int i = 0; i < exported.lightMapMeshes.Count; i++)
            {
                ExportedMesh expo = exported.lightMapMeshes[i];
                if (!written.Contains(expo.lightMapPath))
                {
                    index.Append("\n\t\t\t\t<AssetImage id=\"" + Path.GetFileNameWithoutExtension(expo.lightMapPath) + "\" src=\"" + expo.lightMapPath + ".png\" />");
                    written.Add(expo.lightMapPath);
                }

            }
            for (int i = 0; i < exported.lightMapMeshes.Count; i++)
            {
                ExportedMesh expo = exported.lightMapMeshes[i];
                if (!written.Contains(expo.mesh.name))
                {
                    index.Append("\n\t\t\t\t<AssetObject id=\"" + expo.mesh.name + "\" src=\"" + expo.mesh.name + ".fbx\" />");
                    written.Add(expo.mesh.name);
                }
            }
            index.Append("\n\t\t\t</Assets>\n\t\t\t<Room>");

            for (int i = 0; i < exported.exportedObjs.Count; i++)
            {
                ExportedObject obj = exported.exportedObjs[i];
                GameObject go = obj.go;

                ExportedMesh emesh = exported.lightMapMeshes.FirstOrDefault(x => x.go == go);
                string imageID = "";
                if (emesh.mesh != null)
                {
                    imageID = Path.GetFileNameWithoutExtension(emesh.lightMapPath);
                }
                if (string.IsNullOrEmpty(imageID))
                {
                    index.Append("\n\t\t\t\t<Object collision_id=\"" + obj.mesh.name + "\" id=\"" + obj.mesh.name + "\" lighting=\"true\" pos=\"");
                }
                else
                {
                    index.Append("\n\t\t\t\t<Object collision_id=\"" + obj.mesh.name + "\" id=\"" + obj.mesh.name + "\" image_id=\"" + imageID + "\" lighting=\"true\" pos=\"");
                }

                Transform trans = go.transform;
                Vector3 pos = trans.position;
                pos *= uniformScale;

                Quaternion rot = trans.rotation;
                Vector3 xDir = rot * Vector3.right;
                Vector3 yDir = rot * Vector3.up;
                Vector3 zDir = rot * Vector3.forward;

                Vector3 sca = trans.lossyScale;
                sca *= uniformScale;

                CultureInfo c = CultureInfo.InvariantCulture;

                index.Append(pos.x.ToString(c) + " " + pos.y.ToString(c) + " " + pos.z.ToString(c));
                if (sca.x < 0 || sca.y < 0 || sca.z < 0)
                {
                    index.Append("\" cull_face=\"front");
                }

                index.Append("\" scale=\"");
                index.Append(sca.x.ToString(c) + " " + sca.y.ToString(c) + " " + sca.z.ToString(c));

                index.Append("\" xdir=\"");
                index.Append(xDir.x.ToString(c) + " " + xDir.y.ToString(c) + " " + xDir.z.ToString(c));

                index.Append("\" ydir=\"");
                index.Append(yDir.x.ToString(c) + " " + yDir.y.ToString(c) + " " + yDir.z.ToString(c));

                index.Append("\" zdir=\"");
                index.Append(zDir.x.ToString(c) + " " + zDir.y.ToString(c) + " " + zDir.z.ToString(c));

                index.Append("\" />");

            }

            index.Append("\n\t\t\t</Room>\n\t\t</FireBoxRoom>\n\t</body>\n</html>");

            string indexPath = Path.Combine(exportPath, "index.html");
            File.WriteAllText(indexPath, index.ToString());
        }

        private void RecursiveSearch(GameObject root, ExportedData data)
        {
            Component[] comps = root.GetComponents<Component>();

            for (int i = 0; i < comps.Length; i++)
            {
                Component comp = comps[i];
                if (comp == null)
                {
                    continue;
                }

                if (comp is MeshRenderer)
                {
                    MeshRenderer meshRen = (MeshRenderer)comp;
                    MeshFilter filter = comps.FirstOrDefault(c => c is MeshFilter) as MeshFilter;
                    if (filter == null)
                    {
                        continue;
                    }

                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    // Only export the mesh if we never exported this one mesh
                    if (!data.exportedObjs.Any(c => c.mesh == mesh))
                    {
                        switch (meshFormat)
                        {
                            case ExportMeshFormat.FBX:
                                FBXExporter.ExportMesh(mesh, Path.Combine(exportPath, mesh.name + ".fbx"));
                                break;
                            case ExportMeshFormat.OBJ:
                                break;
                        }
                    }

                    ExportedObject exp = data.exportedObjs.FirstOrDefault(c => c.go == root);
                    if (exp == null)
                    {
                        exp = new ExportedObject();
                        exp.go = root;
                        data.exportedObjs.Add(exp);
                    }
                    exp.mesh = mesh;

                    int lightMap = meshRen.lightmapIndex;
                    if (lightMap != -1)
                    {
                        // Render lightmaps for this object
                        List<GameObject> toRender;
                        if (!lightmapped.TryGetValue(lightMap, out toRender))
                        {
                            toRender = new List<GameObject>();
                            lightmapped.Add(lightMap, toRender);
                        }

                        toRender.Add(root);
                    }
                }
                else if (comp is Collider)
                {
                    Collider col = (Collider)comp;

                    ExportedObject exp = data.exportedObjs.FirstOrDefault(c => c.go == root);
                    if (exp == null)
                    {
                        exp = new ExportedObject();
                        exp.go = root;
                        data.exportedObjs.Add(exp);
                    }
                    exp.col = col;
                }
            }

            foreach (Transform child in root.transform)
            {
                RecursiveSearch(child.gameObject, data);
            }
        }
    }
}
