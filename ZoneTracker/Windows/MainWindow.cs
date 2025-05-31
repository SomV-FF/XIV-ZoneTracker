using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;

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

    public unsafe bool HasNamePlate(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject)
    {
        return ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address)->NameString is not null;
    }

    public List<IGameObject> knownNPCs = new List<IGameObject>();

    public List<string> talkedNames = new List<string>();

    public List<string> excludedNames = new List<string> { "Delivery Moogle", "Junkmonger", "Mender" };

    private float curTimer = 2000;
    private float baseTimer = 2000;
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

    public void CleanTargets()
    {
        for (int i = knownNPCs.Count; i > 0; i--)
        {
            if (!knownNPCs[i].IsValid())
            {
                knownNPCs.RemoveAt(i);
                return;
            }
            if (talkedNames.Contains(knownNPCs[i].Name.ToString()))
            {
                knownNPCs.RemoveAt(i);
                return;
            }
            if (knownNPCs[i].Name == null)
            {
                knownNPCs.RemoveAt(i);
                return;
            }
           if (knownNPCs[i].Name.ToString() == "")
            {
                knownNPCs.RemoveAt(i);
            }
            //remove invisible characters
            if (!(knownNPCs[i] as ICharacter).IsCharacterVisible())
            {
                knownNPCs.RemoveAt(i);
            }
            //remove untargetable characters
            if (!knownNPCs[i].IsTargetable())
            {   
                knownNPCs.RemoveAt(i);
            }
        }
    }

    public void StopMoving()
    {
        Chat.Instance.SendMessage($"/vnav stop");
        
    }
    public override void Draw()
    {
        //        DialogueHandler.Tick();

        ImGui.TextUnformatted("Current Character Status: ");

        if(characterStatus == CharacterStatus.MOVING)
            ImGui.TextUnformatted($"MOVING");
        else if (characterStatus == CharacterStatus.CHATTING)
            ImGui.TextUnformatted($"CHATTING");

        ImGui.Spacing();
        ImGui.Spacing();

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
            if (ImGui.Button("skip wait"))
            {
                curTimer = 5;
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
//        ImGui.TextUnformatted($"The random config bool is {Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

        ImGui.BeginTable("Talky_Main_Table", 2, ImGuiTableFlags.Borders);
        ImGui.TableNextColumn();

//        if (ImGui.Button("Show Settings"))
//        {
//            Plugin.ToggleConfigUI();
//        }
//        ImGui.TableNextColumn();
        
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
        ImGui.TableNextColumn();

        if (ImGui.Button("Clear known NPCs"))
        {
            knownNPCs.Clear();
            talkedNames.Clear();
        }
        ImGui.TableNextColumn();

        if (ImGui.Button("Move to next character"))
        {
            MoveToPoint();
        }
        ImGui.TableNextColumn();

        if (ImGui.Button("Skip next NPC"))
        {
            talkedNames.Add(knownNPCs[0].Name.ToString());
            knownNPCs.Remove(knownNPCs[0]);
        }
        ImGui.EndTable();

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
        if(Player.Available)
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextUnformatted("Found characters:");
        using (var child = ImRaii.Child("CharacterList", new Vector2(500, 300), true))
        {
            foreach(IGameObject x in obj)
            {
                if (!knownNPCs.Contains(x) && !talkedNames.Contains(x.Name.ToString()))
                {
                    bool excludedName = false;
                    bool addName = true;
                    if(x.Name != null)
                    {
                        if (!x.IsTargetable)
                            continue;
                        if (IsPartOfQuestOrImportant(x))
                            continue;
                        if (x.Name.ToString() == null || x.Name.ToString() == "")
                            continue;
                        if(excludedNames.Contains(x.Name.ToString()))
                            continue;
                        if (!(x as ICharacter).IsCharacterVisible())
                            continue;

                        knownNPCs.Add(x);
                    }
                }
            }
            foreach (IGameObject x in knownNPCs)
            {
                ImGui.TextUnformatted(x.Name.ToString() + " " + GetDistanceToPlayer(x).ToString("#") + " yalms away");
            }
        }


        ImGui.Spacing();


        CleanTargets();
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
