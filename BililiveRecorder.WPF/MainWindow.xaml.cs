using Autofac;
using BililiveRecorder.Core;
using BililiveRecorder.FlvProcessor;
using CommandLine;
using Hardcodet.Wpf.TaskbarNotification;
using NLog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BililiveRecorder.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const int MAX_LOG_ROW = 50;
        private const string LAST_WORK_DIR_FILE = "lastworkdir";

        private IContainer Container { get; set; }
        private ILifetimeScope RootScope { get; set; }

        public IRecorder Recorder { get; set; }
        public ObservableCollection<string> Logs { get; set; } =
            new ObservableCollection<string>()
            {
                "官方版本：https://rec.danmuji.org",
                "======[Bililive Recorder - Unofficial]======",
                "Microstorm Studio - 一个全凭兴趣的技术向小组",
                "--------------------------------------------",
                "这个版本为[非官方版本]，增加了一些实验性功能",
                "仅用于测试用途，我们不对软件造成的损失负责",
                "当前生成的数据文件版本：v" + DanmakuRecorder.DataFileVersion,
                "向下兼容最老数据文件版本：v" + DanmakuRecorder.DataFileDownSupport,
                "更老版本的数据文件不再支持",
                "",
                "Unofficial作者联系方式：",
                "QQ:1250542735",
                "邮箱: dengbw01@outlook.com",
                "--------------------------------------------",
                "!!! 弹幕录制默认关闭，请到设置中查看 !!!",
            };

        public static void AddLog(string message) => _AddLog?.Invoke(message);
        private static Action<string> _AddLog;

        public MainWindow()
        {

            _AddLog = (message) =>
                Log.Dispatcher.BeginInvoke(
                    DispatcherPriority.DataBind,
                    new Action(() => { Logs.Add(message); while (Logs.Count > MAX_LOG_ROW) { Logs.RemoveAt(0); } })
                    );

            var builder = new ContainerBuilder();
            builder.RegisterModule<FlvProcessorModule>();
            builder.RegisterModule<CoreModule>();
            Container = builder.Build();
            RootScope = Container.BeginLifetimeScope("recorder_root");

            Recorder = RootScope.Resolve<IRecorder>();

            InitializeComponent();

            DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Title += " " + BuildInfo.Version + " " + BuildInfo.HeadShaShort;  //DanmakuRecorder.DataFileVersion
            Title += " v" + DanmakuRecorder.DataFileVersion+" 测试版本，请勿用于生产环境";
            bool skip_ui = false;
            string workdir = string.Empty;

            CommandLineOption commandLineOption = null;
            Parser.Default
                .ParseArguments<CommandLineOption>(Environment.GetCommandLineArgs())
                .WithParsed(x => commandLineOption = x);

            if (commandLineOption?.WorkDirectory != null)
            {
                skip_ui = true;
                workdir = commandLineOption.WorkDirectory;
            }

            if (!skip_ui)
            {
                try
                {
                    workdir = File.ReadAllText(LAST_WORK_DIR_FILE);
                }
                catch (Exception) { }
                var wdw = new WorkDirectoryWindow()
                {
                    Owner = this,
                    WorkPath = workdir,
                };

                if (wdw.ShowDialog() == true)
                {
                    workdir = wdw.WorkPath;
                }
                else
                {
                    Environment.Exit(-1);
                    return;
                }
            }

            if (!Recorder.Initialize(workdir))
            {
                if (!skip_ui)
                {
                    MessageBox.Show("初始化错误", "录播姬", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Environment.Exit(-2);
                return;
            }

            NotifyIcon.Visibility = Visibility.Visible;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (new TimedMessageBox
            {
                Title = "关闭录播姬？",
                Message = "确定要关闭录播姬吗？",
                CountDown = 10,
                Left = Left,
                Top = Top
            }.ShowDialog() == true)
            {
                _AddLog = null;
                Recorder.Shutdown();
                try
                {
                    File.WriteAllText(LAST_WORK_DIR_FILE, Recorder.Config.WorkDirectory);
                }
                catch (Exception) { }
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void TextBlock_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is TextBlock textBlock)
                {
                    Clipboard.SetText(textBlock.Text);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 触发回放剪辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Clip_Click(object sender, RoutedEventArgs e)
        {
            var rr = _GetSenderAsRecordedRoom(sender);
            if (rr == null)
            {
                return;
            }

            Task.Run(() => rr.Clip());
        }

        /// <summary>
        /// 启用自动录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnableAutoRec(object sender, RoutedEventArgs e)
        {
            var rr = _GetSenderAsRecordedRoom(sender);
            if (rr == null)
            {
                return;
            }

            Task.Run(() =>
            {
                rr.Start();
                Recorder.SaveConfigToFile();
            });
        }

        /// <summary>
        /// 禁用自动录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisableAutoRec(object sender, RoutedEventArgs e)
        {
            var rr = _GetSenderAsRecordedRoom(sender);
            if (rr == null)
            {
                return;
            }

            Task.Run(() =>
            {
                rr.Stop();
                Recorder.SaveConfigToFile();
            });
        }

        /// <summary>
        /// 手动触发尝试录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TriggerRec(object sender, RoutedEventArgs e)
        {
            var rr = _GetSenderAsRecordedRoom(sender);
            if (rr == null)
            {
                return;
            }

            Task.Run(() => rr.StartRecord());
        }

        /// <summary>
        /// 切断当前录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CutRec(object sender, RoutedEventArgs e)
        {
            var rr = _GetSenderAsRecordedRoom(sender);
            if (rr == null)
            {
                return;
            }

            Task.Run(() => rr.StopRecord());
        }

        /// <summary>
        /// 删除当前房间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveRecRoom(object sender, RoutedEventArgs e)
        {
            var rr = (IRecordedRoom)((DataGrid)((ContextMenu)((MenuItem)sender)?.Parent)?.PlacementTarget)?.SelectedItem;
            if (rr == null)
            {
                return;
            }

            Recorder.RemoveRoom(rr);
            Recorder.SaveConfigToFile();
        }

        private void RefreshRoomInfo(object sender, RoutedEventArgs e)
        {
            var rr = (IRecordedRoom)((DataGrid)((ContextMenu)((MenuItem)sender)?.Parent)?.PlacementTarget)?.SelectedItem;
            if (rr == null)
            {
                return;
            }

            rr.RefreshRoomInfo();
        }

        /// <summary>
        /// 全部直播间启用自动录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void EnableAllAutoRec(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(Recorder.ToList().Select(rr => Task.Run(() => rr.Start())));
            Recorder.SaveConfigToFile();
        }

        /// <summary>
        /// 全部直播间禁用自动录制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DisableAllAutoRec(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(Recorder.ToList().Select(rr => Task.Run(() => rr.Stop())));
            Recorder.SaveConfigToFile();
        }

        /// <summary>
        /// 添加直播间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddRoomidButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(AddRoomidTextBox.Text, out int roomid))
            {
                if (roomid > 0)
                {
                    Recorder.AddRoom(roomid);
                    Recorder.SaveConfigToFile();
                }
                else
                {
                    logger.Info("房间号是大于0的数字！");
                }
            }
            else
            {
                logger.Info("房间号是数字！");
            }
            AddRoomidTextBox.Text = string.Empty;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void ShowSettingsWindow()
        {
            var sw = new SettingsWindow(this, Recorder.Config);
            if (sw.ShowDialog() == true)
            {
                sw.Config.CopyPropertiesTo(Recorder.Config);
            }
            Recorder.SaveConfigToFile();
        }

        private IRecordedRoom _GetSenderAsRecordedRoom(object sender) => (sender as Button)?.DataContext as IRecordedRoom;

        private void Taskbar_Quit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                NotifyIcon.ShowBalloonTip("B站录播姬", "录播姬已最小化到托盘，左键单击图标恢复界面。", BalloonIcon.Info);
            }
        }

        private void Taskbar_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Topmost ^= true;
            Topmost ^= true;
            Focus();
        }
    }
}
