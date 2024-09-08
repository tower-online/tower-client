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
    public static async Task<string?> RequestToken(string username)
    {
        //TODO: optional host and port
#if TOWER_PLATFORM_TEST
        var url = $"https://{Settings.RemoteHost}:8000/token/test";
        var requestData = new Dictionary<string, string>
        {
            ["username"] = username
        };
        GD.Print($"[{nameof(Auth)}] Requesting auth token: {url} with username={username}");
#elif TOWER_PLATFORM_STEAM
        var url = $"https://{Settings.RemoteHost}:8000/token/steam";
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
            var json = JsonDocument.Parse(body).RootElement;
            if (json.TryGetProperty("jwt", out var jwtElem))
            {
                var jwt = jwtElem.GetString();
                // GD.Print($"[{nameof(Connection)}] Requesting auth token succeed: {jwt}");
                return jwt;
            }

            GD.PrintErr($"Invalid response body");
            return null;
        }
        catch (HttpRequestException ex)
        {
            GD.PrintErr($"Error requesting token: {ex.Message}");
            return null;
        }
    }

    public static async Task<List<Tuple<string>>?> RequestCharacters(string username, string token)
    {
        var url = $"https://{Settings.RemoteHost}:8000/characters";
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

        using var handler = new HttpClientHandler();
        GD.Print("Warning: Allowing self-signed certification for auth server. Remove this in release");
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
            return default;
        }

        List<Tuple<string>> characters = [];
        try
        {
            if (!JsonDocument.Parse(body).RootElement.TryGetProperty("characters", out var charactersElem))
            {
                GD.PrintErr($"Invalid response body");
                return default;
            }

            foreach (var characterElem in charactersElem.EnumerateArray())
            {
                if (!characterElem.TryGetProperty("name", out var characterNameElem)) throw new Exception();
                var characterName = characterElem.GetString()!;

                characters.Add(new Tuple<string>(characterName));
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