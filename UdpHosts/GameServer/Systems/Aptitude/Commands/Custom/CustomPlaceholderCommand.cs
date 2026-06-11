using System;
using System.Collections.Generic;

namespace GameServer.Aptitude;

public class CustomPlaceholderCommand : ICommand
{
    public string Label;
    private static readonly HashSet<uint> _loggedOnce = new();

    public CustomPlaceholderCommand(string label, uint id)
    {
        Label = label;
        Id = id;
    }

    public uint Id { get; set; }

    public bool Execute(Context context)
    {
        if (_loggedOnce.Add(Id))
        {
            Console.WriteLine($"[Ability] Unimplemented command {Label} (id={Id}) — ability chain may be broken");
        }

        return true;
    }

    public override string ToString()
    {
        return $"PLACEHOLDER ({Label}, ID {Id})";
    }
}
