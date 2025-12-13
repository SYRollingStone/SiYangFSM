#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SiYangFSM.EditorTools
{
    public class StateGeneratorWindow : EditorWindow
    {
        private const string MenuPath = "SiYangFramework/SiYangFSM/State Generator (Template)";
        private const string DefaultNamespace = "SiYangFSM";

        // 模板文件名：你会改成 SiYangFSM_StateTemplate（例如 SiYangFSM_StateTemplate.cs.txt）
        private const string TemplateSearchName = "SiYangFSM_StateTemplate";

        // EditorPrefs keys
        private const string PrefOutputFolder = "SiYangFSM.StateGen.OutputFolder";
        private const string PrefNamespace = "SiYangFSM.StateGen.Namespace";

        // UI fields
        private string _className = "NewState";
        private string _stateName = "NewState";
        private string _namespaceName = DefaultNamespace;
        private string _outputFolder = "Assets/_Generated/SiYangFSM/States";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<StateGeneratorWindow>("State Generator");
            win.minSize = new Vector2(600, 300);
            win.Show();
        }

        private void OnEnable()
        {
            _outputFolder = EditorPrefs.GetString(PrefOutputFolder, _outputFolder);
            _namespaceName = EditorPrefs.GetString(PrefNamespace, _namespaceName);
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generate State From Template", EditorStyles.boldLabel);

            EditorGUILayout.Space(8);

            _className = EditorGUILayout.TextField("Class Name", _className);
            _stateName = EditorGUILayout.TextField("State Name (base ctor)", _stateName);
            _namespaceName = EditorGUILayout.TextField("Namespace", _namespaceName);

            EditorGUILayout.Space(10);
            DrawOutputFolderPicker();

            EditorGUILayout.Space(10);

            var templatePath = TryResolveTemplatePath(out var templateHint, out var templateMsg);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(templateHint);
                if (!string.IsNullOrEmpty(templateMsg))
                    EditorGUILayout.LabelField(templateMsg, EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Ping Template (if found)", GUILayout.Height(22)))
                {
                    if (!string.IsNullOrEmpty(templatePath))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(templatePath);
                        if (obj != null)
                        {
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }
                }
            }

            EditorGUILayout.Space(12);

            using (new EditorGUI.DisabledScope(!CanGenerate(templatePath)))
            {
                if (GUILayout.Button("Generate", GUILayout.Height(38)))
                {
                    try
                    {
                        GenerateFromTemplate(templatePath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        EditorUtility.DisplayDialog("Generate Failed", e.Message, "OK");
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "要求：模板文件名需唯一且可被 AssetDatabase 搜索到。\n" +
                $"建议模板文件命名：{TemplateSearchName}.cs.txt\n\n" +
                "占位符：{{ClassName}} / {{StateName}} / {{Namespace}}",
                MessageType.Info);

            // 实时保存（体验更好）
            SavePrefs();
        }

        private void DrawOutputFolderPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Output Folder", _outputFolder);

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    var abs = EditorUtility.OpenFolderPanel("Select Output Folder", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        var assetsPath = ToAssetsRelativePath(abs);
                        if (assetsPath == null)
                        {
                            EditorUtility.DisplayDialog("Invalid Folder", "请选择 Assets 目录内的文件夹。", "OK");
                        }
                        else
                        {
                            _outputFolder = assetsPath;
                            SavePrefs();
                        }
                    }
                }
            }
        }

        private bool CanGenerate(string templatePath)
        {
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath)) return false;
            if (!IsValidIdentifier(_className)) return false;
            if (string.IsNullOrWhiteSpace(_stateName)) return false;
            if (string.IsNullOrWhiteSpace(_namespaceName)) return false;
            if (string.IsNullOrWhiteSpace(_outputFolder)) return false;
            if (!_outputFolder.StartsWith("Assets", StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// 方案 B：通过 AssetDatabase 搜索模板，要求唯一
        /// </summary>
        private static string TryResolveTemplatePath(out string hint, out string message)
        {
            hint = "Searching...";
            message = null;

            // 按 “名字 + 类型” 搜索，降低误匹配
            // 注意：FindAssets 对 name 匹配是模糊的，所以强烈建议文件名足够唯一
            var guids = AssetDatabase.FindAssets($"{TemplateSearchName} t:TextAsset");
            if (guids == null || guids.Length == 0)
            {
                hint = $"Not found: {TemplateSearchName} (TextAsset)";
                message = "请确认模板在项目/包中存在，并且 Unity 已导入为 TextAsset（.txt/.cs.txt 通常没问题）。";
                return null;
            }

            // 过滤：确保“文件名（不含扩展名）”严格等于 TemplateSearchName，避免模糊搜索命中多个
            var candidates = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var fileNameNoExt = Path.GetFileNameWithoutExtension(path); // 对于 .cs.txt 这里只会去掉 .txt => 得到 "xxx.cs"
                // 处理 .cs.txt 双扩展：再去一次
                if (fileNameNoExt.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    fileNameNoExt = Path.GetFileNameWithoutExtension(fileNameNoExt);

                if (string.Equals(fileNameNoExt, TemplateSearchName, StringComparison.Ordinal))
                    candidates.Add(path);
            }

            if (candidates.Count == 0)
            {
                hint = $"Found assets by fuzzy search, but none matched exact name: {TemplateSearchName}";
                message = "请确认模板文件名（不含扩展名）严格为 SiYangFSM_StateTemplate。";
                return null;
            }

            if (candidates.Count > 1)
            {
                hint = $"Multiple templates found: {candidates.Count}";
                message = "请保证模板唯一（删掉重复文件或改名）。\n命中的路径：\n- " + string.Join("\n- ", candidates);
                return null;
            }

            var templatePath = candidates[0];
            if (!File.Exists(templatePath))
            {
                hint = $"Resolved but missing on disk: {templatePath}";
                message = "可能是资源数据库未刷新或文件被移动。";
                return null;
            }

            hint = templatePath.StartsWith("Packages/", StringComparison.Ordinal)
                ? $"Found in Package: {templatePath}"
                : $"Found in Assets: {templatePath}";

            return templatePath;
        }

        private void GenerateFromTemplate(string templatePath)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Template not found: {templatePath}");

            var folder = _outputFolder.Replace("\\", "/").TrimEnd('/');
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
                throw new Exception("Output Folder 必须在 Assets/ 下。");

            if (!AssetDatabase.IsValidFolder(folder))
                CreateFolderRecursive(folder);

            var template = File.ReadAllText(templatePath, Encoding.UTF8);

            var tokens = new Dictionary<string, string>
            {
                ["ClassName"] = _className.Trim(),
                ["StateName"] = _stateName.Trim(),
                ["Namespace"] = _namespaceName.Trim(),
            };

            var output = ApplyTemplate(template, tokens);
            EnsureNoUnresolvedTokens(output);

            var outPath = $"{folder}/{_className.Trim()}.cs";

            if (File.Exists(outPath))
            {
                var ok = EditorUtility.DisplayDialog(
                    "File Exists",
                    $"文件已存在：\n{outPath}\n\n是否覆盖？",
                    "Overwrite",
                    "Cancel");
                if (!ok) return;
            }

            File.WriteAllText(outPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            Debug.Log($"[SiYangFSM] Generated: {outPath}");
        }

        private static string ApplyTemplate(string template, Dictionary<string, string> tokens)
        {
            // 匹配 {{Token}}
            return Regex.Replace(template, @"\{\{\s*(\w+)\s*\}\}", m =>
            {
                var key = m.Groups[1].Value;
                return tokens.TryGetValue(key, out var val) ? val : m.Value;
            });
        }

        private static void EnsureNoUnresolvedTokens(string content)
        {
            var m = Regex.Match(content, @"\{\{\s*\w+\s*\}\}");
            if (m.Success)
                throw new Exception($"Template contains unresolved token: {m.Value}\n请检查模板占位符或生成器 token 列表。");
        }

        private static bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            if (!(char.IsLetter(name[0]) || name[0] == '_')) return false;
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            }
            return true;
        }

        private static void CreateFolderRecursive(string folder)
        {
            // folder like: Assets/A/B/C
            var parts = folder.Split('/');
            if (parts.Length == 0) return;

            var current = parts[0]; // Assets
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAssetsRelativePath(string absolutePath)
        {
            absolutePath = absolutePath.Replace("\\", "/").TrimEnd('/');
            var dataPath = Application.dataPath.Replace("\\", "/").TrimEnd('/'); // .../Project/Assets

            if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return null;

            return "Assets" + absolutePath.Substring(dataPath.Length);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefOutputFolder, _outputFolder ?? string.Empty);
            EditorPrefs.SetString(PrefNamespace, _namespaceName ?? DefaultNamespace);
        }
    }
}
#endif
