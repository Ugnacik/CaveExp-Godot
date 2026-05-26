using Godot;
using System;
using System.Linq;
using System.Text.Json.Serialization;

public enum Direction
{
    Left,
    Right,
    Top,
    Bottom
}
public class RoomData
{
    
    [JsonPropertyName("name")]
    public string name { get; set; }

    [JsonPropertyName("connections")]
    public string[] connectionsRaw { get; set; }

    [JsonPropertyName("layout")]
    public int[][] layout { get; set; }

    // Parsed enum version (not serialized)
    [JsonIgnore]
    public Direction[] connections
    {
        get
        {
            if (connectionsRaw == null)
                return Array.Empty<Direction>();

            return connectionsRaw
                .Select(c => Enum.Parse<Direction>(c, true)) // case-insensitive
                .ToArray();
        }
    }
}
