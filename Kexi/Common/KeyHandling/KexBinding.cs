﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml.Serialization;
using Kexi.Annotations;
using Kexi.Interfaces;
using Kexi.ViewModel.Lister;
using Microsoft.CodeAnalysis.Operations;

namespace Kexi.Common.KeyHandling
{
    [XmlInclude(typeof(KexDoubleBinding))]
    [Serializable]
    public class KexBinding : INotifyPropertyChanged 
    {
        private ModifierKeys _modifier;
        private Key _key;
        private object[] _commandArguments;
        private string _commandName;

        protected KexBinding() {}
        public KexBinding(string group, Key key, ModifierKeys modifier, string commandName, IKexiCommand action, object[] commandArguments = null)
        {
            Key = key;
            Modifier = modifier;
            Command = action;
            CommandName = commandName; 
            CommandArguments = commandArguments;
            Group = group;
        }

        public string CommandName
        {
            get => _commandName;
            set
            {
                if (value == _commandName)
                    return;
                _commandName = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore]
        public object[] CommandArguments
        {
            get => _commandArguments;
            set
            {
                if (Equals(value, _commandArguments)) return;
                _commandArguments = value;
                OnPropertyChanged();
            }
        }

        public ModifierKeys Modifier
        {
            get => _modifier;
            set
            {
                if (value == _modifier) return;
                _modifier = value;
                OnPropertyChanged();
            }
        }

        public Key Key
        {
            get => _key;
            set
            {
                if (value == _key) return;
                _key = value;
                OnPropertyChanged();
            }
        }
        public string Group { get; set; }

        [XmlIgnore]
        public IKexiCommand Command { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
