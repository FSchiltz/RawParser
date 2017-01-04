using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawEditor
{
    public class Bindable<S> : INotifyPropertyChanged
    {
        protected S value;
        public event PropertyChangedEventHandler PropertyChanged;

        public Bindable(S def)
        {
            Value = def;
        }

        public S Value
        {
            get { return value; }
            set
            {
                if (!EqualityComparer<S>.Default.Equals(this.value, value))
                {
                    this.value = value;
                    OnPropertyChanged("Value");
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
