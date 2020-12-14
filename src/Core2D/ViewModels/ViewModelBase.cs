﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core2D.ViewModels
{
    public partial class ViewModelBase : INotifyPropertyChanged
    {
        private bool _isDirty;
        protected readonly IServiceProvider _serviceProvider;

        [AutoNotify] private ViewModelBase _owner;
        [AutoNotify] private string _name = "";

        public ViewModelBase(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public virtual bool IsDirty()
        {
            return _isDirty;
        }

        public virtual void Invalidate()
        {
            _isDirty = false;
        }

        public virtual void MarkAsDirty()
        {
            _isDirty = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual object Copy(IDictionary<object, object> shared)
        {
            throw new NotImplementedException();
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = default)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = default)
        {
            if (Equals(field, value))
            {
                return false;
            }
            field = value;
            _isDirty = true;
            RaisePropertyChanged(propertyName);
            return true;
        }
    }
}