using Godot;
using System;
using System.Text.Json.Serialization;

public class RoomData
{
    [JsonPropertyName("name")]
    public string name { get; set; }

    [JsonPropertyName("connections")]
    public string[] connections { get; set; }

    [JsonPropertyName("layout")]
    public int[][] layout { get; set; }
}
