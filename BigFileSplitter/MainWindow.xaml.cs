using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BigFileSplitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string SplitFileDefaultFolderName = "SplitFiles";
        private const string DefaultLabelText = "Pick a file to split...";
        private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

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
            pbSplit.Visibility = Visibility.Hidden;
            pbSplit.Value = 0;

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

                pbSplit.Visibility = Visibility.Visible;
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

        private Microsoft.Win32.OpenFileDialog CreateOpenFileDialog()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.CheckFileExists = true;
            dlg.CheckPathExists = true;
            dlg.Multiselect = false;
            dlg.Title = "Find big file!";

            return dlg;
        }

        private void btnSplit_Click(object sender, RoutedEventArgs e)
        {
            var selectedChunk = ((ComboBoxItem)cboChunks.SelectedItem).Content.ToString();
            int chunkSize = int.Parse(selectedChunk) * 1024 * 1024;
            const int BUFFER_SIZE = 20 * 1024;
            byte[] buffer = new byte[BUFFER_SIZE];

            SplitStatusMessage status = SplitStatusMessage.Success;

            try
            {
                btnSplit.IsEnabled = btnBrowse.IsEnabled = false;
                btnCancel.IsEnabled = true;

                CancellationToken token = cancelTokenSource.Token;

                Task splitTask = Task.Factory.StartNew(() => 
                {
                    token.ThrowIfCancellationRequested();

                    var outputDirectory = System.IO.Path.Combine(OutputDirectory.FullName, SplitFileDefaultFolderName);
                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    using (Stream input = File.OpenRead(FileNameAndPath))
                    {
                        int index = 1;
                        while (input.Position < input.Length)
                        {
                            using (Stream output = File.Create($"{outputDirectory}\\{System.IO.Path.GetFileNameWithoutExtension(FileName)}_Split{index}{FileExtension}"))
                            {
                                int remaining = chunkSize, bytesRead;
                                while (remaining > 0 && (bytesRead = input.Read(buffer, 0, Math.Min(remaining, BUFFER_SIZE))) > 0)
                                {
                                    output.WriteAsync(buffer, 0, bytesRead);
                                    remaining -= bytesRead;
                                }
                            }

                            UpdateProgressBar(input.Position, input.Length);
                            index++;
                            Thread.Sleep(500);
                        }                        
                    }

                }, token);                             
                
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is OperationCanceledException)
                    status = SplitStatusMessage.Cancelled;
                else
                    status = SplitStatusMessage.UnknownError;
            }            
            catch(UnauthorizedAccessException)
            {
                status = SplitStatusMessage.UnauthorizedAccessError;
            }
            catch(PathTooLongException)
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
            finally
            {
                ShowMessage(status);       
                cancelTokenSource = new CancellationTokenSource();
                
                btnSplit.IsEnabled = btnBrowse.IsEnabled = true;
                btnCancel.IsEnabled = false;
            }           
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

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
        }

        private void UpdateProgressBar(long currentWork, long totalWork)
        {
            var progress = (currentWork == totalWork) ? 100 : ((currentWork/totalWork) * 100);
            pbSplit.Dispatcher.Invoke(() => pbSplit.Value = progress);
        }
    }
}
