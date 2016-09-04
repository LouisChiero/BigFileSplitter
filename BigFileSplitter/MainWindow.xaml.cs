using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BigFileSplitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string SplitFileDefaultFolderName = "SplitFiles";
        private const string DefaultLabelText = "Pick a file to split...";
        private CancellationTokenSource cancelTokenSource;
        private const int BUFFER_SIZE = 20 * 1024;

        public MainWindow()
        {
            InitializeComponent();
//#if DEBUG
//            pbSplit.Visibility = Visibility.Visible;
//#endif
            lblBigFile.Content = DefaultLabelText;
            Closing += (o, args) => 
            {
                if (cancelTokenSource != null)
                    cancelTokenSource.Dispose();
            };          
        }    

        private string FileNameAndPath { get; set; }
        private string FileName { get; set; }
        private string FileExtension { get; set; }
        private DirectoryInfo OutputDirectory { get; set; }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            HideProgressBar();
            HideSpinner();

            var openFileDialog = CreateOpenFileDialog();
            var openFileDialogResult = openFileDialog.ShowDialog(this);

            if (openFileDialogResult.HasValue && openFileDialogResult.Value)
            {
                FileNameAndPath = openFileDialog.FileName;
                FileInfo file = new FileInfo(FileNameAndPath);
                FileName = file.Name;
                FileExtension = file.Extension;               
                OutputDirectory = file.Directory;

                lblBigFile.Content = $"Splitting {file.Name}";
                lblBigFile.ToolTip = lblBigFile.Content;

                btnSplit.IsEnabled = true;

                ResetProgressBar();
            }
            else
            {
                FileNameAndPath = FileName = FileExtension = string.Empty;
                OutputDirectory = null;               

                lblBigFile.Content = DefaultLabelText;
                btnSplit.IsEnabled = btnCancel.IsEnabled = false;
            }

            openFileDialog.Reset();
        }        

        private async void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            int mbs = int.Parse(((ComboBoxItem)cboChunks.SelectedItem).Content.ToString());
            btnSplit.IsEnabled = btnBrowse.IsEnabled = false;
            btnCancel.IsEnabled = true;
            ResetProgressBar();
            ShowSpinner();

            cancelTokenSource = new CancellationTokenSource();
            CancellationToken token = cancelTokenSource.Token;            

            // split file into chunks
            var splitTaskResult = await Task.Run(() => SplitFileAsync(mbs, token), token);

            HideSpinner();
            ShowMessage(splitTaskResult);

            btnSplit.IsEnabled = btnBrowse.IsEnabled = true;
            btnCancel.IsEnabled = false;
            if (splitTaskResult != SplitStatusMessage.Success)
                HideProgressBar();            
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
        }

        private Microsoft.Win32.OpenFileDialog CreateOpenFileDialog()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            dlg.Title = "Find big file!";

            return dlg;
        }

        private async Task<SplitStatusMessage> SplitFileAsync(int mbSize, CancellationToken token)
        {            
            int chunkSize = mbSize * 1024 * 1024;            
            byte[] buffer = new byte[BUFFER_SIZE];            
            var status = SplitStatusMessage.Success;
            
            try
            {
                var outputDirectory = Path.Combine(OutputDirectory.FullName, SplitFileDefaultFolderName);
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                using (Stream input = File.OpenRead(FileNameAndPath))
                {
                    int index = 1;
                    while (input.Position < input.Length)
                    {
                        using (Stream output = File.Create($"{outputDirectory}\\{Path.GetFileNameWithoutExtension(FileName)}_Split{index}{FileExtension}"))
                        {
                            if (token.IsCancellationRequested)
                                token.ThrowIfCancellationRequested();

                            int remaining = chunkSize, bytesRead;
                            while (remaining > 0 && (bytesRead = input.Read(buffer, 0, Math.Min(remaining, BUFFER_SIZE))) > 0)
                            {
                                await output.WriteAsync(buffer, 0, bytesRead);
                                remaining -= bytesRead;
                                UpdateProgressBar(input.Position, input.Length);
                            }
                        }
                                                
                        index++;                            
                    }
                }
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is OperationCanceledException)
                    status = SplitStatusMessage.Cancelled;
                else
                    status = SplitStatusMessage.UnknownError;
            }
            catch (OperationCanceledException)
            {
                status = SplitStatusMessage.Cancelled;
            }
            catch (UnauthorizedAccessException)
            {
                status = SplitStatusMessage.UnauthorizedAccessError;
            }
            catch (PathTooLongException)
            {
                status = SplitStatusMessage.PathTooLongError;
            }
            catch (DirectoryNotFoundException)
            {
                status = SplitStatusMessage.DirectoryNotFoundError;
            }
            catch (NotSupportedException)
            {
                status = SplitStatusMessage.NotSupportedError;
            }
            catch (IOException)
            {
                status = SplitStatusMessage.IOError;
            }
            catch (Exception)
            {                   
                status = SplitStatusMessage.UnknownError;
            }              

            return status;          
        }

        private void ShowMessage(SplitStatusMessage status)
        {
            bool success = status == SplitStatusMessage.Success;
            string messageText = string.Empty;
            switch(status)
            {
                case SplitStatusMessage.Cancelled:
                    {
                        messageText = "Split canceled by user.";
                        break;
                    }
                case SplitStatusMessage.DirectoryNotFoundError:
                    {
                        messageText = "Split failed due to a directory not found error.";
                        break;
                    }
                case SplitStatusMessage.IOError:
                    {
                        messageText = "Split failed due to an input/output error.";
                        break;
                    }
                case SplitStatusMessage.NotSupportedError:
                    {
                        messageText = "Split failed due to a not supported error.";
                        break;
                    }                
                case SplitStatusMessage.PathTooLongError:
                    {
                        messageText = "Split failed due to a path too long error.";
                        break;
                    }
                case SplitStatusMessage.Success:
                    {
                        messageText = "Split was successful!";
                        break;
                    }
                case SplitStatusMessage.UnauthorizedAccessError:
                    {
                        messageText = "Split failed due to an unauthorized access error.";
                        break;
                    }
                case SplitStatusMessage.UnknownError:
                    {
                        messageText = "Split failed due to an unknown error.";
                        break;
                    }
            }

            MessageBox.Show(
                messageText, 
                "Splitter Status", 
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }        

        private void UpdateProgressBar(long currentWork, long totalWork)
        {            
            var progress = (currentWork == totalWork) ? 100 : ((100 * currentWork/totalWork));
            pbSplit.Dispatcher.Invoke(() => pbSplit.Value = progress);
        }

        private void HideProgressBar()
        {
            pbSplit.Visibility = Visibility.Hidden;
            pbSplit.Value = 0;
        }

        private void ResetProgressBar()
        {
            pbSplit.Value = 0;
            pbSplit.Visibility = Visibility.Visible;            
        }

        private void HideSpinner()
        {
            imgSpinner.Visibility = Visibility.Hidden;
            imgSpinner.RenderTransform = null;
        }

        private void ShowSpinner()
        {
            var animation = new DoubleAnimation(360, 0, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            var rotate = new RotateTransform();
            imgSpinner.RenderTransform = rotate;
            rotate.BeginAnimation(RotateTransform.AngleProperty, animation);

            imgSpinner.Visibility = Visibility.Visible;           
        }        
    }
}
