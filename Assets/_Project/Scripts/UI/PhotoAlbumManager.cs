// Assets/_Project/Scripts/UI/PhotoAlbumManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum PhotoCaptureKind { Unspecified, Original, Final, ScaledFinal, Scene }

public sealed class PhotoRecord
{
    public Texture2D Texture { get; internal set; }
    public string SubjectId { get; internal set; }
    public string SubjectName { get; internal set; }
    public PhotoCaptureKind Kind { get; internal set; }
    public DateTime CapturedAtUtc { get; internal set; }
    public string StageLabel => Kind switch {
        PhotoCaptureKind.Original => "Original — without ruler",
        PhotoCaptureKind.ScaledFinal => "Final — with scale",
        PhotoCaptureKind.Final => "Final photograph",
        PhotoCaptureKind.Scene => "Scene photograph",
        _ => "Photograph"
    };
    public string Caption => (string.IsNullOrEmpty(SubjectName) ? "Photograph" : SubjectName) + " · " + StageLabel;
}

/// <summary>
/// Scene-wide store of every photo the player has captured, the same
/// Instance-singleton shape as EvidenceStateManager/NotificationManager
/// elsewhere in this project. Doesn't know anything about how a photo gets
/// taken or how it's displayed - PhotoCaptureListener adds photos here, and
/// PhotoAlbumUI listens for new ones to show as thumbnails.
/// </summary>
public class PhotoAlbumManager : MonoBehaviour
{
    public static PhotoAlbumManager Instance { get; private set; }

    /// <summary>Fired right after a new photo is added, with the newly captured texture.</summary>
    public event Action<Texture2D> OnPhotoAdded;

    private readonly List<Texture2D> photos = new();
    private readonly List<PhotoRecord> records = new();
    public IReadOnlyList<PhotoRecord> Records => records;
    public PhotoRecord FindRecord(Texture2D texture) => records.Find(r => r.Texture == texture);

    /// <summary>Every photo captured so far, oldest first.</summary>
    public IReadOnlyList<Texture2D> Photos => photos;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddPhoto(Texture2D photo, string subjectId = null, string subjectName = null, PhotoCaptureKind kind = PhotoCaptureKind.Unspecified)
    {
        if (photo == null)
        {
            return;
        }

        photos.Add(photo);
        records.Add(new PhotoRecord { Texture = photo, SubjectId = subjectId, SubjectName = subjectName,
            Kind = kind, CapturedAtUtc = DateTime.UtcNow });
        OnPhotoAdded?.Invoke(photo);
    }
}
