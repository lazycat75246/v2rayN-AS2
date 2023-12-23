using System.ComponentModel;

namespace v2rayN.Mode
{
    public class MainServerItem : ObservableObject
    {  
        public string indexId { get; set; }
        public string ServerName { get; set; }
        public bool IsChecked { get; set; }
    }
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
