public class GameInfo
{
    public int version;
    public ScoreInfo scoreInfo;
    public StageInfo stageInfo;
    public CharacterInfo characterInfo;
    public int gold;

    public long savedAtTicks;

    public GameInfo(int version)
    {
        this.version = version;
        savedAtTicks = System.DateTime.UtcNow.Ticks;
    }

    public GameInfo DeepCopy()
    {
        GameInfo copy = new GameInfo(this.version);
        copy.savedAtTicks = this.savedAtTicks;

        // ScoreInfo
        if (this.scoreInfo != null)
            copy.scoreInfo = new ScoreInfo(this.scoreInfo.highScore);

        // StageInfo
        if (this.stageInfo != null)
            copy.stageInfo = new StageInfo(this.stageInfo.currentStage);

        // CharacterInfo
        if (this.characterInfo != null)
        {
            copy.characterInfo = new CharacterInfo()
            {
                level = this.characterInfo.level,
                currentXP = this.characterInfo.currentXP,
                xpToNextLevel = this.characterInfo.xpToNextLevel,
                attackPower = this.characterInfo.attackPower,
                speed = this.characterInfo.speed,
                attackDelay = this.characterInfo.attackDelay
            };
        }

        return copy;
    }
}