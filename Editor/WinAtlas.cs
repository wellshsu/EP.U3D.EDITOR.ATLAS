//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using EP.U3D.EDITOR.BASE;
using System.Reflection;

namespace EP.U3D.EDITOR.ATLAS
{
    public class WinAtlas : EditorWindow
    {
        [MenuItem(Constants.MENU_WIN_ATLASEASE, false, 2)]
        public static void Invoke()
        {
            if (Instance == null)
            {
                GetWindowWithRect<WinAtlas>(new Rect(30, 30, 285, 450), true, "Atlas Ease");
            }
            Refresh();
            float invokeInterval = Time.realtimeSinceStartup - lastInvokeTime;
            if (invokeInterval < 0.5f)
            {
                string latestEditPrefabPath = EditorPrefs.GetString(LATEST_EDIT_ATLAS);
                HandleReimport(latestEditPrefabPath);
            }
            lastInvokeTime = Time.realtimeSinceStartup;
            if (Instance != null) Instance.OnSelectChange();
        }

        private static string LATEST_EDIT_ATLAS = Path.GetFullPath("./") + "LATEST_EDIT_ATLAS";
        public static WinAtlas Instance;
        private static List<string> prefabPaths = new List<string>();
        private static List<GameObject> prefabs = new List<GameObject>();
        private static Dictionary<string, bool> selected = new Dictionary<string, bool>();
        private static float lastInvokeTime;
        private Vector2 scroll = Vector2.zero;
        private bool selectAll = false;
        private bool lastSelectAll = false;
        private static string searchStr = "";
        private static float unsearchHeigth;

        [InitializeOnLoadMethod]
        private static void SetTPEnv()
        {
            var pkg = Helper.FindPackage(Assembly.GetExecutingAssembly());
            var clazzSettingsDatabase = typeof(TexturePackerImporter.SettingsDatabase);
            var fieldDatabaseFilePath = clazzSettingsDatabase.GetField("databaseFilePath", BindingFlags.NonPublic | BindingFlags.Static);
            var fieldAssetFilePath = clazzSettingsDatabase.GetField("assetFilePath", BindingFlags.NonPublic | BindingFlags.Static);
            fieldDatabaseFilePath.SetValue(null, pkg.resolvedPath + "/Editor/Libs/SettingsTexturePackerImporter.txt");
            fieldAssetFilePath.SetValue(null, pkg.resolvedPath + "/Editor/Libs/SettingsTexturePackerImporter.asset");
        }

        private void OnEnable()
        {
            Instance = this;
            lastInvokeTime = 0;
            selectAll = false;
            lastSelectAll = false;
            Refresh();
            Selection.selectionChanged += OnSelectChange;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectChange;
        }

