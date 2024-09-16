using UnityEditor;
using UnityEngine;
using System;
using System.IO;

namespace Memori.Steamworks
{
    #if UNITY_EDITOR
public class SteamUploader : EditorWindow
{
    [SerializeField] string pathToBuiltProject;
    [SerializeField] string STEAM_USERNAME;
    [SerializeField] string STEAM_PASSWORD;
    [SerializeField] string appID;
    [SerializeField] string depotID;
    public enum SteamBuild { Playtest, Demo, Release}
    [SerializeField] private SteamBuild build;
    private readonly string SteamPlaytestAppId = "YOUR_APP_ID";
    private readonly string SteamDemoAppId = "YOUR_APP_ID";
    private readonly string SteamReleaseAppId = "YOUR_APP_ID";

    string description;

    [MenuItem("Window/Steam Uploader")]
    public static void ShowWindow()
    {
        SteamUploader window = GetWindow<SteamUploader> ();
        Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ($"{RootPath}/steamLogo.png");
        GUIContent titleContent = new("Steam Uploader", icon);
        window.titleContent = titleContent;
    }
    protected void OnEnable ()
    {
        // Here we retrieve the data if it is cached
        var data = EditorPrefs.GetString("SteamUploaderWindow", JsonUtility.ToJson(this, false));
        JsonUtility.FromJsonOverwrite(data, this);
    }

    protected void OnDisable ()
    {
        // caching the values
        var data = JsonUtility.ToJson(this, false);
        EditorPrefs.SetString("SteamUploaderWindow", data);
    }
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Build Type", EditorStyles.label);
        build = (SteamBuild)EditorGUILayout.EnumPopup(build);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Build description", EditorStyles.label);
        description = EditorGUILayout.TextArea(description, GUILayout.Height(50), GUILayout.Width(300));
        EditorGUILayout.EndHorizontal();

        
        GUILayout.Label("Steam Login", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Steam username", EditorStyles.label);
        STEAM_USERNAME = EditorGUILayout.TextField("", STEAM_USERNAME);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Steam password", EditorStyles.label);
        STEAM_PASSWORD = EditorGUILayout.PasswordField("", STEAM_PASSWORD);
        EditorGUILayout.EndHorizontal();


        GUILayout.Label("Depot Configuration", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Build Location", EditorStyles.label);
        pathToBuiltProject = EditorGUILayout.TextField("", pathToBuiltProject);
        if(GUILayout.Button("Browse"))
        {
            pathToBuiltProject = EditorUtility.OpenFolderPanel("Browse", "", "");
        }
        // buildOutput = EditorUtility.OpenFolderPanel("Browse", "", "");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Upload to Steam"))
        {
            Upload(BuildTarget.StandaloneWindows64, pathToBuiltProject, STEAM_USERNAME, STEAM_PASSWORD, GetAppId(), description);
        }
        if(GUILayout.Button("View Uploaded Builds on Steam"))
        {
            Application.OpenURL($"https://partner.steamgames.com/apps/builds/{GetAppId()}");
        }
        EditorGUILayout.EndHorizontal();
    }
    private static void Upload(BuildTarget target, string pathToBuiltProject, string STEAM_USERNAME, string STEAM_PASSWORD, string APP_ID, string DESCRIPTION)
    {
        if (target != BuildTarget.StandaloneWindows64) {
            Debug.LogError($"Target not supported: {target}");
            return;
        }

        var dir = pathToBuiltProject.Replace($"{Application.productName}.exe", "");

        // Debug.Log($"RootPath: {RootPath}");
        Debug.Log($"ContentBuilderPath: {ContentBuilderPath}");
        Debug.Log($"PathToBuiltProject: {pathToBuiltProject}");
        Debug.Log($"Post build path: {dir}");

        // edit the desc of this file "{ContentBuilderPath}\\ContentBuilder\\scripts\\app_{APP_ID}.vdf\" "
        var vdfPath = $"{ContentBuilderPath}\\ContentBuilder\\scripts\\app_{APP_ID}.vdf";
        var vdf = File.ReadAllText(vdfPath);
        var vdfNew = vdf.Replace("description", DESCRIPTION);
        File.WriteAllText(vdfPath, vdfNew);

        // remove things
        // Directory.Delete($"{dir}\\{Application.productName}_BurstDebugInformation_DoNotShip", true);

        // copy files into steam builder content
        var moveToDir = $"{ContentBuilderPath}\\ContentBuilder\\content\\";
        Directory.Delete(moveToDir, true);
        // need copy all files and not use Directory.Move() as it will empty the build directory resulting in a failed build on UCB
        CopyFilesRecursively(dir, moveToDir);
        Debug.Log($"Moved: {dir} to {moveToDir}");

        // start the steam upload
        var StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = $"{ContentBuilderPath}\\ContentBuilder\\builder\\steamcmd.exe",
            Arguments = $"+login {STEAM_USERNAME} {STEAM_PASSWORD} " +
                        $"+run_app_build \"{ContentBuilderPath}\\ContentBuilder\\scripts\\app_{APP_ID}.vdf\" " +
                        "+quit"
        };

        using var steamBuildScript = System.Diagnostics.Process.Start(StartInfo);

        if (steamBuildScript == null)
        {
            Debug.LogError($"Process failed to start. {StartInfo.FileName}");
            return;
        }
        File.WriteAllText(vdfPath, vdf);

        // Debug.Log($"Steam builder started: {steamBuildScript.StartInfo.FileName}");

        // steamBuildScript.WaitForExit();
        // steamBuildScript.WaitForInputIdle();
        // var output = steamBuildScript.StandardOutput.ReadToEnd();
        // var error = steamBuildScript.StandardError.ReadToEnd();

        // if (!string.IsNullOrEmpty(error))
        //     Debug.LogError($"Steam builder error: {error}");
        // else
        //     Debug.Log($"Steam builder completed. {output}");
    }
    private string GetAppId()
    {
        return build switch
        {
            SteamBuild.Playtest => SteamPlaytestAppId,
            SteamBuild.Demo => SteamDemoAppId,
            SteamBuild.Release => SteamReleaseAppId,
            _ => SteamPlaytestAppId,
        };
    }
    
    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        // Now Create all of the directories
        foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

        // Copy all the files & Replaces any files with the same name
        foreach (string newPath in Directory.GetFiles(sourcePath, "*.*",SearchOption.AllDirectories))
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
    }
    public static string RootPath
    {
        get
        {
            var g = AssetDatabase.FindAssets ( $"t:Folder {nameof(SteamUploader)}" );
            return AssetDatabase.GUIDToAssetPath ( g [ 0 ] );
        }
    }
    public static string ContentBuilderPath
    {
        get
        {
            return Environment.CurrentDirectory;
        }
    }
}
#endif
}
