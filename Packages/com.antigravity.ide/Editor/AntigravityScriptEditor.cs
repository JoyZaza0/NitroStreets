using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AntigravityScriptEditor : IExternalCodeEditor
{
    const string EditorName = "Antigravity";

    private static string GetWindowsIdePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe");
    }

    private static string GetWindowsHubPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", "antigravity", "Antigravity.exe");
    }

    private static string[] GetKnownPaths()
    {
        var paths = new List<string>
        {
            GetWindowsIdePath(),
            GetWindowsHubPath(),
            "/Applications/Antigravity IDE.app",
            "/Applications/Antigravity IDE.app/Contents/MacOS/Antigravity IDE",
            "/Applications/Antigravity.app",
            "/Applications/Antigravity.app/Contents/MacOS/Antigravity"
        };
        return paths.Where(p => !string.IsNullOrEmpty(p)).ToArray();
    }

    static AntigravityScriptEditor()
    {
        CodeEditor.Register(new AntigravityScriptEditor());
    }

    private static string GetExecutablePath(string path)
    {
        // On Windows, if the user selected Antigravity 2.0 Hub (Antigravity.exe),
        // prefer Antigravity IDE.exe if installed, since Antigravity 2.0 Hub has no code editor.
        string winIdePath = GetWindowsIdePath();
        if (File.Exists(winIdePath) &&
            !string.IsNullOrEmpty(path) &&
            path.EndsWith("Antigravity.exe", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("Antigravity IDE.exe", StringComparison.OrdinalIgnoreCase))
        {
            return winIdePath;
        }

        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            string ideExec = Path.Combine(path, "Contents", "MacOS", "Antigravity IDE");
            if (File.Exists(ideExec)) return ideExec;

            string executable = Path.Combine(path, "Contents", "MacOS", "Antigravity");
            return File.Exists(executable) ? executable : path;
        }
        return path;
    }

    public CodeEditor.Installation[] Installations
    {
        get
        {
            var installations = new List<CodeEditor.Installation>();
            foreach (var path in GetKnownPaths())
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    bool isIde = path.IndexOf("Antigravity IDE", StringComparison.OrdinalIgnoreCase) >= 0;
                    installations.Add(new CodeEditor.Installation
                    {
                        Name = isIde ? "Antigravity IDE" : EditorName,
                        Path = path
                    });
                }
            }
            return installations.ToArray();
        }
    }

    public void Initialize(string editorInstallationPath)
    {
    }

    public void OnGUI()
    {
        GUILayout.Label("Antigravity IDE Settings", EditorStyles.boldLabel);

        string winIdePath = GetWindowsIdePath();
        if (Application.platform == RuntimePlatform.WindowsEditor && !File.Exists(winIdePath))
        {
            EditorGUILayout.HelpBox(
                "Установлен Antigravity 2.0 (Agent Hub), который содержит только чат с агентом. " +
                "Для открытия и редактирования C#-скриптов требуется отдельное приложение Antigravity IDE.",
                MessageType.Warning);
        }

        if (GUILayout.Button("Regenerate project files", GUILayout.Width(180)))
        {
            ProjectGeneration.Sync();
        }
    }

    private static bool IsSupportedPath(string path)
    {
        // Path is empty with "Open C# Project"
        if (string.IsNullOrEmpty(path))
            return true;

        string extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
            return false;

        string extensionWithoutDot = extension.TrimStart('.');
        return extensionWithoutDot.Equals("asmdef", StringComparison.OrdinalIgnoreCase)
            || extensionWithoutDot.Equals("asmref", StringComparison.OrdinalIgnoreCase)
            || extensionWithoutDot.Equals("json", StringComparison.OrdinalIgnoreCase)
            || extensionWithoutDot.Equals("md", StringComparison.OrdinalIgnoreCase)
            || EditorSettings.projectGenerationBuiltinExtensions.Contains(extensionWithoutDot, StringComparer.OrdinalIgnoreCase)
            || EditorSettings.projectGenerationUserExtensions.Contains(extensionWithoutDot, StringComparer.OrdinalIgnoreCase);
    }

    public bool OpenProject(string filePath, int line, int column)
    {
        if (!IsSupportedPath(filePath))
            return false;

        string installation = CodeEditor.CurrentEditorInstallation;
        string executable = GetExecutablePath(installation);
        string projectDir = Directory.GetCurrentDirectory();

        if (string.IsNullOrEmpty(filePath))
        {
            filePath = projectDir;
        }

        string arguments;
        if (Directory.Exists(filePath))
        {
            arguments = $"-r \"{filePath}\"";
        }
        else
        {
            if (line > 0)
            {
                int safeCol = Math.Max(1, column);
                arguments = $"-r \"{projectDir}\" -g \"{filePath}:{line}:{safeCol}\"";
            }
            else
            {
                arguments = $"-r \"{projectDir}\" \"{filePath}\"";
            }
        }

        try
        {
            Process process = new Process();

            if (installation.EndsWith(".app", StringComparison.OrdinalIgnoreCase) &&
                Application.platform == RuntimePlatform.OSXEditor)
            {
                process.StartInfo.FileName = "/usr/bin/open";
                process.StartInfo.Arguments = $"-a \"{ executable }\" --args {arguments}";
            }
            else
            {
                process.StartInfo.FileName = executable;
                process.StartInfo.Arguments = arguments;
            }

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open Antigravity: {e.Message}");
            return false;
        }
    }

    public void SyncAll()
    {
        ProjectGeneration.Sync();
    }

    public void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        ProjectGeneration.SyncIfNeeded(addedAssets, deletedAssets, movedAssets, movedFromAssetPaths, importedAssets);
    }

    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
        if (!string.IsNullOrEmpty(editorPath) &&
            editorPath.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            bool isIde = editorPath.IndexOf("Antigravity IDE", StringComparison.OrdinalIgnoreCase) >= 0;
            installation = new CodeEditor.Installation
            {
                Name = isIde ? "Antigravity IDE" : EditorName,
                Path = editorPath
            };
            return true;
        }

        installation = default;
        return false;
    }
}