        private void OnSelectChange()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            var index = prefabPaths.IndexOf(path);
            if (index > 0)
            {
                if (!string.IsNullOrEmpty(searchStr))
                {
                    unsearchHeigth = index * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2);
                }
            }
        }

        private void OnDestroy()
        {
            Instance = null;
            prefabPaths.Clear();
            prefabs.Clear();
            selected.Clear();
        }

        private void OnGUI()
        {
            if (prefabPaths.Count == 0 || prefabs.Count == 0) return;

            Helper.BeginContents();

            searchStr = Helper.SearchField(searchStr, GUILayout.Height(20));
            GUILayout.BeginHorizontal();
            GUILayout.Space(3);
            selectAll = GUILayout.Toggle(selectAll, "", GUILayout.Width(15));
            bool overrideAll = selectAll != lastSelectAll;
            lastSelectAll = selectAll;
            GUILayout.Label("Item", GUILayout.Width(168));
            GUILayout.Label("Operate");
            GUILayout.EndHorizontal();
            Helper.EndContents();

            if (string.IsNullOrEmpty(searchStr))
            {
                scroll.y = unsearchHeigth;
            }
            scroll = GUILayout.BeginScrollView(scroll);
            if (string.IsNullOrEmpty(searchStr))
            {
                unsearchHeigth = scroll.y;
            }
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                if (prefabs[i].name.IndexOf(searchStr, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                GUILayout.BeginHorizontal();
                if (overrideAll)
                {
                    selected[path] = selectAll;
                }
                selected[path] = GUILayout.Toggle(selected[path], "", GUILayout.Width(15));
                EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false, GUILayout.Width(145));
                if (GUILayout.Button("Show", GUILayout.Width(45)))
                {
                    HandleSelect(prefabPaths[i]);
                    OnSelectChange();
                }
                if (GUILayout.Button("Reimport", GUILayout.Width(65)))
                {
                    Helper.OnlyWindows(() =>
                     {
                         HandleReimport(path);
                     });
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.Space(3);
            if (GUILayout.Button("Reimport Selected"))
            {
                Helper.OnlyWindows(() =>
                 {
                     HandleReimport();
                 });
            }
            if (GUILayout.Button("Create New Atlas"))
            {
                string root = Constants.ATLAS_BUNDLE_PATH;
                string str = EditorUtility.SaveFilePanel("Create New Atlas", root, "NewAtlas", "prefab");
                if (string.IsNullOrEmpty(str)) return;
                if (str.IndexOf(root) < 0)
                {
                    string toast = Helper.StringFormat("Atlas must be located in '{0}', selected: '{1}'", root, str);
                    Helper.LogError(toast);
                    Helper.ShowToast(toast);
                    Helper.DeleteFile(str);
                    return;
                }
                GameObject go = new GameObject();
                go.AddComponent<Atlas>();
                PrefabUtility.SaveAsPrefabAsset(go, str);
                AssetDatabase.Refresh();
                Refresh();
                HandleSelect(str);
                str = str.Substring(Constants.ATLAS_BUNDLE_PATH.Length);
                str = str.Substring(0, str.LastIndexOf('.'));
                string src = Constants.ATLAS_SPRITE_PATH + str;
                if (!Helper.HasDirectory(src)) Helper.CreateDirectory(src);
                Helper.ShowInExplorer(src);
            }
        }

        private static void Refresh()
        {
            prefabPaths.Clear();
            prefabs.Clear();
            selected.Clear();
            var itr = Constants.ATLAS_EXTRAS.GetEnumerator();
            while (itr.MoveNext())
            {
                var kvp = itr.Current;
                if (Helper.HasFile(kvp.Key))
                {
                    prefabPaths.Add(kvp.Key);
                }
            }
            Helper.CollectAssets(Constants.ATLAS_BUNDLE_PATH, prefabPaths, ".cs", ".js", ".meta", ".DS_Store");
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                if (File.Exists(path) && path.EndsWith(".prefab"))
                {
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go)
                    {
                        Atlas a = go.GetComponent<Atlas>();
                        if (a)
                        {
                            prefabs.Add(go);
                        }
                        else
                        {
                            prefabPaths.RemoveAt(i);
                            i--;
                        }
                    }
                    else
                    {
                        prefabPaths.RemoveAt(i);
                        i--;
                    }
                }
                else
                {
                    prefabPaths.RemoveAt(i);
                    i--;
                }
            }
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                if (selected.ContainsKey(path) == false)
                {
                    selected.Add(path, false);
                }
            }
        }

        private static void HandleSelect(string prefabPath)
        {
            if (File.Exists(prefabPath))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab)
                {
                    Type clazzProjectBrowser = Helper.GetUnityEditorClazz("UnityEditor.ProjectBrowser");
                    FocusWindowIfItsOpen(clazzProjectBrowser);
                    AssetDatabase.OpenAsset(prefab);
                    Selection.activeObject = prefab;
                }
            }
        }

        private static void HandleReimport(string single = "")
        {
            var pkg = Helper.FindPackage(Assembly.GetExecutingAssembly());
            SetTPEnv();
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                GameObject prefab = prefabs[i];
                bool sig = path == single;
                if (prefab && (sig || selected[path]))
                {
                    string rawPath;
                    if (Constants.ATLAS_EXTRAS.ContainsKey(path))
                    {
                        rawPath = Constants.ATLAS_EXTRAS[path];
                    }
                    else
                    {
                        string temp = Path.GetFullPath(path);
                        temp = Helper.NormalizePath(temp);
                        rawPath = temp.Substring(Constants.ATLAS_BUNDLE_PATH.Length);
                        rawPath = rawPath.Substring(0, rawPath.LastIndexOf('.'));
                    }
                    rawPath = Constants.ATLAS_SPRITE_PATH + rawPath;
                    if (Directory.Exists(rawPath))
                    {
                        Process cmd = new Process();
                        cmd.StartInfo.FileName = pkg.resolvedPath + "/Editor/Libs/pktps.cmd";
                        string argFile = rawPath + ".arg";
                        string arg = "--format unity-texture2d --force-publish --trim --disable-auto-alias --disable-rotation --force-squared";
                        if (File.Exists(argFile))
                        {
                            arg = File.ReadAllText(argFile);
                        }
                        cmd.StartInfo.Arguments = string.Format("{0} {1}", rawPath, arg);
                        cmd.Start();
                        cmd.WaitForExit();
                        string rawAtlasPng = rawPath + ".png";
                        string rawAtlasTpsheet = rawPath + ".tpsheet";
                        if (File.Exists(rawAtlasPng) && File.Exists(rawAtlasTpsheet))
                        {
                            // 剔除注释信息
                            string[] lines = File.ReadAllLines(rawAtlasTpsheet);
                            List<string> nlines = new List<string>();
                            for (int j = 0; j < lines.Length; j++)
                            {
                                string line = lines[j];
                                if (line.StartsWith("#"))
                                {
                                    // elimate SmartUpdate
                                }
                                else
                                {
                                    nlines.Add(line);
                                }
                            }
                            File.WriteAllLines(rawAtlasTpsheet, nlines.ToArray());

                            // 根据tpsheet重新合成图片（crack tp）
                            Process cmd2 = new Process();
                            cmd2.StartInfo.FileName = pkg.resolvedPath + "/Editor/Libs/untps.exe";
                            cmd2.StartInfo.Arguments = string.Format("-s \"{0}\" -c \"{1}\"", rawPath, rawAtlasTpsheet);
                            cmd2.Start();
                            cmd2.WaitForExit();

                            string atlasDst = Helper.NormalizePath(Path.GetFullPath(Path.GetDirectoryName(path)));
                            string atlasName = Path.GetFileNameWithoutExtension(path);
                            string atlasPng = string.Format("{0}/{1}.png", atlasDst, atlasName);
                            string atlasTpsheet = string.Format("{0}/{1}.tpsheet", atlasDst, atlasName);
                            string atlasPrefab = string.Format("{0}/{1}.prefab", atlasDst, atlasName);
                            if (Helper.HasDirectory(atlasDst) == false)
                            {
                                Helper.CreateDirectory(atlasDst);
                            }
                            File.Copy(rawAtlasPng, atlasPng, true);
                            File.Copy(rawAtlasTpsheet, atlasTpsheet, true);
                            atlasPng = atlasPng.Substring(Application.dataPath.Length + 1);
                            atlasPng = "Assets/" + atlasPng;
                            atlasPrefab = atlasPrefab.Substring(Application.dataPath.Length + 1);
                            atlasPrefab = "Assets/" + atlasPrefab;
                            AssetDatabase.ImportAsset(atlasPng);

                            Sprite[] sps = AssetDatabase.LoadAllAssetsAtPath(atlasPng).OfType<Sprite>().ToArray();
                            GameObject obj = AssetDatabase.LoadAssetAtPath(atlasPrefab, typeof(GameObject)) as GameObject;
                            Atlas atlas = obj.GetComponent<Atlas>();
                            if (atlas == null) atlas = obj.AddComponent<Atlas>();
                            atlas.sprites = sps;
                            PrefabUtility.SavePrefabAsset(obj);

                            AssetDatabase.Refresh();
                            Helper.Log("[FILE@{0}] Reimport success.", rawPath);
                            Helper.ShowToast("Reimport atlas success.");
                        }
                    }
                    else
                    {
                        Helper.LogError("{0} does not exists.", rawPath);
                    }
                    EditorPrefs.SetString(LATEST_EDIT_ATLAS, path);
                    if (sig) break;
                }
            }
        }
    }
}
