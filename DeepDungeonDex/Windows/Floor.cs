﻿using System;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using DeepDungeonDex.Storage;
using ImGuiNET;
using DeepDungeonDex.Hooks;
using DeepDungeonDex.Models;
using System.Numerics;

namespace DeepDungeonDex.Windows
{
    public class Floor : Window, IDisposable
    {
        private readonly ClientState _clientState;
        private readonly Condition _condition;
        private readonly Framework _framework;
        private readonly StorageHandler _storage;
        private readonly AddonAgent _addon;
        private byte _debug;
        private string _dataPath;

        public Floor(StorageHandler storage, CommandHandler command, Framework framework, Condition condition, ClientState state, AddonAgent addon) : base("DeepDungeonDex FloorGuide", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
        {
            _storage = storage;
            _framework = framework;
            _condition = condition;
            _clientState = state;
            _addon = addon;
            command.AddCommand("debugfloor", args =>
            {
                var argArr = args.Split(' ');
                if (!byte.TryParse(argArr[0], out var id) || !uint.TryParse(argArr[1], out var ter) || ter is > 1 or < 0)
                {
                    _debug = 0;
                    _dataPath = "";
                    IsOpen = false;
                    return;
                }

                _debug = id;
#pragma warning disable CS8509
                _dataPath = ter switch
#pragma warning restore CS8509
                {
                    0 => "PotD",
                    1 => "HoH"
                };
                IsOpen = true;

            }, show: false);
            var config = _storage.GetInstance<Configuration>()!;
            SizeConstraints = new WindowSizeConstraints
            {
                MaximumSize = new Vector2(800 * config.WindowSizeScaled, 600),
                MinimumSize = new Vector2(450 * config.WindowSizeScaled, 100)
            };
            framework.Update += GetData;
            config.OnChange += ConfigChanged;
            state.TerritoryChanged += TerritoryChanged;
            TerritoryChanged(null, state.TerritoryType);
        }

        private void TerritoryChanged(object? sender, ushort e)
        {
            _dataPath = e switch
            {
                >= 561 and <= 565 or >= 593 and <= 607 => "PotD",
                >= 770 and <= 775 or >= 782 and <= 785 => "HoH",
                >= 1099 and <= 1108 => "EO",
                _ => ""
            };
        }

        private void GetData(Framework framework)
        {
            if (_debug != 0)
                return;

            if (_condition[ConditionFlag.InDeepDungeon])
            {
                var config = _storage.GetInstance<Configuration>()!;
                if(!_addon.Disabled && _dataPath != "" && !config.HideFloor && _addon.Floor % 10 != 0 && _storage.GetInstance(_dataPath + "/Floors.yml") != null)
                    IsOpen = true;
                else
                    IsOpen = false;
            }
            else
                IsOpen = false;
        }

        private void ConfigChanged(Configuration config)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MaximumSize = new Vector2(800 * config.WindowSizeScaled, 600),
                MinimumSize = new Vector2(450 * config.WindowSizeScaled, 100)
            };
            BgAlpha = config.Opacity;
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (config.Clickthrough && !Flags.HasFlag(ImGuiWindowFlags.NoInputs))
                Flags |= ImGuiWindowFlags.NoInputs;
            else if (!config.Clickthrough && Flags.HasFlag(ImGuiWindowFlags.NoInputs))
                Flags &= ~ImGuiWindowFlags.NoInputs;
        }

        public override void Draw()
        {
            var locale = _storage.GetInstances<Locale>();
            var remap = (FloorData)((Storage.Storage)_storage.GetInstance(_dataPath + "/Floors.yml")!).Value;
            var floor = _debug == 0 ? _addon.Floor : _debug;
            floor = remap.FloorDictionary.TryGetValue(floor, out var f) ? f : floor;
            ImGui.PushFont(Font.RegularFont);
            try
            {
                ImGui.Text("Floor Help");
                ImGui.TextUnformatted(locale.GetLocale($"{_dataPath}{floor}"));
            }
            catch
            {
                // ignored
            }
            ImGui.PopFont();
        }

        public void Dispose()
        {
            _framework.Update -= GetData;
            _clientState.TerritoryChanged -= TerritoryChanged;
            var _config = _storage.GetInstance<Configuration>()!;
            _config.OnChange -= ConfigChanged;
        }
    }
}
