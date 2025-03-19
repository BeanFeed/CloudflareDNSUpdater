using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CFDNSUpdater;

class CloudflareApiClient
{
    private readonly HttpClient _client = new HttpClient();
    private readonly string _authEmail;
    private readonly string _authKey;

    public CloudflareApiClient(string authEmail, string authKey)
    {
        _authEmail = authEmail;
        _authKey = authKey;
        
        _client.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
        _client.DefaultRequestHeaders.Add("X-Auth-Email", _authEmail);
        _client.DefaultRequestHeaders.Add("X-Auth-Key", _authKey);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> UpdateARecord(string zoneId, string recordId, string name, string newIpAddress)
    {
        var updateData = new
        {
            content = newIpAddress,
            name = name,
            type = "A",
            ttl = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateData),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PutAsync($"zones/{zoneId}/dns_records/{recordId}", content);
        
        return await response.Content.ReadAsStringAsync();
    }

    // Optional: Method to get record ID if you only know the domain name
    public async Task<string?> GetDnsRecordId(string zoneId, string name, string type = "A")
    {
        // You can use query parameters to filter directly in the API call
        var response = await _client.GetAsync($"zones/{zoneId}/dns_records?type={type}&name={name}");
        var jsonResponse = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        if (root.GetProperty("success").GetBoolean() && root.GetProperty("result_info").GetProperty("count").GetInt32() > 0)
        {
            return root.GetProperty("result")[0].GetProperty("id").GetString();
        }
    
        // If the filtered approach doesn't work, try getting all records and filter manually
        response = await _client.GetAsync($"zones/{zoneId}/dns_records");
        jsonResponse = await response.Content.ReadAsStringAsync();
    
        using JsonDocument allDoc = JsonDocument.Parse(jsonResponse);
        var allRoot = allDoc.RootElement;
    
        if (allRoot.GetProperty("success").GetBoolean())
        {
            var results = allRoot.GetProperty("result");
            foreach (var record in results.EnumerateArray())
            {
                if (record.TryGetProperty("name", out var recordName) && 
                    record.TryGetProperty("type", out var recordType) &&
                    recordName.GetString() == name &&
                    recordType.GetString() == type)
                {
                    return record.GetProperty("id").GetString();
                }
            }
        }

        return null;
    }
}

class Program
{
    static async Task Main()
    {
        var configPath = Path.Join(Environment.CurrentDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            await File.Create(configPath).DisposeAsync();
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath)
            .AddEnvironmentVariables()
            .Build();
        var cfConfig = config.GetSection("Cloudflare").Get<CloudflareConfig>();
        var authEmail = cfConfig.AuthEmail;
        var authKey = cfConfig.AuthKey;
        var zones = cfConfig.Zones;

        var cfClient = new CloudflareApiClient(authEmail, authKey);
        var httpClient = new HttpClient();
        while (true)
        {
            //get the current public ip address
            
            var ipResponse = await httpClient.GetStringAsync("https://api.ipify.org");
            string currentIpAddress = ipResponse.Trim();

            foreach (var zone in zones)
            {
                //get the dns record id for the domain
                string? recordId = await cfClient.GetDnsRecordId(zone.ZoneId, zone.RecordName);
            
                if (recordId == null)
                {
                    Console.WriteLine($"No DNS record found for {zone.RecordName}");
                    continue;
                }

                //update the A record with the current IP address
                var updateResult = await cfClient.UpdateARecord(zone.ZoneId, recordId, zone.RecordName, currentIpAddress);
                Console.WriteLine($"Updated DNS record: {updateResult}");   
            }
            //wait for a specified interval before checking again
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }
}

class CloudflareConfig
{
    public string AuthEmail { get; set; }
    public string AuthKey { get; set; }
    public Zone[] Zones { get; set; }
}

class Zone
{
    public string ZoneId { get; set; }
    public string RecordName { get; set; }
}