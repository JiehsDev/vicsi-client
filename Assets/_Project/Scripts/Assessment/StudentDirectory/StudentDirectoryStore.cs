// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentDirectoryStore.cs
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// File persistence for the headset's student directory: one JSON file,
/// &lt;persistentDataPath&gt;/vicsi_student_directory.json, written with the same
/// crash-safe temp-file-then-replace as the upload queue
/// (AssessmentUploadQueueStore.WriteAtomic) - a crash mid-write leaves the previous
/// directory intact, never a half-written one.
///
/// Plain class, not a MonoBehaviour, so tests and editor tooling can point it at any
/// folder. Holds no code in clear text (see CachedStudentEntry).
/// </summary>
public class StudentDirectoryStore
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "vicsi_student_directory.json";

    public string FilePath { get; }

    public StudentDirectoryStore(string rootDirectory = null)
    {
        string root = string.IsNullOrEmpty(rootDirectory) ? Application.persistentDataPath : rootDirectory;
        FilePath = Path.Combine(root, FileName);
    }

    /// <summary>
    /// The cached directory, or an empty one if there is no file yet. A file that can't
    /// be read or parsed is NOT deleted - it's renamed aside (…json.unreadable-&lt;time&gt;)
    /// and an empty directory is returned, so a damaged file degrades to "needs a sync",
    /// never "app won't start", and stays available for inspection.
    /// </summary>
    public StudentDirectoryCacheFile Load()
    {
        if (!File.Exists(FilePath))
        {
            return new StudentDirectoryCacheFile();
        }

        try
        {
            var file = JsonUtility.FromJson<StudentDirectoryCacheFile>(File.ReadAllText(FilePath));
            if (file == null || file.schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException($"unexpected schema version {(file == null ? "null" : file.schemaVersion.ToString())}");
            }
            file.students ??= new();
            file.sync ??= new();
            file.testStudent ??= new();
            return file;
        }
        catch (Exception ex)
        {
            string aside = FilePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try
            {
                File.Move(FilePath, aside);
            }
            catch (Exception)
            {
                // Leave it where it is; the next successful save replaces it.
            }
            Debug.LogError($"[StudentDirectoryStore] Cached student directory could not be read ({ex.GetType().Name}); starting empty until the next sync. Moved aside to {aside}.");
            return new StudentDirectoryCacheFile();
        }
    }

    public void Save(StudentDirectoryCacheFile file)
    {
        file.schemaVersion = CurrentSchemaVersion;
        AssessmentUploadQueueStore.WriteAtomic(FilePath, JsonUtility.ToJson(file, prettyPrint: true));
    }
}
