using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;

namespace DocService
{
    [RunInstaller(true)]
    public partial class DocEditingService : ServiceBase
    {
        private readonly HttpClient _httpClient = new HttpClient();

        private string DownloadFolderPath = string.Empty;
        private string DownloadedFileName = string.Empty;
        private string DomnainAndUserName = string.Empty;

        private FileSystemWatcher _fileWatcher;
        private List<string> openDocuments = new List<string>();


        public DocEditingService()
        {
            InitializeComponent();
        }

        #region On Service start
        protected override void OnStart(string[] args)
        {

            try
            {
                LogMessage("Proccess Start Sccuessfully", EventLogEntryType.Information);
                _fileWatcher = new FileSystemWatcher();
                GetDownloadAndUserName();

                _fileWatcher = new FileSystemWatcher(DownloadFolderPath);
                _fileWatcher.Renamed += OnRenamed;
                _fileWatcher.EnableRaisingEvents = true;

            }
            catch (Exception ex)
            {
                LogMessage($"Error downloading document with ID 0: {ex.ToString()}", EventLogEntryType.Error);

            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            try
            {
                FileInfo file = new FileInfo(e.FullPath);

                if (!file.Name.Contains("crdownload") || !file.Name.Contains(".tmp"))
                {
                    LogMessage($"Document is already open", EventLogEntryType.Information);

                    if (!IsDocumentOpen(file.Name))
                    {
                        if (file.Name.Contains("LFH;"))
                        {
                            Thread.Sleep(5000);
                            DownloadedFileName = file.Name;
                            ProcessRequest();
                            LogMessage($"Document is already open", EventLogEntryType.Information);

                        }
                    }
                    else
                    {
                        LogMessage($"Document is already open", EventLogEntryType.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error {ex.Message}", EventLogEntryType.Error);
            }
        }


        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                Thread.Sleep(3000);
                ProcessRequest();
            }
        }

        protected override void OnStop()
        {
            try
            {
                CleanupResources();
                LogMessage("Process Stopped Successfully", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Error in OnStop: {ex.ToString()}", EventLogEntryType.Error);
            }
        }

        private void CleanupResources()
        {
            try
            {
                // Release resources and cleanup
                if (_fileWatcher != null)
                {
                    _fileWatcher.Dispose();
                }


                // Remove the task scheduler entry if it exists
                using (TaskService taskService = new TaskService())
                {
                    taskService.RootFolder.DeleteTask("File Editing", false);
                }

            }
            catch (Exception ex)
            {
                LogMessage($"Error in CleanupResources: {ex.ToString()}", EventLogEntryType.Error);
            }
        }
        #endregion

        #region Main File Related Things
        private void ProcessRequest()
        {
            try
            {
                if (!string.IsNullOrEmpty(DownloadedFileName))
                {
                    string fullPath = string.Empty;

                    try
                    {

                        fullPath = Path.Combine(DownloadFolderPath, DownloadedFileName);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                int isProcessStart = CreateHighPriorityTask(fullPath);
                                if (isProcessStart != 0)
                                {
                                    // Add the document to the list of open documents.
                                    openDocuments.Add(DownloadedFileName);
                                    WaitForDocumentClose(fullPath, isProcessStart);
                                    SaveDocument(fullPath, DownloadedFileName);

                                }

                            }
                            catch (Exception ex)
                            {
                                LogMessage($"An error occurred: {ex.Message}", EventLogEntryType.Error);
                            }
                            finally
                            {
                                // Remove the document from the list of open documents when it's closed.
                                lock (openDocuments)
                                {
                                    openDocuments.Remove(DownloadedFileName);
                                }
                            }

                        }
                        else
                        {
                            LogMessage($"File not found: {fullPath}", EventLogEntryType.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error in Opening File {ex.Message}", EventLogEntryType.Error);
                    }

                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error in Opening File {ex.Message}", EventLogEntryType.Error);
            }
        }

        private int CreateHighPriorityTask(string fullPath)
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    TaskDefinition taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Document Editing Process";
                    taskDefinition.RegistrationInfo.Author = "SYSTEM";

                    taskDefinition.Principal.DisplayName = "Document Edit Service";
                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                    taskDefinition.Principal.GroupId = "User";
                    taskDefinition.Principal.UserId = DomnainAndUserName;
                    taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

                    taskDefinition.Settings.AllowDemandStart = true;
                    taskDefinition.Settings.AllowHardTerminate = true;
                    taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                    taskDefinition.Settings.DisallowStartOnRemoteAppSession = true;
                    taskDefinition.Settings.Hidden = false;
                    taskDefinition.Settings.RestartCount = 0;
                    taskDefinition.Settings.RunOnlyIfIdle = false;
                    taskDefinition.Settings.RunOnlyIfNetworkAvailable = false;
                    taskDefinition.Settings.StartWhenAvailable = true;
                    taskDefinition.Settings.StopIfGoingOnBatteries = false;
                    taskDefinition.Settings.Volatile = false;

                    taskDefinition.Settings.WakeToRun = false;
                    taskDefinition.Actions.Add(new ExecAction(fullPath));

                    const string taskName = "File Editing";
                    taskService.RootFolder.RegisterTaskDefinition(taskName, taskDefinition);


                    var task = taskService.FindTask(taskName).Run();
                    int i = (int)task.EnginePID;
                    return i;

                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error Which Creating Editing Task:{ex.Message}", EventLogEntryType.Error);
                return 0;
            }
        }

        private bool IsWordDefaultForExtension(string extension)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(extension))
            {
                if (key != null)
                {
                    object defaultValue = key.GetValue(null);
                    return defaultValue != null && defaultValue.ToString().ToLower().Contains("word");
                }
            }

            LogMessage("its not working.", EventLogEntryType.Error);

            return false;

        }

