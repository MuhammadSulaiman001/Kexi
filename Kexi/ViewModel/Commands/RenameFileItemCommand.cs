﻿using System;
using System.ComponentModel.Composition;
using Kexi.Interfaces;

namespace Kexi.ViewModel.Commands
{
    [Export]
    [Export(typeof(IKexiCommand))]
    public class RenameFileItemCommand : IKexiCommand
    {
        private readonly Workspace _workspace;

        [ImportingConstructor]
        public RenameFileItemCommand(Workspace workspace)
        {
            _workspace = workspace;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _workspace.RenamePopupViewModel.IsOpen = true;
        }

        public event EventHandler CanExecuteChanged;
    }
}