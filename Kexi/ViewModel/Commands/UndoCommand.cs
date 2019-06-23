﻿using System;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using Kexi.Files;
using Kexi.Interfaces;

namespace Kexi.ViewModel.Commands
{
    [Export]
    [Export(typeof(IKexiCommand))]
    public class UndoCommand : IKexiCommand
    {
        [ImportingConstructor]
        public UndoCommand(Workspace workspace)
        {
            _workspace = workspace;
        }

        public bool CanExecute(object parameter)
        {
            if (_workspace.CommandRepository.LastCommand is IUndoable undoable)
            { 
                return true;
            }

            return false;
        }

        public void Execute(object parameter)
        {
            if (_workspace.CommandRepository.LastCommand is IUndoable undoable)
            {
                undoable.Undo();
            }
            else
                _workspace.NotificationHost.AddInfo("Cant undo last command");
        }


        public event EventHandler  CanExecuteChanged;
        private readonly Workspace _workspace;
    }
}