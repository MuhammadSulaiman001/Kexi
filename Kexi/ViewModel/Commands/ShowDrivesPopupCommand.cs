﻿using System;
using System.ComponentModel.Composition;
using Kexi.Interfaces;
using Kexi.ViewModel.Popup;

namespace Kexi.ViewModel.Commands
{
    [Export]
    [Export(typeof(IKexiCommand))]
    public class ShowDrivesPopupCommand : IKexiCommand
    {
        private readonly Workspace _workspace;
        private readonly DrivePopupViewModel _drivePopup;

        [ImportingConstructor]
        public ShowDrivesPopupCommand(Workspace workspace, DrivePopupViewModel drivePopup)
        {
            _workspace = workspace;
            _drivePopup = drivePopup;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _workspace.PopupViewModel = _drivePopup;
            _drivePopup.IsOpen = true;
        }

        public event EventHandler CanExecuteChanged;
    }
}