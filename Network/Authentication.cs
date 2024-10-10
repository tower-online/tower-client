using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tower.Network;

public static class Authentication
{
    public enum Platform
    {
        Test,
        Steam
    }

    public static async Task<string?> RequestAuthTokenAsync(string host, ushort port,
        string username, Platform platform, ILogger logger)
    {
        logger.LogInformation("Requesting auth token...");
        
        var url = platform switch
        {
            Platform.Test => $"https://{host}:{port}/token/test",
            Platform.Steam => $"https://{host}:{port}/token/steam",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        var data = platform switch
        {
            Platform.Test => new Dictionary<string, string>
            {
                ["username"] = username
            },
            Platform.Steam => new Dictionary<string, string>
            {
                ["username"] = username,
                //TODO
            },
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        using var client = new HttpClient(handler);

        string body;
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            logger.LogError("Error HTTPS post");
            return null;
        }

        string? token;
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            
            var jwtElem = root.GetProperty("jwt");
            token = jwtElem.GetString();
        }
        catch (Exception)
        {
            logger.LogError("Invalid JSON");
            return null;
        }

        return token;
    }

    public static async Task<List<string>?> RequestCharactersAsync(string host, ushort port, 
        string username, string token, Platform platform, ILogger logger)
    {
        logger.LogInformation("Requesting characters list...");

        var url = $"https://{host}:{port}/characters";
        
        var data = platform switch
        {
            Platform.Test => new Dictionary<string, string>
            {
                ["platform"] = "TEST",
                ["username"] = username,
                ["jwt"] = token
            },
            Platform.Steam => new Dictionary<string, string>
            {
                ["platform"] = "STEAM",
                ["username"] = username,
                ["jwt"] = token
                //TODO
            },
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
        
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        using var client = new HttpClient(handler);

        string body;
        try
        {
            HttpResponseMessage response = await client.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"
            ));
            response.EnsureSuccessStatusCode();

            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            logger.LogError("Error HTTPS post");
            return null;
        }
        
        List<string> characters = [];
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            var charactersElem = root.GetProperty("characters");

            foreach (var characterElem in charactersElem.EnumerateArray())
            {
                characters.Add(characterElem.GetProperty("name").GetString()!);
            }
        }
        catch (Exception)
        {
            logger.LogError("Invalid JSON");
            return null;
        }

        return characters;
    }
}