using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TransProp.Core;

namespace TransProp.Client
{

    public class Mediator
    {
        public enum Messages
        {
            SaveCurrentWork
        }

        private static Mediator instance;
        public static Mediator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Mediator();
                }
                return instance;
            }
        }

        private Mediator() { }

        Dictionary<Messages, List<Action<object>>> callbacks = new Dictionary<Messages, List<Action<object>>>();

        public void SendMessage(Messages message, object parameter)
        {
            List<Action<object>> actions;
            if (callbacks.TryGetValue(message, out actions))
            {
                foreach (Action<object> action in actions)
                {
                    action(parameter);
                }
            }
        }

        public void Subscribe(Messages message, Action<object> callback)
        {
            if (!callbacks.ContainsKey(message))
            {
                callbacks.Add(message, new List<Action<object>>());
            }
            callbacks[message].Add(callback);
        }

    }

    public class CompViewModelItem : INotifyPropertyChanged
    {
        public string Key { get; set; }
        public string ReferenceValue { get; set; }

        private string referenceValueForSearch ;
        public string ReferenceValueForSearch {
            get
            {
                if (referenceValueForSearch == null)
                {
                    referenceValueForSearch = ReferenceValue.ToUpperInvariant();
                }
                return referenceValueForSearch;
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private string newValue;
        public string NewValue
        {
            get { return newValue; }
            set
            {
                if (newValue != value)
                {
                    newValue = value;
                    NewValueForSearch = value.ToUpperInvariant();
                    OnPropertyChanged("NewValue");
                }
            }
        }

        public string NewValueForSearch { get; private set; }

        public bool IsComment
        {
            get
            {
                return Key == "#" || Key == "!";
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadTransFileButton.IsEnabled = false;
            SaveTranslationButton.IsEnabled = false;
            Mediator.Instance.Subscribe(Mediator.Messages.SaveCurrentWork, SaveCurrentWork);
        }

        private void SaveCurrentWork(object obj)
        {
            if (list == null)
            {
                return;
            }
            string fileName = string.Empty;
            if (obj != null)
            {
                fileName = obj.ToString();
            }

            if (string.IsNullOrEmpty(fileName))
            {
                if (!list.Any(vm => !string.IsNullOrEmpty(vm.NewValue)))
                {//nothing to save
                    return;
                }
                string directory = System.IO.Path.GetDirectoryName(referenceFileName);
                directory = System.IO.Path.Combine(directory, "Backup");
                fileName = System.IO.Path.Combine(directory, DateTime.Now.ToString("dd_MM_yyyy_HH_mm") + ".properties");
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
            }

            PropsDocument doc = new PropsDocument();
            foreach (CompViewModelItem vm in list)
            {
                string value = vm.IsComment ? vm.ReferenceValue : vm.NewValue;
                doc.Add(new PropsElement(vm.Key, value));
            }
            doc.Save(fileName);
        }

        private void ChangeIsEnableControls(bool isEnable)
        {
            LoadTransFileButton.IsEnabled = isEnable;
            SaveTranslationButton.IsEnabled = isEnable;
            LoadRefFileButton.IsEnabled = isEnable;
        }

        private ObservableCollection<CompViewModelItem> list;
        private string referenceFileName;
        private string transFileName;

        private PropsDocument referenceDocument;
        private PropsDocument transDocument;
        private void loadReferenceFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (referenceDocument != null)
            {
                if (MessageBox.Show(
@"If you load a new document you may loose your work.
You might want to save your work first.
If you click yes you current work will be overriden.", "Save before.", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            ChangeIsEnableControls(false);

            referenceFileName = SelectOpenFile();
            if (string.IsNullOrEmpty(referenceFileName))
            {
                ChangeIsEnableControls(true);
                return;
            }
            SetMessage("Loading properties...");

            ExecuteAsync(() =>
            {
                referenceDocument = new PropsDocument(true);
                referenceDocument.Load(referenceFileName);

                //creating a new document for translation
                transDocument = new PropsDocument();

                list = new ObservableCollection<CompViewModelItem>();
                foreach (PropsElement element in referenceDocument.Elements)
                {
                    PropsElement copyElement = new PropsElement(element.Key, string.Empty);
                    list.Add(new CompViewModelItem { Key = element.Key, ReferenceValue = element.Value, NewValue = string.Empty });
                    transDocument.Add(copyElement);
                }
            }, () =>
            {
                SetMessage(referenceFileName + " loaded");
                ClearSearch();
                ListControl.ItemsSource = list;

                ChangeIsEnableControls(true);
            });
        }

        private void SetMessage(string message)
        {
            MessageText.Text = message;
        }

        private void loadTransFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (list.Any(vm => !string.IsNullOrEmpty(vm.NewValue)))
            {
                if (MessageBox.Show(
@"If you load a new document you may loose your work.
You might want to save your work first.
If you click yes you current work will be overriden.", "Save before.", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }


            ChangeIsEnableControls(false);
            transFileName = SelectOpenFile();
            if (string.IsNullOrEmpty(transFileName))
            {
                ChangeIsEnableControls(true);
                return;
            }
            SetMessage("Loading file...");
            ExecuteAsync(() =>
            {
                transDocument = new PropsDocument(false);
                transDocument.Load(transFileName);

                //todo : find a way to keep translate doc comments
                transDocument.RemoveComments();
            }, () =>
            {



                //verify if the new file schema is the same as the reference
                if (!SchemasMatch())
                {
                    MessageBox.Show("This document does not match the reference document.");
                    ChangeIsEnableControls(true);
                    return;
                }

                //fill only the translate column
                List<PropsElement> tempList = transDocument.Elements.ToList();
                if (list != null)
                {
                    foreach (CompViewModelItem vm in list)
                    {
                        if (!vm.IsComment)
                        {
                            PropsElement element = tempList.FirstOrDefault(el => el.Key == vm.Key);
                            if (element == null)
                            {
                                ChangeIsEnableControls(true);
                                throw new Exception("Cant find trans Element when sync loaded trans document.");
                            }
                            vm.NewValue = element.Value;
                            tempList.Remove(element);
                        }
                    }
                    if (tempList.Any())
                    {
                        ChangeIsEnableControls(true);
                        throw new Exception("Elements left in trans document after sync.");
                    }
                }
                ChangeIsEnableControls(true);
                ClearSearch();
            });
        }

        private bool SchemasMatch()
        {
            List<PropsElement> refElements = referenceDocument.Elements.Where(e => !e.IsComment).ToList();
            List<PropsElement> trsElements = transDocument.Elements.Where(e => !e.IsComment).ToList();

            if (refElements.Count != trsElements.Count)
            {
                return false;
            }
            for (int i = 0; i < refElements.Count; i++)
            {
                PropsElement refEl = refElements[i];
                PropsElement trsEl = trsElements[i];
                if (refEl.Key != trsEl.Key)
                {
                    return false;
                }
            }
            return true;
        }

        Microsoft.Win32.OpenFileDialog openFileDialog;
        private string SelectOpenFile()
        {
            if (openFileDialog == null)
            {
                openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.DefaultExt = ".properties";
                openFileDialog.Filter = "Properties files (.properties)|*.properties";
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Multiselect = false;
            }

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            return string.Empty;
        }

        Microsoft.Win32.SaveFileDialog saveFileDialog;
        private void SaveTranslationButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeIsEnableControls(false);
            if (saveFileDialog == null)
            {
                saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                saveFileDialog.AddExtension = true;
                saveFileDialog.CreatePrompt = true;
                saveFileDialog.DefaultExt = ".properties";
                saveFileDialog.Filter = "Properties files (.properties)|*.properties";
                saveFileDialog.OverwritePrompt = true;
            }

            saveFileDialog.FileName = string.IsNullOrEmpty(transFileName)
                                        ? "NewTranslation_" + DateTime.Now.ToString("dd_MM_yyyy")
                                        : transFileName;

            if (saveFileDialog.ShowDialog() == true)
            {
                transFileName = saveFileDialog.FileName;
                SaveCurrentWork(transFileName);
            }
            ChangeIsEnableControls(true);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MessageBox.Show(
@"Make sure you have saved all your work.
Click OK to exit the application.
",
"Exit application", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement el = sender as FrameworkElement;
            if (el != null)
            {
                CompViewModelItem item = el.DataContext as CompViewModelItem;
                if (item != null)
                {
                    item.NewValue = item.ReferenceValue;
                }
            }
        }

        private void ExecuteAsync(Action action, Action callback)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    action();
                }
                finally
                {
                    Dispatcher.BeginInvoke(callback);
                }
            });
        }

        private void Filter()
        {
            if (list == null)
            {
                return;
            }
            string refTerm = searchReference.Text;
            string transTerm = searchTrans.Text;
            if (string.IsNullOrEmpty(refTerm) && string.IsNullOrEmpty(transTerm))
            {
                ListControl.ItemsSource = list;
            }
            else
            {
                refTerm = refTerm.ToUpperInvariant();
                transTerm = transTerm.ToUpperInvariant();

                IEnumerable<CompViewModelItem> filteredList = null;
                if (!string.IsNullOrEmpty(refTerm) && string.IsNullOrEmpty(transTerm))
                {
                    filteredList = list.Where(vm => !vm.IsComment && vm.ReferenceValueForSearch.Contains(refTerm));
                }
                else if (string.IsNullOrEmpty(refTerm) && !string.IsNullOrEmpty(transTerm))
                {
                    filteredList = list.Where(vm => !vm.IsComment && vm.NewValueForSearch.Contains(transTerm));
                }
                else
                {
                    filteredList = list.Where(vm => !vm.IsComment && vm.ReferenceValueForSearch.Contains(refTerm) && vm.NewValueForSearch.Contains(transTerm));
                }

                if (filteredList != null)
                {
                    ListControl.ItemsSource = filteredList;
                }
            }
        }


        private void searchTrans_KeyUp(object sender, KeyEventArgs e)
        {
            Filter();
        }

        private void searchReference_KeyUp(object sender, KeyEventArgs e)
        {
            Filter();
        }

        private void clearTransSearch_Click(object sender, RoutedEventArgs e)
        {
            this.searchTrans.Text = "";
            Filter();
        }

        private void clearReferenceSearch_Click(object sender, RoutedEventArgs e)
        {
            this.searchReference.Text = "";
            Filter();
        }
        private void ClearSearch()
        {
            this.searchTrans.Text = "";
            this.searchReference.Text = "";
            Filter();

        }
    }
}
