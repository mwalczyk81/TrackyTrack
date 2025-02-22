﻿using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class FrameworkManager
{
    private readonly Plugin Plugin;

    public bool IsSafe;

    private static readonly Dictionary<Currency, int> CurrencyCounts = new()
    {
        { Currency.Gil, 0 },             // Gil
        { Currency.StormSeals, 0 },      // Storm Seals
        { Currency.SerpentSeals, 0 },    // Serpent Seals
        { Currency.FlameSeals, 0 },      // Flame Seals
        { Currency.MGP, 0 },             // MGP
        { Currency.AlliedSeals, 0 },     // Allied Seals
        { Currency.Ventures, 0 },        // Venture
        { Currency.SackOfNuts, 0 },      // Sack of Nuts
        { Currency.CenturioSeals, 0 },   // Centurio Seals
        { Currency.Bicolor, 0 },         // Bicolor
        { Currency.Skybuilders, 0 },     // Skybuilders
    };

    private uint LastSeenVentureId = 0;

    public FrameworkManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Framework.Update += CofferTracker;
        Plugin.Framework.Update += CurrencyTracker;
        Plugin.Framework.Update += EurekaTracker;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= CofferTracker;
        Plugin.Framework.Update -= CurrencyTracker;
        Plugin.Framework.Update -= EurekaTracker;
    }
    public unsafe void ScanCurrentCharacter()
    {
        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        foreach (var currency in CurrencyCounts.Keys)
            CurrencyCounts[currency] = instance->GetInventoryItemCount((uint) currency, false, false, false);

        IsSafe = true;
    }

    public unsafe void RetainerPreChecker(AddonArgs addonInfo)
    {
        var retainer = RetainerManager.Instance();
        if (retainer != null)
        {
            try
            {
                if (addonInfo.AddonName == "SelectString")
                    LastSeenVentureId = retainer->GetActiveRetainer()->VentureID;
            }
            catch
            {
                // Do nothing
            }
        }
    }

    public unsafe void RetainerChecker(AddonArgs addonInfo)
    {
        if (addonInfo.AddonName == "RetainerTaskResult")
        {
            try
            {
                    var value = AtkStage.GetSingleton()->AtkArrayDataHolder->NumberArrays[105]->IntArray;
                    var primary = value[295];
                    var primaryHQ = primary > 1_000_000;
                    if (primaryHQ)
                        primary -= 1_000_000;
                    var primaryCount = (short)(value[297] & 0xffff);

                    var additionalItem = value[298];
                    var additionalHQ = additionalItem > 1_000_000;
                    if (additionalHQ)
                        additionalItem -= 1_000_000;
                    var additionalCount = (short) (value[300] & 0xffff);

                    Plugin.RetainerHandler(LastSeenVentureId,new VentureItem((uint) primary, primaryCount, primaryHQ),new VentureItem((uint) additionalItem, additionalCount, additionalHQ));
            }
            catch
            {
                // Do nothing
            }
        }
    }

    public unsafe void CurrencyTracker(Framework _)
    {
        // Only run for real characters
        if (Plugin.ClientState.LocalContentId == 0)
        {
            IsSafe = false;
            return;
        }

        if (!IsSafe)
        {
            ScanCurrentCharacter();
            return;
        }

        var instance = InventoryManager.Instance();
        if (instance == null)
            return;

        if (Plugin.Configuration.EnableRepair)
        {
            var currentGil = instance->GetInventoryItemCount((uint) Currency.Gil, false, false, false);
            if (currentGil < CurrencyCounts[Currency.Gil])
                Plugin.TimerManager.RepairResult(CurrencyCounts[Currency.Gil] - currentGil);
            CurrencyCounts[Currency.Gil] = currentGil;
        }

        if (Plugin.Configuration.EnableCurrency)
        {
            foreach (var (currency, oldCount) in CurrencyCounts)
            {
                var current = instance->GetInventoryItemCount((uint) currency, false, false, false);
                if (current > oldCount)
                    Plugin.CurrencyHandler(currency, current - oldCount);
                CurrencyCounts[currency] = current;
            }
        }
    }

    public void CofferTracker(Framework _)
    {
        var local = Plugin.ClientState.LocalPlayer;
        if (local == null || !local.IsCasting)
            return;

        switch (local)
        {
            // Coffers
            case { CastActionId: 32161, CastActionType: 2 }:
            case { CastActionId: 36635, CastActionType: 2 }:
            case { CastActionId: 36636, CastActionType: 2 }:
            {
                if (Plugin.Configuration.EnableVentureCoffers || Plugin.Configuration.EnableGachaCoffers)
                    Plugin.TimerManager.StartCoffer();
                break;
            }

            // Tickets
            case { CastActionId: 21069, CastActionType: 2 }:
            case { CastActionId: 21070, CastActionType: 2 }:
            case { CastActionId: 21071, CastActionType: 2 }:
            case { CastActionId: 30362, CastActionType: 2 }:
            case { CastActionId: 28064, CastActionType: 2 }:
            {
                if (Plugin.TimerManager.TicketUsedTimer.Enabled)
                    return;

                // 100ms before cast finish is when cast counts as successful
                if (local.CurrentCastTime + 0.100 > local.TotalCastTime)
                    Plugin.CastedTicketHandler(local.CastActionId);
                break;
            }
        }
    }

    public void EurekaTracker(Framework _)
    {
        if (!Plugin.Configuration.EnableEurekaCoffers)
            return;

        if (!EurekaExtensions.AsArray.Contains(Plugin.ClientState.TerritoryType))
            return;

        var local = Plugin.ClientState.LocalPlayer;
        if (local == null || !local.IsCasting)
            return;

        // Interaction cast on coffer
        if (local is { CastActionId: 21, CastActionType: 4 })
        {
            if (Plugin.TimerManager.AwaitingEurekaResult.Enabled)
                return;

            if (local.TargetObject == null)
                return;

            // 100ms before cast finish is when cast counts as successful
            if (local.CurrentCastTime + 0.100 > local.TotalCastTime)
                Plugin.TimerManager.StartEureka(local.TargetObject.DataId);
        }
    }
}
