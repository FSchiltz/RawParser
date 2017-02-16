using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawEditor.Base
{
    public class Bindable<TValue> : INotifyPropertyChanged
    {
        private TValue val;
        public event PropertyChangedEventHandler PropertyChanged;

        public Bindable(TValue defaultValue)
        {
            Value = defaultValue;
        }

        public TValue Value
        {
            get { return val; }
            set
            {
                if (!EqualityComparer<TValue>.Default.Equals(this.val, value))
                {
                    this.val = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
