using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

public static class LocalStorage
{
    static string settingsDirectory = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TorbanDev");
    static string settingsFileName = Path.Combine(settingsDirectory, "SpotifySettings.txt");
    static string configDirectory = Directory.GetCurrentDirectory();
    static string configFileName = Path.Combine(configDirectory, "SpotifyAppSettings.config");
    static string spotifyAppPath = Path.Combine(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify"), "Spotify.exe");

    public static void StartSpotifyApp()
    {
        Process.Start(spotifyAppPath);
    }

    static public LocalData GetTokenData(bool isConfig)
    {
        string directory = "";
        string fileName = "";
        if (isConfig)
        {
            directory = configDirectory;
            fileName = configFileName;
        }
        else
        {
            directory = settingsDirectory;
            fileName = settingsFileName;
        }
        if (!ValidateFilePath(directory, fileName)) return null;

        string text = File.ReadAllText(fileName);

        if (text == null || text == "")
        {
            return null;
        }
        LocalData ld = new LocalData();
        if (isConfig)
        {
            ld = JsonSerializer.Deserialize<ConfigData>(text);
        }
        else
        {
            ld = JsonSerializer.Deserialize<TokenData>(text);
        }

        return ld;
    }

    static bool ValidateFilePath(string directory, string filePath)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (!File.Exists(filePath))
        {
            using (FileStream fs = File.Create(filePath))
            {

            }
        }
        return true;
    }
    static public void SaveTokenDataPublic(string tokenText, bool isConfig)
    {
        string file = "";
        if (isConfig)
        {
            file = configFileName;
        }
        else
        {
            file = settingsFileName;
        }
        SaveTokenData(tokenText, file);
    }
    static public void SaveTokenData(string tokenText, string filepath)
    {
        using (TextWriter tr = new StreamWriter(filepath))
        {
            tr.Write(tokenText);
        }
    }

}

public class TokenData : LocalData
{
    public string access_token { get; set; }
    public DateTime expires_in { get; set; }
    public string refresh_token { get; set; }
}

public class ConfigData : LocalData
{
    public string client_id { get; set; }
    public string client_Secret { get; set; }
}

public class LocalData
{

}