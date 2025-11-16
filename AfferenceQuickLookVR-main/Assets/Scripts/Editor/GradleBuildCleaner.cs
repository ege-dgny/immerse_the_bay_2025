using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Cleans Gradle build directories before building to prevent "Unable to delete directory" errors
/// This is a common Windows issue where Gradle cannot delete locked directories
/// </summary>
public class GradleBuildCleaner : IPreprocessBuildWithReport
{
    public int callbackOrder => 0; // Run early in the build process

    public void OnPreprocessBuild(BuildReport report)
    {
        // Only run for Android builds
        if (report.summary.platform != BuildTarget.Android)
            return;

        CleanGradleDirectories();
    }

    /// <summary>
    /// Pre-build cleanup - called before Gradle build starts
    /// </summary>
    [MenuItem("Tools/Clean Gradle Build Directories")]
    public static void CleanGradleDirectories()
    {
        string projectPath = Application.dataPath.Replace("/Assets", "");
        string gradleBasePath = Path.Combine(projectPath, "Library", "Bee", "Android", "Prj", "IL2CPP", "Gradle");

        if (!Directory.Exists(gradleBasePath))
        {
            Debug.Log("Gradle directory does not exist yet. Nothing to clean.");
            return;
        }

        // Directories that commonly cause deletion issues
        // We clean entire build directories to be more comprehensive
        string[] problematicPaths = new string[]
        {
            // Entire build directories (most comprehensive approach)
            Path.Combine(gradleBasePath, "launcher", "build"),
            Path.Combine(gradleBasePath, "unityLibrary", "build"),
            Path.Combine(gradleBasePath, "unityLibrary", "xrmanifest.androidlib", "build"),
            
            // Specific problematic subdirectories (as fallback)
            Path.Combine(gradleBasePath, "launcher", "build", "generated", "res", "resValues", "release"),
            Path.Combine(gradleBasePath, "unityLibrary", "build", "generated", "res", "resValues", "release"),
            Path.Combine(gradleBasePath, "unityLibrary", "xrmanifest.androidlib", "build", "generated", "res", "resValues", "release"),
            Path.Combine(gradleBasePath, "launcher", "build", "intermediates", "aar_metadata_check", "release"),
            Path.Combine(gradleBasePath, "unityLibrary", "build", "intermediates", "incremental", "release", "packageReleaseResources"),
            Path.Combine(gradleBasePath, "unityLibrary", "xrmanifest.androidlib", "build", "intermediates", "incremental", "release", "packageReleaseResources")
        };

        int cleanedCount = 0;
        // Process paths in reverse order (most specific first, then general)
        // This ensures we clean subdirectories before parent directories
        System.Array.Reverse(problematicPaths);
        
        foreach (string path in problematicPaths)
        {
            if (Directory.Exists(path))
            {
                bool success = false;
                // Retry logic for Windows file locking issues
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        // First, try to unlock files by removing read-only attributes
                        UnlockDirectory(path);
                        
                        // Try to delete the directory
                        Directory.Delete(path, true);
                        Debug.Log($"Successfully cleaned: {path}");
                        cleanedCount++;
                        success = true;
                        break;
                    }
                    catch (System.Exception ex)
                    {
                        if (retry < 2)
                        {
                            // Wait a bit before retrying (Windows file locks can be transient)
                            System.Threading.Thread.Sleep(100);
                            continue;
                        }
                        
                        Debug.LogWarning($"Could not delete {path} after retries: {ex.Message}");
                        // Try to delete individual files if directory deletion fails
                        try
                        {
                            DeleteDirectoryContents(path);
                            Debug.Log($"Cleaned contents of: {path}");
                            cleanedCount++;
                            success = true;
                        }
                        catch (System.Exception ex2)
                        {
                            Debug.LogError($"Failed to clean {path}: {ex2.Message}");
                        }
                    }
                }
            }
        }

        if (cleanedCount > 0)
        {
            Debug.Log($"Gradle build cleanup completed. Cleaned {cleanedCount} directory(ies).");
        }
        else
        {
            Debug.Log("Gradle build directories are already clean.");
        }
    }

    /// <summary>
    /// Removes read-only attributes from all files in a directory to unlock them
    /// </summary>
    private static void UnlockDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        try
        {
            // Remove read-only attribute from directory itself
            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            // Remove read-only attributes from all files
            string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                    }
                }
                catch
                {
                    // Ignore individual file errors
                }
            }
        }
        catch
        {
            // Ignore unlock errors, we'll try to delete anyway
        }
    }

    /// <summary>
    /// Recursively deletes all files and subdirectories in a directory
    /// </summary>
    private static void DeleteDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        // First unlock all files
        UnlockDirectory(directoryPath);

        // Delete all files
        string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not delete file {file}: {ex.Message}");
            }
        }

        // Delete all subdirectories
        string[] directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);
        // Delete in reverse order (deepest first)
        System.Array.Sort(directories);
        System.Array.Reverse(directories);
        foreach (string dir in directories)
        {
            try
            {
                Directory.Delete(dir, false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not delete directory {dir}: {ex.Message}");
            }
        }
    }
}

