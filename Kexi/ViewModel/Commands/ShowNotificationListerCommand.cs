﻿using System;
using System.ComponentModel.Composition;
using Kexi.Common;
using Kexi.Interfaces;
using Kexi.ViewModel.Lister;

namespace Kexi.ViewModel.Commands
{
    [Export]
    [Export(typeof(IKexiCommand))]
    public class ShowNotificationListerCommand : IKexiCommand
    {
        private readonly Workspace _workspace;

        [ImportingConstructor]
        public ShowNotificationListerCommand(Workspace workspace)
        {
            _workspace = workspace;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            var notifications = KexContainer.Resolve<NotificationLister>();
            _workspace.Open(notifications);
            notifications.Refresh();
        }

        public event EventHandler CanExecuteChanged;
    }
}