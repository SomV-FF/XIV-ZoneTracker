using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using ZoneTracker;
using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;



using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ECommons.GameHelpers;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.Automation;
using System.Xml.Linq;
using ECommons.UIHelpers;

using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ZoneTracker.ZoneTracker;

namespace ZoneTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private string GoatImagePath;
    private Plugin Plugin;

    internal static unsafe float GetDistanceToPlayer(IGameObject gameObject) => GetDistanceToPlayer(gameObject.Position);

    internal static unsafe float GetDistanceToPlayer(Vector3 v3) => Vector3.Distance(v3, Player.GameObject->Position);
    internal static Vector2 ConvertWorldXZToMap(Vector2 coords, Lumina.Excel.Sheets.Map map) => Dalamud.Utility.MapUtil.WorldToMap(coords, map.OffsetX, map.OffsetY, map.SizeFactor);
    internal static List<IGameObject>? GetObjectsByObjectKind(ObjectKind objectKind) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.ObjectKind == objectKind)];

    public enum CharacterStatus
    {
        MOVING,
        CHATTING,
    }

    public static bool awaitingChatFinish = false;

    public CharacterStatus characterStatus { get; set; }

    public unsafe bool IsPartOfQuestOrImportant(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject)
    {
        return ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address)->NamePlateIconId is not 0;
    }

    public List<IGameObject> knownNPCs = new List<IGameObject>();
    public List<string> talkedNames = new List<string>();


    private float curTimer = 500;
    private float baseTimer = 500;
    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("My Amazing Window##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        GoatImagePath = goatImagePath;
        Plugin = plugin;
    }

    public void Dispose() { }


    internal static unsafe void InteractWithObject(IGameObject? gameObject, bool face = true)
    {
        try
        {
            if (gameObject == null || !gameObject.IsTargetable)
                return;
//            if (face)
//                Plugin.OverrideCamera.Face(gameObject.Position);
            var gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            TargetSystem.Instance()->InteractWithObject(gameObjectPointer, false);
        }
        catch (Exception ex)
        {
            Svc.Log.Info($"InteractWithObject: Exception: {ex}");
        }
    }

    public void MoveToPoint()
    {
        if (knownNPCs.Count > 0)
        {
            Chat.Instance.SendMessage($"/vnav moveto {knownNPCs[0].Position.X} {knownNPCs[0].Position.Y} {knownNPCs[0].Position.Z}");
            characterStatus = CharacterStatus.MOVING;
        }
    }

    public void StopMoving()
    {
        Chat.Instance.SendMessage($"/vnav stop");
        
    }
    public override void Draw()
    {
        //        DialogueHandler.Tick();

        if(characterStatus == CharacterStatus.MOVING)
            ImGui.TextUnformatted($"MOVING");
        else if (characterStatus == CharacterStatus.CHATTING)
            ImGui.TextUnformatted($"CHATTING");

        if (awaitingChatFinish)
        {
            ImGui.TextUnformatted($"Awaiting Chat Finish");
            ImGui.TextUnformatted($"Remaining frames: {curTimer}");
            curTimer -= 1;
            if(curTimer < 0 )
            {
                curTimer = baseTimer;
                awaitingChatFinish = false;
            }
            return;
        }

        //update known character list
        List<IGameObject> obj = GetObjectsByObjectKind(ObjectKind.EventNpc);
        if (obj == null) return;

        
        // Do not use .Text() or any other formatted function like TextWrapped(), or SetTooltip().
        // These expect formatting parameter if any part of the text contains a "%", which we can't
        // provide through our bindings, leading to a Crash to Desktop.
        // Replacements can be found in the ImGuiHelpers Class
        ImGui.TextUnformatted($"The random config bool is {Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");
        
        if (ImGui.Button("Show Settings"))
        {
            Plugin.ToggleConfigUI();
        }

        if(ImGui.Button("Interact with nearest character"))
        {
            foreach (IGameObject x in obj)
            {
                if (x.IsTargetable)
                {
                    InteractWithObject(x, false);
                    break;
                }
            }
        }

        if (ImGui.Button("Clear known NPCs"))
        {
            knownNPCs.Clear();
            talkedNames.Clear();
        }

        if (ImGui.Button("Move to point"))
        {
            MoveToPoint();
        }

        if(knownNPCs.Count > 0)
        {
            if (Vector3.Distance(Player.Position, knownNPCs[0].Position) < 3 && characterStatus == CharacterStatus.MOVING)
            {
                characterStatus = CharacterStatus.CHATTING;
                StopMoving();
                InteractWithObject(knownNPCs[0], false);
                awaitingChatFinish = true;
                talkedNames.Add(knownNPCs[0].Name.ToString());
                knownNPCs.Remove(knownNPCs[0]);
            }
        }

        if(characterStatus == CharacterStatus.CHATTING && !awaitingChatFinish)
        {
            knownNPCs = knownNPCs.OrderBy(GetDistanceToPlayer).ToList();
            MoveToPoint();

        }

        ImGui.Spacing();
        

        using (var child = ImRaii.Child("CharacterList", new Vector2(500, 300), true))
        {
            foreach(IGameObject x in obj)
            {
                if (!knownNPCs.Contains(x) && !talkedNames.Contains(x.Name.ToString()))
                {
                    if (x.IsTargetable && !IsPartOfQuestOrImportant(x))
                        knownNPCs.Add(x);
                }
            }
            foreach (IGameObject x in knownNPCs)
            {
                ImGui.TextUnformatted(x.Name.ToString() + " " + GetDistanceToPlayer(x).ToString("#") + " yalms away");
            }
        }


        ImGui.Spacing();

        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
        using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
        {
            // Check if this child is drawing
            if (child.Success)
            {
                // Example for other services that Dalamud provides.
                // ClientState provides a wrapper filled with information about the local player object and client.
                var localPlayer = Plugin.ClientState.LocalPlayer;
                if (localPlayer == null)
                {
                    ImGui.TextUnformatted("Our local player is currently not loaded.");
                    return;
                }

                if (!localPlayer.ClassJob.IsValid)
                {
                    ImGui.TextUnformatted("Our current job is currently not valid.");
                    return;
                }

                // ExtractText() should be the preferred method to read Lumina SeStrings,
                // as ToString does not provide the actual text values, instead gives an encoded macro string.
                ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation.ExtractText()}\"");
                ImGui.TextUnformatted($"Our character name is John Final_Fantasy");//{localPlayer.Name}
                var territoryId = Plugin.ClientState.TerritoryType;
                if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
                {
                    var map = territoryRow.Map.Value;
                    Vector2 pos = new Vector2(localPlayer.Position.X, localPlayer.Position.Z);
                    ImGui.TextUnformatted($"Our character position is {ConvertWorldXZToMap(pos, map)}");
                    ImGui.TextUnformatted($"We are currently in ({territoryRow.PlaceName.Value.Name.ExtractText()})");
                }
                // Example for quarrying Lumina directly, getting the name of our current area.
                else
                {
                    ImGui.TextUnformatted("Invalid territory.");
                }
//                FFXIVClientStructs.FFXIV.Client.Game.GameMain.
                
                //character objects
                

            }
        }
    }
}
