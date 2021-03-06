﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Kexi.ViewModel;
using Kexi.ViewModel.Lister;

namespace Kexi.Common.KeyHandling
{
    public class KeyDispatcher
    {
        public const  Key AlternateEscape = Key.Oem102;
        public static Key MoveDownKey     = Key.J;
        public static Key MoveUpKey       = Key.K;
        public static Key MoveLeftKey     = Key.H;
        public static Key MoveRightKey    = Key.L;

        public KeyDispatcher(Workspace workspace)
        {
            Workspace = workspace;
            Configuration = KeyConfigurationSerializer.GetConfiguration();
            var keyModeBindings = Configuration.Bindings;

            Workspace.PropertyChanged += Workspace_PropertyChanged;
            _viStyleKeyHandler        =  new ViStyleKeyHandler(workspace, keyModeBindings.FirstOrDefault(b => b.KeyMode == KeyMode.ViStyle)?.KeyBindings);
            _classicKeyHandler        =  new ClassicKeyHandler(workspace, keyModeBindings.FirstOrDefault(b => b.KeyMode == KeyMode.Classic)?.KeyBindings);
            //Livefilter uses same Keybindingings as Classic
            _liveFilterKeyHandler = new LiveFilterKeyHandler(workspace, keyModeBindings.FirstOrDefault(b => b.KeyMode == KeyMode.Classic)?.KeyBindings);
        }

        private Workspace Workspace { get; }
        public IEnumerable<KexBinding> ViBindings => _viStyleKeyHandler.Bindings;
        public IEnumerable<KexBinding> ClassicBindings => _classicKeyHandler.Bindings;

        public IEnumerable<KexBinding> AllBindings => ViBindings.Concat(ClassicBindings);

        public IEnumerable<KexBinding> ActiveBindings => Workspace.Options.KeyMode == KeyMode.ViStyle
            ? ViBindings
            : ClassicBindings;

        private readonly ClassicKeyHandler    _classicKeyHandler;
        private readonly LiveFilterKeyHandler _liveFilterKeyHandler;
        private readonly ViStyleKeyHandler    _viStyleKeyHandler;
        public KeyConfiguration Configuration { get; }

        private void Workspace_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Workspace.ActiveLister)) _classicKeyHandler.ClearSearchString();
        }

        public void Execute(KeyEventArgs args, ILister lister, string group = null)
        {
            switch (Workspace.Options.KeyMode)
            {
                case KeyMode.Classic:
                    _classicKeyHandler.Execute(args, lister, group);
                    break;
                case KeyMode.LiveFilter:
                    _liveFilterKeyHandler.Execute(args, lister, group);
                    break;
                default:
                    _viStyleKeyHandler.Execute(args, lister, group);
                    break;
            }
        }

    }
}