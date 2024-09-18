using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HttpClient = System.Net.Http.HttpClient;
using Tower.System;

namespace Tower.Network;

public static class Auth
{
    public static async Task<string> RequestToken(string username)
    {
#if TOWER_PLATFORM_TEST
        var url = $"https://{Settings.RemoteHost}:{Settings.RemoteAuthPort}/token/test";
        var requestData = new Dictionary<string, string>
        {
            ["username"] = username
        };
        GD.Print($"[{nameof(Auth)}] Requesting auth token: {url} with username={username}");
#elif TOWER_PLATFORM_STEAM
        var url = $"https://{Settings.RemoteHost}:{Settings.RemoteAuthPort}/token/steam";
        var requestData = new Dictionary<string, string>
        {
            ["username"] = username,
            //TODO: Add ticket and required keys
        };
        GD.Print($"[{nameof(Connection)}] Requesting auth token: {url} with username={username}");
#endif


        using var handler = new HttpClientHandler();
        GD.Print("Warning: Allowing self-signed certification for auth server. Remove this in release");
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(body).RootElement;
            
            var jwtElem = root.GetProperty("jwt");
            return jwtElem.GetString();
        }
        catch (HttpRequestException ex)
        {
            GD.PrintErr($"Error requesting token: {ex.Message}");
            return null;
        }
        catch (Exception)
        {
            GD.PrintErr("Invalid JSON");
            return null;
        }
    }

    public static async Task<bool> RequestCharacterCreate(string username, string token, string characterName, string race)
    {
#if TOWER_PLATFORM_TEST
        var url = $"https://{Settings.RemoteHost}:{Settings.RemoteAuthPort}/character/create/test";
        var requestData = new Dictionary<string, string>
        {
            ["platform"] = "TEST",
            ["username"] = username,
            ["jwt"] = token,
            ["character_name"] = characterName,
            ["race"] = race
        };
#elif TOWER_PLATFORM_STEAM
        //TODO
#endif
        
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            GD.PrintErr($"Error requesting characters: {ex.Message}");
            return false;
        }

        return true;
    }

    public static async Task<List<string>> RequestCharacters(string username, string token)
    {
        var url = $"https://{Settings.RemoteHost}:{Settings.RemoteAuthPort}/characters";
#if TOWER_PLATFORM_TEST
        var requestData = new Dictionary<string, string>
        {
            ["platform"] = "TEST",
            ["username"] = username,
            ["jwt"] = token
        };
#elif TOWER_PLATFORM_STEAM
        var requestData = new Dictionary<string, string>
        {
            ["platform"] = "STEAM"
            ["username"] = username,
            ["jwt"] = token
        };
#endif
        GD.Print($"[{nameof(Auth)}] Requesting characters: {url} with username={username} token={token}");

        GD.Print("Warning: Allowing self-signed certification for auth server. Remove this in release");
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var client = new HttpClient(handler);
        string body;
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            body = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            GD.PrintErr($"Error requesting characters: {ex.Message}");
            return null;
        }

        List<string> characters = [];
        try
        {
            if (!JsonDocument.Parse(body).RootElement.TryGetProperty("characters", out var charactersElem))
            {
                GD.PrintErr($"Invalid response body");
                return default;
            }

            foreach (var characterElem in charactersElem.EnumerateArray())
            {
                characters.Add(characterElem.GetProperty("name").GetString()!);
            }
        }
        catch (Exception)
        {
            GD.PrintErr($"Invalid JSON");
            return default;
        }

        GD.Print($"[{nameof(Auth)}] Requesting characters succeed");
        return characters;
    }
}