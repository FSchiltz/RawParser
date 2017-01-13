using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawEditor
{
    public class Bindable<Type> : INotifyPropertyChanged
    {
        private Type val;
        public event PropertyChangedEventHandler PropertyChanged;

        public Bindable(Type defaultValue)
        {
            Value = defaultValue;
        }

        public Type Value
        {
            get { return val; }
            set
            {
                if (!EqualityComparer<Type>.Default.Equals(this.val, value))
                {
                    this.val = value;
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
