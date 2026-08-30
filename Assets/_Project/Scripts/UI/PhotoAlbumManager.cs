// Assets/_Project/Scripts/UI/PhotoAlbumManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

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

    public void AddPhoto(Texture2D photo)
    {
        if (photo == null)
        {
            return;
        }

        photos.Add(photo);
        OnPhotoAdded?.Invoke(photo);
    }
}
