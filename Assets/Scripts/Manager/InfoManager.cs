using System.IO;
using Newtonsoft.Json;
using UnityEngine;


public class InfoManager 
{
    public readonly static InfoManager Instance = new InfoManager();
    private const string GAME_INFO_FILE_NAME = "game_info.json";
    private string gameInfoPath;
    
    public GameInfo GameInfo { get; set; }
    
    private InfoManager()
    {
        gameInfoPath = Path.Combine(Application.persistentDataPath, GAME_INFO_FILE_NAME);
        Debug.Log(gameInfoPath);
    }

    /// <summary>
    /// 최초 1회 생성할때만 호출해야함 
    /// </summary>
    public void CreateGameInfo()
    {
        GameInfo = new GameInfo(1);
        GameInfo.scoreInfo = new ScoreInfo(0);
        GameInfo.stageInfo = new StageInfo(1);  //1번 스테이지부터 
        GameInfo.characterInfo = new CharacterInfo();
        Debug.Log("GameInfo가 생성되었습니다.");
        this.Save();
    }

    public void Save()
    {
        string gameInfoJson = JsonConvert.SerializeObject(this.GameInfo);
        Debug.Log(gameInfoJson);
        File.WriteAllText(gameInfoPath,gameInfoJson);
        Debug.Log("gameInfo가 저장되었습니다.");
    }

    public void Load()
    {
        string gameInfoJson = File.ReadAllText(gameInfoPath);
        Debug.Log(gameInfoJson);
        this.GameInfo = JsonConvert.DeserializeObject<GameInfo>(gameInfoJson);

        if (GameInfo.scoreInfo == null)
            GameInfo.scoreInfo = new ScoreInfo(0);

        if (GameInfo.stageInfo == null)
            GameInfo.stageInfo = new StageInfo(1);

        if (GameInfo.characterInfo == null)
            GameInfo.characterInfo = new CharacterInfo();

        Debug.Log($"GameInfo를 불러왔습니다.: {GameInfo.version}, {GameInfo.scoreInfo.highScore}");
    }



    public bool IsNewbie()
    {
        if (File.Exists(gameInfoPath))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public void UpdateHighScore(int score)
    {
        var oldScore = this.GameInfo.scoreInfo.highScore;
        
        GameInfo.scoreInfo.highScore = score;
        Debug.Log($"최고 점수가 업데이트 {oldScore} -> {score} 되었습니다.");
    }
    
    public void SaveCharacterInfoFromHero(Hero hero)
    {
        if (GameInfo == null)
            return;

        if (GameInfo.characterInfo == null)
            GameInfo.characterInfo = new CharacterInfo();

        var ci = GameInfo.characterInfo;

        ci.level        = hero.level;
        ci.currentXP    = hero.currentXP;
        ci.xpToNextLevel= hero.xpToNextLevel;

        ci.attackPower  = hero.attackPower;
        ci.speed        = hero.speed;
        ci.attackDelay  = hero.attackDelay;

        Save();
    }

}
