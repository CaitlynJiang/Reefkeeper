using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Reefkeeper.Photos
{
    /// <summary>
    /// The first photo of each species, saved as PNG under persistentDataPath/ReefPhotos so the
    /// photo wall survives restarts. A species with a photo is no longer "new".
    /// </summary>
    public static class ReefPhotoAlbum
    {
        static readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();
        static bool loaded;

        /// <summary>Raised with the species id when its photo is added (or with null after a reset).</summary>
        public static event Action<string> Changed;

        static string Folder => Path.Combine(Application.persistentDataPath, "ReefPhotos");
        static string FileOf(string id) => Path.Combine(Folder, id.Replace(' ', '_') + ".png");

        public static bool Has(string id) { Load(); return photos.ContainsKey(id); }
        public static Texture2D Get(string id) { Load(); return photos.TryGetValue(id, out var t) ? t : null; }
        public static int PhotoCount { get { Load(); return photos.Count; } }

        public static void Store(string id, Texture2D photo)
        {
            Load();
            if (!photo || photos.ContainsKey(id)) return;
            photos[id] = photo;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(FileOf(id), photo.EncodeToPNG());
            }
            catch (Exception e) { Debug.LogWarning($"[Photos] Could not save {id}: {e.Message}"); }
            Changed?.Invoke(id);
        }

        /// <summary>Forget every photo (files too): every species counts as new again.</summary>
        public static void ResetAll()
        {
            Load();
            foreach (var t in photos.Values) if (t) UnityEngine.Object.Destroy(t);
            photos.Clear();
            try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); }
            catch (Exception e) { Debug.LogWarning($"[Photos] Could not clear the album: {e.Message}"); }
            Changed?.Invoke(null);
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (var (id, _, _) in ReefSpecies.All)
            {
                string path = FileOf(id);
                if (!File.Exists(path)) continue;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Photo " + id, wrapMode = TextureWrapMode.Clamp };
                try { if (t.LoadImage(File.ReadAllBytes(path))) photos[id] = t; }
                catch (Exception e) { Debug.LogWarning($"[Photos] Could not load {path}: {e.Message}"); }
            }
        }

        // Domain reload may be off in the editor: start each play session from disk.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { photos.Clear(); loaded = false; Changed = null; }
    }
}
