﻿using Dalamud.Logging;
using TrackyTrack.Data;
using Lumina.Excel.GeneratedSheets;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private Item[] DesynthCache = null!;

    private int SelectedCharacter;
    private int SelectedHistory;

    private uint SourceSearchResult;
    private uint RewardSearchResult;
    private int ILvLSearchResult = 1;

    private int HighestILvL = 1;
    private int SelectedJob = 0;
    private bool ExcludeGear = true;
    private bool ExcludeNonMB = true;

    private Item[] SearchCache = null!;

    private static readonly string[] Jobs = { "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };

    public void InitializeDesynth()
    {
        DesynthCache = ItemSheet.Where(i => i.Desynth > 0).ToArray();
        HighestILvL = DesynthCache.Select(i => (int)i.LevelItem.Row).Max();

        // Fill once
        SearchCache = DesynthCache.Where(i => i.Desynth > 0)
                                  .Where(i => i.ItemUICategory.Row != 39)
                                  .Where(i => i.LevelItem.Row > ILvLSearchResult)
                                  .Where(i => i.ClassJobRepair.Row == SelectedJob + 8)
                                  .Where(i => !ExcludeGear || i.EquipSlotCategory.Row == 0)
                                  .Where(i => !ExcludeNonMB || !i.IsUntradable)
                                  .OrderBy(i => i.LevelItem.Row)
                                  .ToArray();
    }

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> SourceOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => Utils.ToStr(i.Name) != "").Where(i => i.Desynth > 0)
    };

    private static readonly ExcelSheetSelector.ExcelSheetPopupOptions<Item> ItemOptions = new()
    {
        FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {Utils.ToStr(a.Name)}" },
        FilteredSheet = Plugin.Data.GetExcelSheet<Item>()!.Skip(1).Where(i => Utils.ToStr(i.Name) != "")
    };

    private void DesynthesisTab()
    {
        if (ImGui.BeginTabItem("Desynthesis"))
        {
            // Sort out any character with 0 desynthesis
            var characters = Plugin.CharacterStorage.Values.Where(c => c.Storage.History.Count > 0).ToArray();
            if (!characters.Any())
            {
                Helper.NoDesynthesisData();

                ImGui.EndTabItem();
                return;
            }

            if (ImGui.BeginTabBar("##DesynthTabBar"))
            {
                Stats(characters);

                History(characters);

                Rewards(characters);

                Source(characters);

                Search(characters);

                Find();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void Stats(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Stats"))
            return;

        var totalNumber = characters.Sum(c => c.Storage.History.Count);
        var dict = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.Total))
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                dict[pair.Key] += pair.Value;
        }

        var numberOfDesynthesis = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!numberOfDesynthesis.TryAdd(pair.Value.Source, 1))
                numberOfDesynthesis[pair.Value.Source] += 1;
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        if (ImGui.BeginTable($"##TotalStatsTable", 3))
        {
            ImGui.TableSetupColumn("##stat", 0, 0.8f);
            ImGui.TableSetupColumn("##name");
            ImGui.TableSetupColumn("##amount");

            ImGui.TableNextColumn();
            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Desynthesized");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalNumber:N0} time{(totalNumber > 1 ? "s" : "")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var avg = characters.Sum(c => c.Storage.History.Values.Sum(result => result.Received.Length)) / (double) totalNumber;
            ImGui.TextColored(ImGuiColors.HealerGreen, "Avg");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{avg:F2} Reward{(avg > 1 ? "s" : "")}");
            ImGui.Unindent(10.0f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Most often:");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var destroyed = numberOfDesynthesis.MaxBy(pair => pair.Value);
            var item = ItemSheet.GetRow(destroyed.Key)!;

            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Destroyed");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{destroyed.Value:N0}");

            var bestItem = dict.Where(pair => pair.Key is > 20 and < 1000000).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestItem.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Rewarded");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestItem.Value:N0}");

            var bestCrystal = dict.Where(pair => pair.Key is > 0 and < 20).MaxBy(pair => pair.Value);
            item = ItemSheet.GetRow(bestCrystal.Key)!;
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Crystal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Utils.ToStr(item.Name)}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{bestCrystal.Value:N0}");
            ImGui.Unindent(10.0f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Gil:");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var sum = 0UL;
            foreach (var pair in dict.Where(pair => Desynth.GilItems.ContainsKey(pair.Key)))
                sum += Desynth.GilItems[pair.Key] * pair.Value;

            ImGui.Indent(10.0f);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Pure");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sum:N0}");
            ImGui.Unindent(10.0f);

            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void History(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("History"))
            return;

        var existingCharacters = characters.Select(character => $"{character.CharacterName}@{character.World}").ToArray();

        var selectedCharacter = SelectedCharacter;
        ImGui.Combo("##existingCharacters", ref selectedCharacter, existingCharacters, existingCharacters.Length);
        if (selectedCharacter != SelectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            SelectedHistory = 0;
        }

        var selectedChar = characters[SelectedCharacter];
        var history = selectedChar.Storage.History.Reverse().Select(pair => $"{pair.Key}").ToArray();

        ImGui.Combo("##voyageSelection", ref SelectedHistory, history, history.Length);
        Helper.DrawArrows(ref SelectedHistory, history.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var resultPair = selectedChar.Storage.History.Reverse().ToArray()[SelectedHistory];

        var source = ItemSheet.GetRow(resultPair.Value.Source)!;
        DrawIcon(source.Icon);
        ImGui.SameLine();

        var sourceName = Utils.ToStr(source.Name);
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
        if (ImGui.Selectable(sourceName))
        {
            ImGui.SetClipboardText(sourceName);
            SourceSearchResult = source.RowId;
            RewardSearchResult = 0;
        }
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{sourceName}\nClick to copy and set as reward for search");

        if (ImGui.BeginTable($"##HistoryTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);

            ImGui.Indent(10.0f);
            foreach (var result in resultPair.Value.Received.Where(i => i.Item > 0))
            {
                var item = ItemSheet.GetRow(result.Item)!;

                ImGui.TableNextColumn();
                DrawIcon(item.Icon);
                ImGui.TableNextColumn();

                var name = Utils.ToStr(item.Name);
                if (ImGui.Selectable(name))
                {
                    ImGui.SetClipboardText(name);
                    SourceSearchResult = 0;
                    RewardSearchResult = item.RowId;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as reward for search");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{result.Count}");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void Rewards(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Rewards"))
            return;

        var dict = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.Total))
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                dict[pair.Key] += pair.Value;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginChild("RewardsTableChild"))
        {
            if (ImGui.BeginTable($"##RewardsTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (itemId, count) in dict.Where(pair => pair.Key is > 0 and < 1000000).OrderBy(pair => pair.Key))
                {
                    var item = ItemSheet.GetRow(itemId)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                    {
                        ImGui.SetClipboardText(name);
                        RewardSearchResult = item.RowId;
                        SourceSearchResult = 0;
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as reward for search");

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{count}");
                    ImGui.TableNextRow();
                }

                ImGui.Unindent(10.0f);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    private void Source(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Sources"))
            return;

        var numberOfDesynthesis = new Dictionary<uint, uint>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!numberOfDesynthesis.TryAdd(pair.Value.Source, 1))
                numberOfDesynthesis[pair.Value.Source] += 1;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginChild("SourceTableChild"))
        {
            if (ImGui.BeginTable($"##DesynthesisSourceTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var (source, count) in numberOfDesynthesis.OrderByDescending(pair => pair.Value))
                {
                    var item = ItemSheet.GetRow(source)!;

                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                    {
                        ImGui.SetClipboardText(name);
                        SourceSearchResult = item.RowId;
                        RewardSearchResult = 0;
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{Utils.ToStr(item.Name)}\nClick to copy and set as source for search");

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"x{count:N0}");
                    ImGui.TableNextRow();
                }

                ImGui.Unindent(10.0f);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        ImGui.EndTabItem();
    }

    private void Search(IEnumerable<CharacterConfiguration> characters)
    {
        if (!ImGui.BeginTabItem("Search"))
            return;

        ImGui.Columns(2);

        var buttonWidth = ImGui.GetContentRegionAvail().X - (20.0f * ImGuiHelpers.GlobalScale);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search through your sources");
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}##sources", new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("SourceResultPopup", out var sourceRow, SourceOptions))
        {
            SourceSearchResult = sourceRow;
            RewardSearchResult = 0;
        }

        ImGui.NextColumn();

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search through your rewards");
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}##item", new Vector2(buttonWidth, 0));
        ImGui.PopFont();

        if (ExcelSheetSelector.ExcelSheetPopup("ItemResultPopup", out var itemRow, ItemOptions))
        {
            SourceSearchResult = 0;
            RewardSearchResult = itemRow;
        }

        ImGui.Columns(1);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (RewardSearchResult > 0)
            ItemSearch(characters);
        else if (SourceSearchResult > 0)
            SourceSearch(characters);

        ImGui.EndTabItem();
    }

    private void SourceSearch(IEnumerable<CharacterConfiguration> characters)
    {
        var historyDict = new Dictionary<uint, List<DesynthResult>>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!historyDict.TryAdd(pair.Value.Source, new List<DesynthResult> {pair.Value}))
                historyDict[pair.Value.Source].Add(pair.Value);
        }

        var sourceItem = ItemSheet.GetRow(SourceSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!historyDict.TryGetValue(SourceSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this source item ...");
            return;
        }

        var statDict = new Dictionary<uint, (uint Min, uint Max, uint Received)>();
        foreach (var result in history.SelectMany(h => h.Received).Where(h => h.Item > 0))
        {
            if (!statDict.TryAdd(result.Item, (result.Count, result.Count, result.Count)))
            {
                var stat = statDict[result.Item];
                stat.Received += result.Count;
                if (stat.Min > result.Count)
                    stat.Min = result.Count;

                if (stat.Max < result.Count)
                    stat.Max = result.Count;

                statDict[result.Item] = stat;
            }
        }

        var desynthesized = history.Count;
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Desynthesized {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 5, 0, new Vector2(400 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("Reward##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("Min##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("Max##statMax", 0, 0.1f);
            ImGui.TableSetupColumn("Received##received", 0, 0.3f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var statPair in SortByKeyCustom(statDict))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(statPair.Key)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Max}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{statPair.Value.Received:N0}");

                ImGui.TableNextRow();
            }
            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        var percentageDict = new Dictionary<uint, uint>();
        foreach (var result in history.Select(c => c.Received.First()).Where(h => h.Item > 0))
            if (!percentageDict.TryAdd(result.Item, 1))
                percentageDict[result.Item] += 1;

        var sortedList = percentageDict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (double) count / desynthesized * 100.0;
            return new Utils.SortedEntry(item.Icon, Utils.ToStr(item.Name), count, percentage);
        }).OrderByDescending(x => x.Percentage);

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Percentages:");

        if (ImGui.BeginTable($"##PercentageSourceTable", 3))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Pct##percentage", 0, 0.25f);

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in sortedList)
            {
                ImGui.TableNextColumn();
                DrawIcon(sortedEntry.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");

                ImGui.TableNextRow();
            }
            ImGui.Unindent(10.0f);

            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, $"History:");
        if (ImGui.BeginChild("SearchSourceHistoryChild", new Vector2(0, 200 * ImGuiHelpers.GlobalScale)))
        {
            if (ImGui.BeginTable($"##SearchSourceHistoryTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 10.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##amount", 0, 0.2f);

                ImGui.Indent(10.0f);
                foreach (var result in history)
                {
                    foreach (var itemResult in result.Received)
                    {
                        var item = ItemSheet.GetRow(itemResult.Item)!;

                        ImGui.TableNextColumn();
                        DrawIcon(item.Icon);
                        ImGui.TableNextColumn();

                        var name = Utils.ToStr(item.Name);
                        ImGui.TextUnformatted(name);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(name);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"x{itemResult.Count}");
                        ImGui.TableNextRow();
                    }

                    // add spacing
                    ImGui.TableNextColumn();
                    ImGuiHelpers.ScaledDummy(5.0f);

                    ImGui.TableNextRow();
                }
                ImGui.Unindent(10.0f);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        ImGuiHelpers.ScaledDummy(5.0f);
    }

    private void ItemSearch(IEnumerable<CharacterConfiguration> characters)
    {
        var historyItemDict = new Dictionary<uint, Dictionary<uint, List<DesynthResult>>>();
        foreach (var pair in characters.SelectMany(c => c.Storage.History))
        {
            if (!pair.Value.Received.Any())
            {
                PluginLog.Error($"Found error entry: {pair.Key}");
                continue;
            }

            if (!historyItemDict.TryAdd(pair.Value.Received.First().Item, new Dictionary<uint, List<DesynthResult>> { {pair.Value.Source, new List<DesynthResult> {pair.Value}} }))
                if (!historyItemDict[pair.Value.Received.First().Item].TryAdd(pair.Value.Source, new List<DesynthResult> { pair.Value }))
                    historyItemDict[pair.Value.Received.First().Item][pair.Value.Source].Add(pair.Value);
        }

        var sourceItem = ItemSheet.GetRow(RewardSearchResult)!;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Searched for {Utils.ToStr(sourceItem.Name)}");
        if (!historyItemDict.TryGetValue(RewardSearchResult, out var history))
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this reward item ...");
            return;
        }

        var statDict = new Dictionary<uint, (uint Min, uint Max)>();
        foreach (var (source, result) in history.Where(pair => pair.Key > 0))
        {
            foreach (var desynthResult in result)
            {
                var count = desynthResult.Received.First().Count;
                if (!statDict.TryAdd(source, (count, count)))
                {
                    var stat = statDict[source];
                    if (stat.Min > count)
                        statDict[source] = (count, stat.Max);

                    if (stat.Max < count)
                        statDict[source] = (stat.Min, count);
                }
            }
        }

        var desynthesized = history.Values.Sum(list => list.Count);
        ImGui.TextColored(ImGuiColors.HealerGreen, $"Seen as reward {desynthesized:N0} time{(desynthesized > 1 ? "s" : "")}");
        if (ImGui.BeginTable($"##HistoryStats", 4, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("##statItemName", 0, 0.6f);
            ImGui.TableSetupColumn("##statMin", 0, 0.1f);
            ImGui.TableSetupColumn("##statSymbol", 0, 0.05f);
            ImGui.TableSetupColumn("##statMax", 0, 0.1f);

            foreach (var statPair in SortByKeyCustom(statDict))
            {
                var name = Utils.ToStr(ItemSheet.GetRow(statPair.Key)!.Name);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}"))
                    ImGui.SetClipboardText(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Min}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("-");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{statPair.Value.Max}");

                ImGui.TableNextRow();
            }

            ImGui.EndTable();
        }
    }

    private void Find()
    {
        if (!ImGui.BeginTabItem("Find"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.HealerGreen, "Search for a desynthesizable source");

        var changed = false;
        if (ImGui.SliderInt("##ilvlInput", ref ILvLSearchResult, 1, HighestILvL, "iLvL %d"))
        {
            ILvLSearchResult = (int) Math.Round(ILvLSearchResult / 5.0) * 5;
            changed = true;
        }
        changed |= ImGui.Combo("##jobSelection", ref SelectedJob, Jobs, Jobs.Length);
        changed |= ImGui.Checkbox("Exclude Gear", ref ExcludeGear);
        changed |= ImGui.Checkbox("Exclude Marketboard Prohibited", ref ExcludeNonMB);

        if (changed)
        {
            SearchCache = DesynthCache.Where(i => i.ItemUICategory.Row != 39)
                                      .Where(i => i.LevelItem.Row > ILvLSearchResult)
                                      .Where(i => i.ClassJobRepair.Row == SelectedJob + 8)
                                      .Where(i => !ExcludeGear || i.EquipSlotCategory.Row == 0)
                                      .Where(i => !ExcludeNonMB || !i.IsUntradable)
                                      .OrderBy(i => i.LevelItem.Row)
                                      .ToArray();
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (!SearchCache.Any())
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"Nothing found for this job and iLvL ...");

            ImGui.EndTabItem();
            return;
        }

        ImGui.TextColored(ImGuiColors.HealerGreen, $"Found {SearchCache.Length:N0} item{(SearchCache.Length > 1 ? "s" : "")}");
        if (ImGui.BeginChild("##PossibleItemsChild"))
        {
            ImGui.Indent(10.0f);
            if (ImGui.BeginTable($"##PossibleItemsTable", 3))
            {
                ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, IconSize.X + 5.0f);
                ImGui.TableSetupColumn("##item");
                ImGui.TableSetupColumn("##iLvL", 0, 0.25f);

                foreach (var item in SearchCache)
                {
                    ImGui.TableNextColumn();
                    DrawIcon(item.Icon);
                    ImGui.TableNextColumn();

                    var name = Utils.ToStr(item.Name);
                    if (ImGui.Selectable(name))
                        ImGui.SetClipboardText(name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"iLvL {item.LevelItem.Row}");
                    ImGui.TableNextRow();
                }

                ImGui.EndTable();
            }
            ImGui.Unindent(10.0f);
        }
        ImGui.EndChild();

        ImGui.EndTabItem();
    }

private const int GilItemOrder = 1_000_000;
private const int CrystalOrder = 2_000_000;
public static IOrderedEnumerable<KeyValuePair<uint, TValue>> SortByKeyCustom<TValue>(Dictionary<uint, TValue> unsortedDict)
{
    return unsortedDict.OrderBy(pair =>
    {
        var idx = pair.Key;
        if (idx < 20)
            idx += CrystalOrder;
        else if (Desynth.GilItems.ContainsKey(idx))
            idx += GilItemOrder;

        return idx;
    });
}
}
