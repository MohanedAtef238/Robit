using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

[Serializable]
public class UserProfile {
    public string id;
    public string name;
    public string pfpPath;
    public string lastCalibrationDate;
    public bool isCalibrated;
}

[Serializable]
public class ProfileData {
    public List<UserProfile> profiles = new List<UserProfile>();
}

public static class ProfileManager {
    public const int MaxProfiles = 3;
    private static string FilePath => Path.Combine(Application.persistentDataPath, "profiles.json");
    
    public static ProfileData LoadProfiles() {
        if (!File.Exists(FilePath)) return new ProfileData();
        string json = File.ReadAllText(FilePath);
        return JsonUtility.FromJson<ProfileData>(json) ?? new ProfileData();
    }
    
    public static void SaveProfiles(ProfileData data) {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }
    
    public static UserProfile CreateProfile(string name, List<UserProfile> existingProfiles = null) {
        Sprite[] sprites = Resources.LoadAll<Sprite>("ProfilePictures");
        string randomPfp = "";
        
        if (sprites != null && sprites.Length > 0) {
            HashSet<string> usedPfps = new HashSet<string>();
            if (existingProfiles != null) {
                foreach (var p in existingProfiles) {
                    if (!string.IsNullOrEmpty(p.pfpPath)) {
                        usedPfps.Add(p.pfpPath);
                    }
                }
            }
            
            List<Sprite> availableSprites = new List<Sprite>();
            foreach (var s in sprites) {
                if (!usedPfps.Contains(s.name)) {
                    availableSprites.Add(s);
                }
            }
            
            // Fallback to all if somehow we run out
            if (availableSprites.Count == 0) {
                availableSprites.AddRange(sprites);
            }
            
            randomPfp = availableSprites[UnityEngine.Random.Range(0, availableSprites.Count)].name;
        }
        
        return new UserProfile {
            id = Guid.NewGuid().ToString(),
            name = name,
            pfpPath = randomPfp,
            lastCalibrationDate = "Never",
            isCalibrated = false
        };
    }
}
