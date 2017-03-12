﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Xml;

public class AndroidFixerEditorWindow : EditorWindow
{
    /// <summary>
    /// All the AAR files
    /// </summary>
    private FileInfo[] m_aarFiles;
    /// <summary>
    /// All the AndroidManifest.xml files
    /// </summary>
    private FileInfo[] m_xmlFiles;
    /// <summary>
    /// All the JAR files
    /// </summary>
    private FileInfo[] m_jarFiles;

    private bool m_showArrFiles = true;
    private bool m_showJarFiles = true;
    private bool m_showXmlFiles = true;

    private int m_minSDKVersion = 15;

    [MenuItem("XGameDev/Android Fixer")]
    public static void ShowWindow()
    {
        GetWindow(typeof(AndroidFixerEditorWindow));
    }

    void Update()
    {
        Repaint();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Check Android"))
        {
            string projectPath = Application.dataPath;

            m_aarFiles = getAllFilesInDir(projectPath, "*.aar");
            sortFiles(m_aarFiles);
            m_jarFiles = getAllFilesInDir(projectPath, "*.jar");
            sortFiles(m_jarFiles);
            m_xmlFiles = getAllFilesInDir(projectPath, "AndroidManifest.xml");
            sortFiles(m_xmlFiles);
        }

        GUILayout.Space(16);

        m_showArrFiles = EditorGUILayout.Toggle("Show Aar Files", m_showArrFiles);

        GUILayout.Space(16);

        if (m_showArrFiles)
        {

            if (GUILayout.Button("Check conflicts"))
            {
                checkAarFiles();
            }

            if (m_aarFiles != null)
            {
                for (int i = 0; i < m_aarFiles.Length; i++)
                {
                    EditorGUILayout.LabelField(string.Format("{0}:{1}", stripAssetPath(m_aarFiles[i].FullName), m_aarFiles[i].Length), EditorStyles.boldLabel);
                }
            }
            GUILayout.Space(16);
        }

        m_showJarFiles = EditorGUILayout.Toggle("Show JAR Files", m_showJarFiles);

        GUILayout.Space(16);

        if (m_showJarFiles)
        {

            if (GUILayout.Button("Check conflicts"))
            {
                checkJarFiles();
            }

            if (m_jarFiles != null)
            {
                for (int i = 0; i < m_jarFiles.Length; i++)
                {
                    EditorGUILayout.LabelField(string.Format("{0}:{1}", stripAssetPath(m_jarFiles[i].FullName), m_jarFiles[i].Length), EditorStyles.boldLabel);
                }
            }
            GUILayout.Space(16);
        }

        m_showXmlFiles = EditorGUILayout.Toggle("Show AndroidManifest Files", m_showXmlFiles);

        GUILayout.Space(16);

        if (m_showXmlFiles)
        {

            if (m_xmlFiles != null)
            {

                if (GUILayout.Button(string.Format("Change minSDKVersion to {0}", m_minSDKVersion)))
                {
                    changeMinSdkVersionInXMl();
                }

                for (int i = 0; i < m_xmlFiles.Length; i++)
                {

                    //int idx = m_xmlFiles [i].FullName.IndexOf ("Assets", StringComparison.InvariantCultureIgnoreCase);

                    //string path = m_xmlFiles [i].Name;

                    //if (idx >= 0) {
                    //	path = m_xmlFiles [i].FullName.Substring (idx);
                    //}

                    string path = stripAssetPath(m_xmlFiles[i].FullName);

                    EditorGUILayout.LabelField(path, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(string.Format("minSdkVersion {0}", processXml(m_xmlFiles[i].FullName)), EditorStyles.boldLabel);
                }
            }
        }
    }

    /// <summary>
    /// Gets all files in dir.
    /// </summary>
    /// <param name="dirPath">Dir path.</param>
    /// <param name="filter">Filter.</param>
    /// <param name="fileArr">File arr.</param>
    static FileInfo[] getAllFilesInDir(string dirPath, string filter)
    {
        DirectoryInfo dir = new DirectoryInfo(dirPath);
        return dir.GetFiles(filter, SearchOption.AllDirectories);
    }

    void sortFiles(FileInfo[] files)
    {
        Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name));
    }

    string stripAssetPath(string fullPath)
    {
        int idx = fullPath.IndexOf("Assets", StringComparison.InvariantCultureIgnoreCase);
        string path = fullPath;
        if (idx >= 0)
        {
            path = fullPath.Substring(idx);
        }
        return path;
    }

    bool checkIfVersion(string part)
    {
        if (string.IsNullOrEmpty(part)) {
            return false;
        }
        if ("release".Equals(part, StringComparison.InvariantCultureIgnoreCase) ||
            "debug".Equals(part, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }
        string[] numbers = part.Split('.');
        int x;
        for (int i = 0; i < numbers.Length; i++)
        {
            if (!int.TryParse(numbers[i], out x))
            {
                return false;
            }
        }
        return true;
    }

    #region AAR
    private void checkAarFiles()
    {
        checkFiles(m_aarFiles);
    }
    #endregion

    #region JAR
    private void checkJarFiles()
    {
        checkFiles(m_jarFiles);
    }

    private void checkFiles(FileInfo[] infiles)
    {
        if (infiles == null)
        { return; }
        Dictionary<string, string> files = new Dictionary<string, string>();
        for (int i = 0; i < infiles.Length; i++)
        {
            int idx = infiles[i].Name.LastIndexOf("-", StringComparison.InvariantCultureIgnoreCase);
            string name = infiles[i].Name;
            string version;
            if (idx >= 0)
            {
                version = infiles[i].Name.Substring(idx + 1, name.Length - idx - 1).Replace(infiles[i].Extension, "");
                if (!checkIfVersion(version))
                {
                    //Debug.LogFormat("Not a version {0}, n={1}", version, name);
                    continue;
                }
                name = infiles[i].Name.Substring(0, idx);
            }
            string requiredPathParts = Path.Combine("Plugins", "Android");
            if (infiles[i].FullName.Contains(requiredPathParts))
            {
                requiredPathParts = Path.Combine(requiredPathParts, infiles[i].Name);
                if (!infiles[i].FullName.EndsWith(requiredPathParts))
                {
                    requiredPathParts = Path.Combine("libs", infiles[i].Name);
                    if (!infiles[i].FullName.EndsWith(requiredPathParts))
                    {
                        requiredPathParts = Path.Combine("bin", infiles[i].Name);
                        if (!infiles[i].FullName.EndsWith(requiredPathParts))
                        {
                            Debug.LogWarningFormat("{0} file excluded from build (move to proper folder) {1}", infiles[i].Name, infiles[i].FullName);
                            continue;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarningFormat("{0} file excluded from build (move to proper folder) {1}", infiles[i].Name, infiles[i].FullName);
                continue;
            }
            if (!files.ContainsKey(name))
            {
                files.Add(name, infiles[i].FullName);
            }
            else
            {
                Debug.LogErrorFormat("{3} \"{0}\" already added:\n{1}\n{2}", name, files[name], infiles[i].FullName, infiles[i].Extension);
            }
        }
    }

    #endregion

    #region XML
    /// <summary>
    /// Processes the xml.
    /// </summary>
    /// <returns>The xml.</returns>
    /// <param name="pathToXml">Path to xml.</param>
    string processXml(string pathToXml)
    {

        //string pathToXml = m_xmlFiles[0].FullName;

        XmlDocument doc = new XmlDocument();
        doc.Load(pathToXml);

        XmlNode root = doc.DocumentElement;
        if (root == null)
        {
            return string.Empty;
        }

        XmlNode usesSdkNode = root.SelectSingleNode("uses-sdk");

        if (usesSdkNode != null && usesSdkNode.Attributes != null)
        {

            XmlAttribute att = usesSdkNode.Attributes["android:minSdkVersion"];

            if (att != null)
            {

                int minSdkVersion = int.Parse(att.Value);

                if (minSdkVersion > m_minSDKVersion)
                {
                    m_minSDKVersion = minSdkVersion;
                }

                return att.Value;
            }
            return string.Empty;
            //return usesSdkNode.Attributes ["android:minSdkVersion"].Value;
        }

        return string.Empty;
    }

    private void changeMinSdkVersionInXMl()
    {
        if (m_xmlFiles == null)
        {
            return;
        }
        for (int i = 0; i < m_xmlFiles.Length; i++)
        {

            XmlDocument doc = new XmlDocument();
            doc.Load(m_xmlFiles[i].FullName);

            XmlNode root = doc.DocumentElement;
            if (root == null)
            {
                return;
            }
            XmlNode usesSdkNode = root.SelectSingleNode("uses-sdk");

            if (usesSdkNode != null && usesSdkNode.Attributes != null)
            {

                XmlAttribute att = usesSdkNode.Attributes["android:minSdkVersion"];

                if (att != null)
                {

                    att.Value = m_minSDKVersion.ToString();
                }
            }

            //Debug.Log (m_xmlFiles[i].FullName);
            //doc.Save (m_xmlFiles[i].Name);

            doc.Save(m_xmlFiles[i].FullName);
        }
    }
    #endregion
}