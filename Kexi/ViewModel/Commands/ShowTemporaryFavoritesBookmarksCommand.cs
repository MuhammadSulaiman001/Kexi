﻿using System;
using System.ComponentModel.Composition;
using Kexi.Interfaces;
using Kexi.ViewModel.Popup;

namespace Kexi.ViewModel.Commands
{
    [Export]
    [Export(typeof(IKexiCommand))]
    public class ShowTemporaryFavoritesBookmarksCommand : IKexiCommand
    {
        private readonly Workspace _workspace;
        private readonly TemporaryFavoritesPopupViewModel _favoritesPopup;

        [ImportingConstructor]
        public ShowTemporaryFavoritesBookmarksCommand(Workspace workspace, TemporaryFavoritesPopupViewModel favoritesPopup)
        {
            _workspace = workspace;
            _favoritesPopup = favoritesPopup;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _workspace.PopupViewModel = _favoritesPopup;
            _favoritesPopup.Open();
        }

        public event EventHandler CanExecuteChanged;
    }
}