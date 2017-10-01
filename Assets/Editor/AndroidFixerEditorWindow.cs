using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

public abstract class BaseFileInfo
{
    public static readonly string PluginsSlashAndroid = Path.Combine("Plugins", "Android");

    protected BaseFileInfo(FileInfo fileInfo)
    {
        Extension = fileInfo.Extension;
        FullPath = fileInfo.FullName;
        AssetPath = StripAssetPath(FullPath);
        Name = fileInfo.Name;
        DirectoryInfo parentFolderInfo = Directory.GetParent(FullPath);
        ParentFolderPath = parentFolderInfo.FullName;
        ParentFolderName = parentFolderInfo.Name;
        NameWithoutExtension = Path.GetFileNameWithoutExtension(FullPath);
        string requiredPathParts = PluginsSlashAndroid;
        if (FullPath.IndexOf(requiredPathParts, StringComparison.InvariantCultureIgnoreCase) >= 0)
        {
            if (!AssetPath.StartsWith(PluginsSlashAndroid))
            {
                string[] parts = Regex.Split(AssetPath, PluginsSlashAndroid.Replace(@"\", @"\\"), RegexOptions.IgnoreCase);
                if (parts.Length >= 1)
                {
                    IsInPluginFolder = true;
                    PluginFolderName = parts[0].Trim(Path.DirectorySeparatorChar);
                    //Debug.LogFormat("PluginName={0} FullPath={1}", PluginFolderName, FullPath);
                }
            }
            requiredPathParts = Path.Combine(PluginsSlashAndroid, Name);
            if (!FullPath.EndsWith(requiredPathParts, StringComparison.InvariantCultureIgnoreCase))
            {
                requiredPathParts = Path.Combine("libs", Name);
                if (!FullPath.EndsWith(requiredPathParts, StringComparison.InvariantCultureIgnoreCase))
                {
                    requiredPathParts = Path.Combine("bin", Name);
                    if (!FullPath.EndsWith(requiredPathParts, StringComparison.InvariantCultureIgnoreCase))
                    {
                        IgnoredFromBuild = true;
                    }
                }
            }
            else
            {
                IsInMainFolder = true;
            }
        }
        else
        {
            IgnoredFromBuild = true;
        }
    }

    //protected FileInfo fileInfo;
    //protected DirectoryInfo parentFolderInfo;
    public string Extension { get; protected set; }  // Jar or Aar
    public bool IgnoredFromBuild { get; protected set; }
    public string NameWithoutExtension { get; protected set; }
    public string Name { get; protected set; }
    public string FullPath { get; protected set; }
    public string AssetPath { get; protected set; }
    public string ParentFolderPath { get; protected set; }
    public string ParentFolderName { get; protected set; }
    public string PluginFolderName { get; protected set; }  // optional
    public bool IsInPluginFolder { get; protected set; }
    public bool IsInMainFolder { get; protected set; }

    public bool Unfold;

    private static string StripAssetPath(string fullPath)
    {
        string token = string.Format("Assets{0}", Path.DirectorySeparatorChar);
        int idx = fullPath.IndexOf(token, StringComparison.InvariantCultureIgnoreCase);
        string path = fullPath;
        if (idx >= 0)
        {
            path = fullPath.Substring(idx + token.Length);
        }
        return path;
    }
}

public class AndroidLibraryFileInfo : BaseFileInfo
{
    public AndroidLibraryFileInfo(FileInfo f) : base(f)
    {
        HasVersion = CheckVersion(NameWithoutExtension, out NameWithoutVersion, out Version);
    }

    public bool HasVersion;
    public string Version; // optional
    public string NameWithoutVersion;

    private static bool CheckIfVersion(string part)
    {
        if (string.IsNullOrEmpty(part))
        {
            return false;
        }
        part = part.Trim();
        //part = part.TrimStart('v');
        if ("release".Equals(part, StringComparison.InvariantCultureIgnoreCase) ||
            "debug".Equals(part, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }
        string[] numbers = part.Split(new[]{'.'}, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < numbers.Length; i++)
        {
            for (int j = 0; j < numbers[i].Length; j++)
            {
                if (!char.IsDigit(numbers[i][j]))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool CheckVersion(string nameWithoutExtension, out string nameWithOutVersion, out string version)
    {
        nameWithOutVersion = nameWithoutExtension.Trim().ToLowerInvariant();
        int idx = nameWithOutVersion.LastIndexOf("-", StringComparison.InvariantCultureIgnoreCase);
        if (idx >= 0)
        {
            version = nameWithOutVersion.Substring(idx + 1, nameWithOutVersion.Length - idx - 1);
            version = version.TrimStart('v', '.');
            if (CheckIfVersion(version))
            {
                nameWithOutVersion = nameWithoutExtension.Substring(0, idx);
                if (nameWithOutVersion.StartsWith("android"))
                {
                    nameWithOutVersion = nameWithOutVersion.Replace("android", string.Empty);
                }
                nameWithOutVersion = nameWithOutVersion.Trim('.', '-').Trim();
                return true;
            }
        }
        version = null;
        return false;
    }
}

public class AndroidManifestFileInfo : BaseFileInfo
{
    public bool IsMain { get; protected set; }

    public Dictionary<string, string> ApplicationAttributes;
    public Dictionary<string, string> UsesSdkAttributes;

    public AndroidManifestFileInfo(FileInfo f) : base(f)
    {
        IsMain = IsInMainFolder;
        ApplicationAttributes = GetApplicationAttributes(FullPath);
        UsesSdkAttributes = GetUsesSdksAttributes(FullPath);
    }

    private Dictionary<string, string> GetAttributes(string pathToXml, string node, string[] attributeNames)
    {
        if (string.IsNullOrEmpty(pathToXml))
        {
            throw new ArgumentException("XML file path argument is null or empty", "pathToXml");
        }
        if (!File.Exists(pathToXml))
        {
            throw new ArgumentException(string.Format("XML file {0} not found", pathToXml), "pathToXml");
        }
        if (string.IsNullOrEmpty(node))
        {
            throw new ArgumentException();
        }
        XmlDocument doc = new XmlDocument();
        doc.Load(pathToXml);

        XmlNode root = doc.DocumentElement;
        if (root == null)
        {
            return null;
        }

        XmlNode usesSdkNode = root.SelectSingleNode(node);

        if (usesSdkNode != null && usesSdkNode.Attributes != null)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(attributeNames.Length);
            for (int i = 0; i < attributeNames.Length; i++)
            {
                string attributeName = attributeNames[i];

                XmlAttribute att = usesSdkNode.Attributes[attributeName];

                if (att != null)
                {
                    result.Add(attributeName, att.Value);
                }
            }
            return result;
        }

        return null;
    }

    private Dictionary<string, string> GetApplicationAttributes(string pathToXml)
    {
        return GetAttributes(pathToXml, "application", new[] { "android:label", "android:icon", "android:debuggable" });
    }

    private Dictionary<string, string> GetUsesSdksAttributes(string pathToXml)
    {
        return GetAttributes(pathToXml, "uses-sdk", new[] { "android:targetSdkVersion", "android:minSdkVersion" });
    }


    private string GetAttribute(string pathToXml, string node, string attributeName)
    {
        if (string.IsNullOrEmpty(pathToXml))
        {
            throw new ArgumentException("XML file path argument is null or empty", "pathToXml");
        }
        if (!File.Exists(pathToXml))
        {
            throw new ArgumentException(string.Format("XML file {0} not found", pathToXml), "pathToXml");
        }
        if (string.IsNullOrEmpty(node))
        {
            throw new ArgumentException();
        }
        XmlDocument doc = new XmlDocument();
        doc.Load(pathToXml);

        XmlNode root = doc.DocumentElement;
        if (root == null)
        {
            return null;
        }

        XmlNode usesSdkNode = root.SelectSingleNode(node);

        if (usesSdkNode != null && usesSdkNode.Attributes != null)
        {
            XmlAttribute att = usesSdkNode.Attributes[attributeName];

            if (att != null)
            {
                return att.Value;
            }
        }

        return null;
    }

    private void ChangeMinSdkVersionInXMl(int minSdkVersion)
    {
        UpdateXml(FullPath, "uses-sdk", "android:minSdkVersion", minSdkVersion.ToString());
    }

    private bool UpdateXml(string pathToXml, string nodeName, Dictionary<string, string> updates)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(pathToXml);

        XmlNode root = doc.DocumentElement;
        if (root == null)
        {
            return false;
        }
        XmlNode node = root.SelectSingleNode(nodeName);

        if (node != null && node.Attributes != null)
        {
            foreach (var pair in updates)
            {
                XmlAttribute att = node.Attributes[pair.Key];
                if (att != null)
                {
                    att.Value = pair.Value;
                }
            }
        }
        doc.Save(pathToXml);
        return true;
    }

    private bool UpdateXml(string pathToXml, string nodeName, string attributeName, string attributeValue)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(pathToXml);

        XmlNode root = doc.DocumentElement;
        if (root == null)
        {
            return false;
        }
        XmlNode node = root.SelectSingleNode(nodeName);

        if (node != null && node.Attributes != null)
        {
            XmlAttribute att = node.Attributes[attributeName];
            if (att != null)
            {
                att.Value = attributeValue;
            }
        }
        doc.Save(pathToXml);
        return true;
    }

}

public class AndroidFixerEditorWindow : EditorWindow
{
    private FileInfo[] xmlFiles;

    private Vector2 _scrollPosition;
    private string[] tabs = { "AAR/JAR", "AndroidManifest", "SDK & Config" };
    private int selectedTab;

    private AndroidLibraryFileInfo[] androidLibraryFiles;
    private AndroidManifestFileInfo[] androidManifestFiles;
    private Dictionary<string, AndroidLibraryFileInfo> androidLibraryFilesUniqueNames;

    private AndroidSdkVersions minSDKVersion
    {
        get { return PlayerSettings.Android.minSdkVersion; }
    }

    [MenuItem("Android Tools/Troubleshooter")]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow(typeof(AndroidFixerEditorWindow));
#if UNITY_5_0
        window.title = "Android Troubleshooter";
#else
        window.titleContent = new GUIContent("Android Troubleshooter");
#endif
    }

    private void Update()
    {
        Repaint();
    }

    private void OnGUI() // change to callback when something changes
    {
        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30.0f));
        switch (selectedTab)
        {
            case 0:
                if (androidLibraryFiles == null)
                {
                    GetAndroidFiles();
                    SortFiles(androidLibraryFiles);
                }
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height - 50f));
                for (int i = 0; i < androidLibraryFiles.Length; i++)
                {
                    DisplayLibFileRow(androidLibraryFiles[i]);
                }
                EditorGUILayout.EndScrollView();
                break;
            case 1:
                if (androidManifestFiles == null)
                {
                    GetManifestFiles();
                    SortFiles(androidManifestFiles);
                }
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height - 50f));
                for (int i = 0; i < androidManifestFiles.Length; i++)
                {
                    DisplayManifestFileRow(androidManifestFiles[i]);
                }
                EditorGUILayout.EndScrollView();
                break;
        }
    }

    private void GetAndroidFiles()
    {
        string projectPath = Path.GetFullPath(Application.dataPath);
        // http://stackoverflow.com/a/163220/1449056
        FileInfo[] aarFiles = GetAllFilesInDir(projectPath, "*.aar");
        FileInfo[] jarFiles = GetAllFilesInDir(projectPath, "*.jar");
        int filesLength = aarFiles.Length + jarFiles.Length;
        androidLibraryFiles = new AndroidLibraryFileInfo[filesLength];
        androidLibraryFilesUniqueNames = new Dictionary<string, AndroidLibraryFileInfo>(filesLength);
        for (int i = 0; i < aarFiles.Length; i++)
        {
            androidLibraryFiles[i] = ProcessFile(aarFiles[i]);
        }
        for (int j = 0, i = aarFiles.Length; j < jarFiles.Length; j++, i++)
        {
            androidLibraryFiles[i] = ProcessFile(jarFiles[j]);
        }

    }

    private void GetManifestFiles()
    {
        string projectPath = Path.GetFullPath(Application.dataPath);
        FileInfo[] xmlFiles = GetAllFilesInDir(projectPath, "AndroidManifest.xml");
        androidManifestFiles = new AndroidManifestFileInfo[xmlFiles.Length];
        for (int i = 0; i < xmlFiles.Length; i++)
        {
            androidManifestFiles[i] = new AndroidManifestFileInfo(xmlFiles[i]);
        }
    }

    private void DisplayLibFileRow(AndroidLibraryFileInfo aFile)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        string foldoutText = aFile.NameWithoutVersion;
        if (aFile.HasVersion)
        {
            foldoutText = string.Format("{0} v.{1}", aFile.NameWithoutVersion, aFile.Version);
        }
        if (aFile.IsInPluginFolder)
        {
            foldoutText = string.Format("{0} ({1})", foldoutText, aFile.PluginFolderName);
        }
        GUIContent guiContent = new GUIContent(foldoutText, aFile.FullPath);
        aFile.Unfold = EditorGUILayout.Foldout(aFile.Unfold, guiContent);
        if (aFile.IgnoredFromBuild && GUILayout.Button("M", GUILayout.Width(20.0f)))
        {

        }
        if (!aFile.IgnoredFromBuild && androidLibraryFilesUniqueNames.ContainsKey(aFile.NameWithoutVersion) &&
            !androidLibraryFilesUniqueNames[aFile.NameWithoutVersion]
                .FullPath.Equals(aFile.FullPath) && GUILayout.Button("D", GUILayout.Width(20.0f)))
        {

        }
        if (GUILayout.Button("X", GUILayout.Width(20.0f)))
        {
            // delete file
            return;
        }
        EditorGUILayout.EndHorizontal();
        if (aFile.Unfold)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(string.Format("Name: \"{0}\"", aFile.NameWithoutVersion));
            EditorGUILayout.LabelField(string.Format("Version: \"{0}\"", aFile.Version));
            EditorGUILayout.LabelField(string.Format("Type: \"{0}\"", aFile.Extension));
            EditorGUILayout.LabelField(string.Format("Path: \"{0}\"", aFile.AssetPath));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
    }

    private void DisplayManifestFileRow(AndroidManifestFileInfo mFile)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        string foldoutText = mFile.AssetPath;
        if (mFile.IsInPluginFolder)
        {
            foldoutText = string.Format("{0} ({1})", foldoutText, mFile.PluginFolderName);
        }
        GUIContent guiContent = new GUIContent(foldoutText, mFile.FullPath);
        mFile.Unfold = EditorGUILayout.Foldout(mFile.Unfold, guiContent);
        if (mFile.IgnoredFromBuild && GUILayout.Button("M", GUILayout.Width(20.0f)))
        {

        }
        if (GUILayout.Button("X", GUILayout.Width(20.0f)))
        {
            // delete file
            return;
        }
        EditorGUILayout.EndHorizontal();
        if (mFile.Unfold)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(string.Format("Path: \"{0}\"", mFile.AssetPath));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
    }

    private AndroidLibraryFileInfo ProcessFile(FileInfo fileInfo)
    {
        AndroidLibraryFileInfo androidLibraryFile = new AndroidLibraryFileInfo(fileInfo);
        if (androidLibraryFile.IgnoredFromBuild)
        {
            Debug.LogWarningFormat("{0} ignored", androidLibraryFile.FullPath);
            return androidLibraryFile;
        }
        if (androidLibraryFilesUniqueNames.ContainsKey(androidLibraryFile.NameWithoutVersion))
        {
            // compare versions
            Debug.LogErrorFormat("Duplicate file: {0} {1}", androidLibraryFilesUniqueNames[androidLibraryFile.NameWithoutVersion].NameWithoutExtension,
                androidLibraryFile.NameWithoutExtension);
        }
        else
        {
            androidLibraryFilesUniqueNames.Add(androidLibraryFile.NameWithoutVersion, androidLibraryFile);
        }
        return androidLibraryFile;
    }

    /// <summary>
    /// Gets all files in dir.
    /// </summary>
    /// <param name="dirPath">Dir path.</param>
    /// <param name="filter">Filter.</param>
    /// <param name="fileArr">File arr.</param>
    private static FileInfo[] GetAllFilesInDir(string dirPath, string filter)
    {
        DirectoryInfo dir = new DirectoryInfo(dirPath);
        return dir.GetFiles(filter, SearchOption.AllDirectories);
    }

    private void SortFiles(AndroidLibraryFileInfo[] files)
    {
        Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.NameWithoutVersion, y.NameWithoutVersion));
    }

    private void SortFiles(AndroidManifestFileInfo[] files)
    {
        Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.AssetPath, y.AssetPath));
    }

    private void ScanAndroidFolders(string path)
    {
        string[] folders = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
        string requiredPart = BaseFileInfo.PluginsSlashAndroid;
        for (int i = 0; i < folders.Length; i++)
        {
            if (folders[i].Contains(requiredPart))
            {
                Debug.Log(folders[i]);
            }
            // check "res" folders
            // check libraries (there should be a manifest and android.library=true in project.properties)
            // check "libs" and "bin" folders
            // save plugin root folders
        }
    }

    //private int compareVersions(string a, string b)
    //{
    //    if (!checkIfVersion(a))
    //    {
    //        throw new ArgumentException(string.Format("Unexpected version: {0}", a.Stringify()), "a");
    //    }
    //    if (!checkIfVersion(b))
    //    {
    //        throw new ArgumentException(string.Format("Unexpected version: {0}", b.Stringify()), "b");
    //    }
    //    if (a.Equals(b))
    //    {
    //        return 0;
    //    }
    //    if (a.Equals("release") && b.Equals("debug"))
    //    {
    //        return 1;
    //    }
    //    if (b.Equals("release") && a.Equals("debug"))
    //    {
    //        return -1;
    //    }
    //    string[] partsA = a.Trim().TrimStart('v').Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
    //    string[] partsB = b.Trim().TrimStart('v').Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
    //    int lengthA = partsA.Length;
    //    int lengthB = partsB.Length;
    //    int min = lengthA;
    //    int diff = lengthA - lengthB;
    //    if (lengthA > lengthB)
    //    {
    //        min = lengthB;
    //    }
    //    for (int i = 0; i < min; i++)
    //    {
    //        int xA = int.Parse(partsA[i]);
    //        int yB = int.Parse(partsB[i]);
    //        if (xA > yB)
    //        {
    //            return i;
    //        }
    //        if (yB < xA)
    //        {
    //            return -i;
    //        }
    //    }
    //    return diff;
    //}
}