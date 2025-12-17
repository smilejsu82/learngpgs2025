using Newtonsoft.Json;

public static class DeepCopyUtility
{
    public static T DeepCopy<T>(T source)
    {
        if (source == null)
            return default;

        string json = JsonConvert.SerializeObject(source);
        return JsonConvert.DeserializeObject<T>(json);
    }
}