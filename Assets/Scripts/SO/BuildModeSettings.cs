using UnityEngine;

public enum BuildMode
{
    Debug,
    Development,
    Release
}

[CreateAssetMenu(fileName = "BuildModeSettings", menuName = "Game/Build Mode Settings")]
public class BuildModeSettings : ScriptableObject
{
    public BuildMode currentMode = BuildMode.Debug;

    private static BuildModeSettings _instance;

    public static BuildModeSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                // Resources 폴더에서 자동 로드
                _instance = Resources.Load<BuildModeSettings>("BuildModeSettings");
            }
            return _instance;
        }
    }
}