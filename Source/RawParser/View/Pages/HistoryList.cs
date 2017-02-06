using RawEditor.Effect;
using RawEditor.Settings;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawEditor.View.Pages
{
    public class HistoryList : ObservableCollection<HistoryObject>
    {
        public int CurrentIndex { get; set; } = 0;
        public ImageEffect effect;
        public event PropertyChangedEventHandler HistoryChanged;

        protected void OnHistoryChanged([CallerMemberName] string propertyName = null)
        {
            HistoryChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Undo()
        {
            CurrentIndex--;
            switch (this[CurrentIndex].target)
            {
                case EffectObject.Exposure:
                    effect.Exposure = (double)this[CurrentIndex].oldValue;
                    break;
            }
            OnHistoryChanged();
        }

        public void Redo() { }
    }
}