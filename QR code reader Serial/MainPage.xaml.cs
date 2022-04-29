using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace QR_code_reader_Serial
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MainPageViewMode ViewModel = new MainPageViewMode();
        DataReader dr;

        public MainPage()
        {
            this.InitializeComponent();
            GetPorts();
        }

        private async void GetPorts()
        {
            ViewModel.Reload = Visibility.Visible;
            if(ViewModel.SerialDevices != null)
            {
                foreach(var device in ViewModel.SerialDevices)
                {
                    device.device.Dispose();
                }
                ViewModel.SerialDevices.Clear();
            }
            
            DeviceInformationCollection serialDeviceInfos = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());

            foreach (DeviceInformation serialDeviceInfo in serialDeviceInfos)
            {
                try
                {
                    var com = await SerialDevice.FromIdAsync(serialDeviceInfo.Id);
                    if (com != null)
                    {
                        var item = new SerialDeviceItem();
                        item.PortName = com.PortName;
                        item.DeviceName = serialDeviceInfo.Name;
                        item.device = com;
                        ViewModel.SerialDevices.Add(item);
                    }
                }
                catch (Exception)
                {
                    // Couldn't instantiate the device
                }

            }
            ViewModel.Reload = Visibility.Collapsed;
        }


        public string ReadBuffer = "";
        private async void ReadStanby()
        {
            try
            {
                uint bytesRead = await dr.LoadAsync(1);
                var str = dr.ReadString(bytesRead);
                switch (str)
                {
                    case "\n":
                        break;
                    case "\r":
                        ViewModel.ReadCode = ReadBuffer;
                        var readcode = new ReadQRCode(ReadBuffer);
                        ViewModel.ReadItems.Add(readcode);
                        ReadBuffer = string.Empty;
                        break;
                    default:
                        ReadBuffer += str;
                        break;
                }
                ReadStanby();
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                dr.Dispose();
                ViewModel.IsSerialOpen = false;
            }
        }

        private async void OpenSerial()
        {
            DeviceInformationCollection serialDeviceInfos = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());

            foreach (DeviceInformation serialDeviceInfo in serialDeviceInfos)
            {
                try
                {
                    if (ViewModel.SelectedDevice == null)
                    {
                        return;
                    }
                    ViewModel.IsSerialOpen = true;
                    dr = new DataReader(ViewModel.SelectedDevice.device.InputStream);
                    ReadStanby();
                    return;
                }
                catch (Exception)
                {
                    // Couldn't instantiate the device
                }

            }
            ViewModel.IsSerialOpen = false;
        }

        private void Button_Reload_Click(object sender,RoutedEventArgs e)
        {
            GetPorts();
        }

        private async void Button_Save_Click(object sender ,RoutedEventArgs e)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("QRCodeList", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "QRCode";

            var message = string.Empty;
            StorageFile file = await savePicker.PickSaveFileAsync();
            if(file != null)
            {
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                var SaveData = string.Empty;
                foreach(var item in ViewModel.ReadItems)
                {
                    SaveData += item.ReadString + "\n";
                }
                await Windows.Storage.FileIO.WriteTextAsync(file, SaveData);
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    
                    message = "File " + file.Name + " was saved.";
                }
                else
                {
                    message = "File " + file.Name + " couldn't be saved.";
                }
            }
            else
            {
                message = "Operation cancelled.";
            }
            await new MessageDialog(message).ShowAsync();

        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OpenSerial();
        }
    }

    public class MainPageViewMode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly PropertyChangedEventArgs SerialDevicesPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(SerialDevices));
        private ObservableCollection<SerialDeviceItem> serialDevices = new ObservableCollection<SerialDeviceItem>();
        public ObservableCollection<SerialDeviceItem> SerialDevices
        {
            get { return this.serialDevices; }
            set
            {
                if (this.serialDevices == value) { return; }
                this.serialDevices = value;
                this.PropertyChanged?.Invoke(this, SerialDevicesPropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs ReadItemsPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(ReadItems));
        private ObservableCollection<ReadQRCode> readItems = new ObservableCollection<ReadQRCode>();
        public ObservableCollection<ReadQRCode> ReadItems
        {
            get { return this.readItems; }
            set
            {
                if (this.readItems == value) { return; }
                this.readItems = value;
                this.PropertyChanged?.Invoke(this, ReadItemsPropertyChangedEventArgs);
            }
        }


        private static readonly PropertyChangedEventArgs SelectedDevicePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(SelectedDevice));
        private SerialDeviceItem selectedDevice;
        public SerialDeviceItem SelectedDevice
        {
            get { return this.selectedDevice; }
            set
            {
                if (this.selectedDevice == value) { return; }
                this.selectedDevice = value;
                this.PropertyChanged?.Invoke(this, SelectedDevicePropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs ReloadPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(Reload));
        private Visibility reload = Visibility.Visible;
        public Visibility Reload
        {
            get { return this.reload; }
            set
            {
                if (this.reload == value) { return; }
                this.reload = value;
                this.PropertyChanged?.Invoke(this, ReloadPropertyChangedEventArgs);
            }
        }


        private static readonly PropertyChangedEventArgs ReadCodePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(ReadCode));
        private string readCode;
        public string ReadCode
        {
            get { return this.readCode; }
            set
            {
                if (this.readCode == value) { return; }
                this.readCode = value;
                this.PropertyChanged?.Invoke(this, ReadCodePropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs IsSerialOpenPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(IsSerialOpen));
        private bool isSerialOpen = false;
        public bool IsSerialOpen
        {
            get { return this.isSerialOpen; }
            set
            {
                if (this.isSerialOpen == value) { return; }
                this.isSerialOpen = value;
                this.PropertyChanged?.Invoke(this, IsSerialOpenPropertyChangedEventArgs);
            }
        }

        public MainPageViewMode()
        {
            ReadItems.CollectionChanged += ReadItems_CollectionChanged;
        }

        private void ReadItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (ReadQRCode item in e.NewItems)
                    item.PropertyChanged += DetailPropertyChanged;
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                foreach (ReadQRCode item in e.OldItems)
                    item.PropertyChanged -= DetailPropertyChanged;
                foreach (ReadQRCode item in e.NewItems)
                    item.PropertyChanged += DetailPropertyChanged;
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (ReadQRCode item in e.OldItems)
                    item.PropertyChanged -= DetailPropertyChanged;
            }
        }

        private void DetailPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var ReadItem = sender as ReadQRCode;
            if (ReadItem.IsRemove)
                ReadItems.Remove(ReadItem);
        }
    }

    public class SerialDeviceItem
    {
        public string PortName { get; set; }
        public string DeviceName { get; set; }
        public SerialDevice device { get; set; }
    }


    public class ReadQRCode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string ReadString { get; set; }

        private static readonly PropertyChangedEventArgs IsRemovePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(IsRemove));
        private bool isRemove;
        public bool IsRemove
        {
            get { return this.isRemove; }
            set
            {
                if (this.isRemove == value) { return; }
                this.isRemove = value;
                this.PropertyChanged?.Invoke(this, IsRemovePropertyChangedEventArgs);
            }
        }


        public ReadQRCode(string ReadData)
        {
            ReadString = ReadData;
        }

        public void Button_Remove_Click(object sender ,RoutedEventArgs e)
        {
            IsRemove = true;
        }

    }

}
