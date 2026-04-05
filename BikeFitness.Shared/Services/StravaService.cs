using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BikeFitness.Shared.Models;

namespace BikeFitness.Shared.Services
{
    public class StravaService : IStravaService
    {
        private const string TokenUrl = "https://www.strava.com/oauth/token";
        private const string AuthUrl = "https://www.strava.com/oauth/authorize";
        private const string UploadUrl = "https://www.strava.com/api/v3/uploads";
        
        private readonly HttpClient _httpClient;
        private StravaToken? _currentToken;
        private readonly string _tokenStoragePath;

        public bool IsAuthorized => _currentToken != null && !string.IsNullOrEmpty(_currentToken.AccessToken);

        public StravaService()
        {
            _httpClient = new HttpClient();
            
            try 
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(appData, "BikeFitnessApp");
                Directory.CreateDirectory(appFolder);
                _tokenStoragePath = Path.Combine(appFolder, "strava_token.json");

                LoadToken();
            }
            catch (Exception ex)
            {
                Logger.Log($"StravaService initialization failed: {ex.Message}");
                _tokenStoragePath = ""; 
            }
            
            LoadSecrets();
        }

        private void LoadSecrets()
        {
            // Use environment variable exclusively for safety
            var secret = Environment.GetEnvironmentVariable("STRAVA_CLIENT_SECRET");
            if (!string.IsNullOrEmpty(secret))
            {
                AppSettings.StravaClientSecret = secret;
            }
        }

        private void LoadToken()
        {
            if (!string.IsNullOrEmpty(_tokenStoragePath) && File.Exists(_tokenStoragePath))
            {
                try
                {
                    var json = File.ReadAllText(_tokenStoragePath);
                    _currentToken = JsonSerializer.Deserialize<StravaToken>(json);
                }
                catch { _currentToken = null; }
            }
        }

        private void SaveToken(StravaToken token)
        {
            if (string.IsNullOrEmpty(_tokenStoragePath)) return;

            try
            {
                _currentToken = token;
                var json = JsonSerializer.Serialize(token);
                File.WriteAllText(_tokenStoragePath, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save Strava token: {ex.Message}");
            }
        }

        public void Deauthorize()
        {
            if (!string.IsNullOrEmpty(_tokenStoragePath) && File.Exists(_tokenStoragePath)) 
            {
                try { File.Delete(_tokenStoragePath); } catch { }
            }
            _currentToken = null;
        }

        public async Task<bool> AuthorizeAsync()
        {
            try {
            if (string.IsNullOrEmpty(AppSettings.StravaClientSecret))
            {
                throw new InvalidOperationException("Strava Client Secret is not configured. Please set the STRAVA_CLIENT_SECRET environment variable.");
            }

            var state = Guid.NewGuid().ToString();
            var scope = "activity:write,read";
            var redirectUrl = AppSettings.StravaAuthCallbackUrl;

            var authUri = $"{AuthUrl}?client_id={AppSettings.StravaClientId}&redirect_uri={WebUtility.UrlEncode(redirectUrl)}&response_type=code&scope={scope}&state={state}";

            // Start local listener to catch the code
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUrl);
            listener.Start();

            // Open browser
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(authUri) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", authUri);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", authUri);
            }

            // Wait for callback
            var context = await listener.GetContextAsync();
            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];

            // Send simple response to browser
            var response = context.Response;
            string responseString = "<html><body><h1>Authorization Successful!</h1><p>You can close this window and return to BikeFitnessApp.</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            listener.Stop();

            if (code == null || returnedState != state) return false;

            return await ExchangeCodeForTokenAsync(code);
            }
            catch (Exception ex)
            {
                Logger.Log($"Strava authorization failed: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            var values = new Dictionary<string, string>
            {
                { "client_id", AppSettings.StravaClientId },
                { "client_secret", AppSettings.StravaClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(TokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("access_token", out var accessElement))
                {
                    var token = new StravaToken
                    {
                        AccessToken = accessElement.GetString() ?? "",
                        RefreshToken = root.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() ?? "" : "",
                        ExpiresAt = root.TryGetProperty("expires_at", out var expiresElement) ? expiresElement.GetInt64() : 0
                    };

                    SaveToken(token);
                    return true;
                }
            }

            return false;
        }

        private async Task<string?> GetValidAccessTokenAsync()
        {
            if (_currentToken == null) return null;

            // Check if expired (or expiring in next 5 mins)
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_currentToken.ExpiresAt < now + 300)
            {
                // Refresh token
                var success = await RefreshTokenAsync();
                if (!success) return null;
            }

            return _currentToken.AccessToken;
        }

        private async Task<bool> RefreshTokenAsync()
        {
            if (_currentToken == null || string.IsNullOrEmpty(_currentToken.RefreshToken)) return false;

            var values = new Dictionary<string, string>
            {
                { "client_id", AppSettings.StravaClientId },
                { "client_secret", AppSettings.StravaClientSecret },
                { "refresh_token", _currentToken.RefreshToken },
                { "grant_type", "refresh_token" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(TokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("access_token", out var accessElement))
                {
                    _currentToken.AccessToken = accessElement.GetString() ?? "";
                    _currentToken.RefreshToken = root.TryGetProperty("refresh_token", out var refreshElement) ? refreshElement.GetString() ?? "" : _currentToken.RefreshToken;
                    _currentToken.ExpiresAt = root.TryGetProperty("expires_at", out var expiresElement) ? expiresElement.GetInt64() : _currentToken.ExpiresAt;

                    SaveToken(_currentToken);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> UploadActivityAsync(string filePath, string activityName)
        {
            try
            {
                var accessToken = await GetValidAccessTokenAsync();
                if (accessToken == null) return false;

                using var content = new MultipartFormDataContent();
                
                using var fileStream = File.OpenRead(filePath);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                
                content.Add(fileContent, "file", Path.GetFileName(filePath));
                content.Add(new StringContent("fit"), "data_type");
                content.Add(new StringContent(activityName), "name");
                content.Add(new StringContent("Indoor Workout via BikeFitnessApp"), "description");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _httpClient.PostAsync(UploadUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Log($"Strava upload failed: {ex.Message}");
                return false;
            }
        }
    }
}
