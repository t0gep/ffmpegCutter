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
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;

namespace ffmpegCutter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CheckFfmpegOnStartup();
        }


        // ffmpegのパスをチェックする
        private void CheckFfmpegOnStartup()
        {
            var path = Properties.Settings.Default.FfmpegPath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("ffmpegのパスが設定されていないか、存在しません。設定メニューから設定してください。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ドラッグ中のイベント
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length == 1)
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }


        // ドロップ後のイベント
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length != 1)
            {
                MessageBox.Show("ファイルは1つだけドロップしてください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SetInputVideo(files[0]);
        }


        // メニュー/設定/ffmpegのパスを設定
        private void Menu_SetFfmpegPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "ffmpeg.exeを選択してください",
                Filter = "ffmpeg.exe|ffmpeg.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                if (!File.Exists(dialog.FileName))
                {
                    MessageBox.Show("選択されたファイルが存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Properties.Settings.Default.FfmpegPath = dialog.FileName;
                Properties.Settings.Default.Save();

                MessageBox.Show("ffmpegのパスを設定しました。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);

            }
        }

        // メニュー/ファイル/終了
        private void Menu_File_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        // 実行ボタンのクリックイベント
        private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteButton.IsEnabled = false;

            try
            {
                if (!File.Exists(Properties.Settings.Default.FfmpegPath))
                {
                    MessageBox.Show("ffmpegのパスが設定されていないか、存在しません。設定メニューから設定してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_inputVideoPath == null || !File.Exists(_inputVideoPath))
                {
                    MessageBox.Show("入力動画ファイルが設定されていないか、存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!TryParseStartTime(out var startTime))
                {
                    MessageBox.Show("開始時刻は hhmmss 形式で入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var duration = DurationBox.Value ?? 0;
                if (duration <= 0)
                {
                    MessageBox.Show("切り抜き時間は正の数で入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var outputPath = CreateOutputPath(_inputVideoPath, startTime);

                var psi = new ProcessStartInfo
                {
                    FileName = Properties.Settings.Default.FfmpegPath,
                    UseShellExecute = false
                };

                psi.ArgumentList.Add("-ss");
                psi.ArgumentList.Add(startTime.ToString(@"hh\:mm\:ss"));
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(_inputVideoPath);
                psi.ArgumentList.Add("-t");
                psi.ArgumentList.Add(duration.ToString());
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("copy");
                psi.ArgumentList.Add(outputPath);

                var process = Process.Start(psi);
                if (process == null)
                {
                    MessageBox.Show("ffmpegのプロセスを開始できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await process.WaitForExitAsync();

            }
            finally
            {
                ExecuteButton.IsEnabled = true;
            }
        }

        // ここまで、イベントハンドラなど

        // 以下、補助メソッドなど

        private string? _inputVideoPath;

        // 開始時間のパース
        private bool TryParseStartTime(out TimeSpan time)
        {
            time = TimeSpan.Zero;

            var text = StartTimeBox.Text;

            if (text.Length != 6 || !int.TryParse(text, out _))
            {
                return false;
            }

            var h = int.Parse(text[..2]);
            var m = int.Parse(text[2..4]);
            var s = int.Parse(text[4..6]);

            try
            {
                time = new TimeSpan(h, m, s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 出力ファイル名パスの作成
        private string CreateOutputPath(string inputPath, TimeSpan startTime)
        {
            var dir = System.IO.Path.GetDirectoryName(inputPath)!;
            var name = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            var ext = System.IO.Path.GetExtension(inputPath);

            var timeText = startTime.ToString(@"hh\-mm\-ss");
            var basePath = System.IO.Path.Combine(dir, $"{name}_{timeText}{ext}");


            if (!File.Exists(basePath))
            {
                return basePath;
            }

            int i = 1;
            while (true)
            {
                var candidate = System.IO.Path.Combine(dir, $"{name}_{timeText}({i}){ext}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
                i++;
            }
        }

        // 入力動画ファイルの設定
        private void SetInputVideo(string path)
        {
            _inputVideoPath = path;
            InputFileBox.Text = path;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}