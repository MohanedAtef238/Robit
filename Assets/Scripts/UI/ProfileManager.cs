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
    public const int MaxProfiles = 4;
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
    
    public static UserProfile CreateProfile(string name) {
        string[] placeholders = { "placeholder1", "placeholder2", "placeholder3" };
        string randomPfp = placeholders[UnityEngine.Random.Range(0, placeholders.Length)];
        
        return new UserProfile {
            id = Guid.NewGuid().ToString(),
            name = name,
            pfpPath = randomPfp,
            lastCalibrationDate = "Never",
            isCalibrated = false
        };
    }
}