        static string RemoveRepeatNumbers(string fileName)
        {
            // Use a regular expression to find and remove "(n)" pattern from the filename
            // where n is a number enclosed in parentheses.
            string cleanedFileName = Regex.Replace(fileName, @"\s+\(\d+\)", string.Empty);

            return cleanedFileName;
        }

        #endregion

        #region File Watcher things

        private void WaitForDocumentClose(string documentPath, int processId)
        {
            try
            {
                LogMessage("Wait Doc Proccess Start Sccuessfully", EventLogEntryType.Information);

                bool IsProcessExit = true;
                while (IsProcessExit)
                {
                    Thread.Sleep(5000);
                    try
                    {
                        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}"))
                        {
                            ManagementObjectCollection processes = searcher.Get();
                            if (processes.Count == 0)
                            {
                                IsProcessExit = false;
                                LogMessage("Document closed successfully", EventLogEntryType.Information);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error while checking if the document is closed: {ex.Message}", EventLogEntryType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.ToString()}", EventLogEntryType.Error);

            }
        }

        #endregion

        #region SQL Queries
        private async void SaveDocument(string documentPath, string fileName)
        {
            try
            {
                LogMessage("Save Process started", EventLogEntryType.Information);

                if (File.Exists(documentPath))
                {
                    DownloadedFileName = DownloadedFileName.Replace("LFH;", "");
                    DownloadedFileName = RemoveRepeatNumbers(DownloadedFileName);
                    string[] charArray = DownloadedFileName.Split('_');
                    string actualFileName = string.Empty;
                    try
                    {
                        actualFileName = charArray[charArray.Length - 1].ToString();

                    }
                    catch (Exception ex)
                    {
                        LogMessage("InValide File Name", EventLogEntryType.Error);
                    }

                    DownloadedFileName = DownloadedFileName.Replace("_", "/");


                    LogMessage("API Process started", EventLogEntryType.Information);

                    using (var formData = new MultipartFormDataContent())
                    {
                        using (FileStream fileStream = new FileStream(documentPath, FileMode.Open))
                        {
                            formData.Add(new StreamContent(fileStream), "file", actualFileName);
                            formData.Add(new StringContent("admin"), "username");
                            formData.Add(new StringContent("admin"), "password");
                            formData.Add(new StringContent(DownloadedFileName), "file_path");

                            HttpResponseMessage response = await _httpClient.PostAsync(BaseURL.UploadFile, formData);
                            string responseBody = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                LogMessage(responseBody, EventLogEntryType.Information);
                                LogMessage("File Edit Sccuessfully", EventLogEntryType.Information);
                                File.Delete(documentPath); // If the file upload sccessfully than delete the file if there is any error occures it will repon again
                            }
                            else
                            {
                                LogMessage(responseBody, EventLogEntryType.Error);
                                //File.Delete(documentPath);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, EventLogEntryType.Error);


            }
        }

        #endregion

        #region UserName and Download Folder Property
        private void GetDownloadAndUserName()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                ManagementObjectCollection collection = searcher.Get();
                if (collection != null)
                {
                    DomnainAndUserName = (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
                    var resultSet = DomnainAndUserName.Split('\\');
                    string username = resultSet[1];
                    DownloadFolderPath = string.Format(@"C:\Users\{0}\Downloads", username);

                }
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message, EventLogEntryType.Error);

            }
        }

        // Add a method to check if a document is open.
        private bool IsDocumentOpen(string documentname)
        {
            try
            {
                // Check if the document is in the list of open documents.
                return openDocuments.Contains(documentname);
            }
            catch (Exception ex)
            {
                LogMessage($"Error in IsDocumentOpen: {ex.Message}", EventLogEntryType.Error);
                return false;
            }
        }

        #endregion

        #region Logging
        private void LogMessage(string message, EventLogEntryType entryType)
        {
            EventLog.WriteEntry("DocumentEditingService", message, entryType);
        }
        #endregion
    }
}

