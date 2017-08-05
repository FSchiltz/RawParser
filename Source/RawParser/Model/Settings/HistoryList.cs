using PhotoNet;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawEditor.Settings
{
    public class HistoryCollection : ObservableCollection<HistoryObject>
    {
        private int CurrentIndex
        {
            get { return index; }
            set
            {
                index = value;
                IsUndoEnabled = !(index < 0);
                IsRedoEnabled = (index < Count - 1);
            }
        }
        private int index = -1;
        public event PropertyChangedEventHandler HistoryChanged;
        public HistoryObject Default { get; set; }
        private bool undoEnabled = false, redoEnabled = false;
        public bool IsUndoEnabled
        {
            get { return undoEnabled; }
            set
            {
                undoEnabled = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsUndoEnabled"));
            }
        }
        public bool IsRedoEnabled
        {
            get { return redoEnabled; }
            set
            {
                redoEnabled = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsRedoEnabled"));
            }
        }

        protected void OnHistoryChanged([CallerMemberName] string propertyName = null)
        {
            HistoryChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Undo()
        {
            if (CurrentIndex >= 0)
            {
                CurrentIndex--;
                OnHistoryChanged();
            }
        }

        public new void Add(HistoryObject history)
        {
            CurrentIndex++;
            Insert(CurrentIndex, history);
        }

        public new void Clear()
        {
            CurrentIndex = -1;
            IsRedoEnabled = IsUndoEnabled = false;
            base.Clear();
        }

        public void Redo()
        {
            if (CurrentIndex < Count - 1)
            {
                CurrentIndex++;
                OnHistoryChanged();
            }
        }

        public void SetCurrent(int indice)
        {
            if (indice >= 0 && indice < Count)
            {
                CurrentIndex = indice;
                OnHistoryChanged();
            }
        }

        public HistoryObject Current
        {
            get
            {
                if (CurrentIndex >= 0)
                {
                    return this[CurrentIndex];
                }
                else return Default;
            }
        }
    }
}