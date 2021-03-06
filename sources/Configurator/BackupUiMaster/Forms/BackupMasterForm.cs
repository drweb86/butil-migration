﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using BULocalization;
using BUtil.Core.PL;
using BUtil.Core.FileSystem;
using BUtil.Core;
using BUtil.Core.Options;
using BUtil.Core.Misc;
using BUtil.Core.Logs;
using BUtil.BackupUiMaster.Controls;
using BUtil.Core.Storages;

namespace BUtil.Configurator.BackupUiMaster.Forms
{
	internal delegate void Stub();
	
	internal sealed partial class BackupMasterForm : Form
	{
		#region Types
		
		enum GroupEnum
		{
			CompressionOfFolders = 0,
			CompressionOfFiles = 1,
			Storages = 2,
			OtherTasks = 3,
			BeforeBackupChain = 4,
			AfterBackupChain = 5
		}
		
		enum ImagesEnum
		{
			Folder = 0,
			File = 1,
			Ftp = 2,
			Hdd = 3,
			Network = 4,
			CompressIntoAnImage = 5,
			ProgramInRunBeforeAfterBackupChain = 6
		}
		
		#endregion
		
		#region Constants
		
		const string ImagePacking = "ImagePacking";
		
		#endregion
		
		#region Fields
		
		bool _firstTimeApplicationInTray = true;
		bool _trayModeActivated;
		bool _backupInProgress;
		bool _strongIntentionToCancelBackup;
		readonly BackupUiMaster _controller;
		readonly BackupTask _task;
		BackupProgressUserControl _backupProgressUserControl;
		
		#endregion

        public BackupMasterForm(ProgramOptions options, List<BackupTask> backupTasksChain)
		{
			InitializeComponent();
			
			if (Program.PackageIsBroken || Program.SevenZipIsBroken)
			{
				throw new InvalidOperationException("Tried to perform operation that requires package state is ok.");
			}

			tasksListView.Columns[0].Width = tasksListView.Width - 40;
			processingStateInformationColumnHeader.Width = 0;

            //TODO: please move to controller
            if (backupTasksChain.Count == 0)
            {
                using (var form = new SelectTaskToRunForm(options.BackupTasks))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        backupTasksChain = form.TasksToRun;
                    }
                    else
                    {
                        Environment.Exit(-1);
                    }
                }
            }

            _task = backupTasksChain[0];
            _controller = new BackupUiMaster(_task, options);
            _controller.BackupFinished += OnBackupFinsihed;
            CompressionItemsListViewResize(null, null);
            
            ApplyLocalization(Translation.Current);
		}
		
		void OnBackupFinsihed()
		{
			if (!InvokeRequired)
			{
				_backupInProgress = false;
				cancelButton.Enabled = false; 
				
				if (_backupProgressUserControl != null)
				{
					_backupProgressUserControl.Stop();
				}
				ReturnFromTray();
			}
			else
			{
				Invoke(new Stub(OnBackupFinsihed));
			}
		}
		
		static Color GetResultColor(ProcessingState state)
		{
			switch (state)
			{
				case ProcessingState.FinishedSuccesfully:
					return Color.LightGreen;
				case ProcessingState.FinishedWithErrors:
					return Color.LightCoral;
				case ProcessingState.InProgress:
					return Color.Yellow;
				case ProcessingState.NotStarted:
					throw new InvalidOperationException(state.ToString());
				default:
					throw new NotImplementedException(state.ToString());
			}
		}
		
		void OnNotificationReceived(object sender, EventArgs e)
		{
			if (InvokeRequired)
			{
				Invoke(new EventHandler(OnNotificationReceived), new [] {sender, e});
			}
			else
			{
				if (e == null)
				{
					throw new InvalidOperationException();
				}
				
                
                if (e is PackingNotificationEventArgs)
				{
					var notification = (PackingNotificationEventArgs)e;
					foreach(ListViewItem item in tasksListView.Items)
					{
						if (item.Tag == notification.Item)
						{
							item.SubItems[2].Text = LocalsHelper.ToString(notification.State);
							if (notification.State != ProcessingState.NotStarted)
							{
								item.BackColor =  GetResultColor(notification.State);
							}
						}
					}
				}
				else if (e is CopyingToStorageNotificationEventArgs)
				{
					var notification = (CopyingToStorageNotificationEventArgs)e;
					
					foreach(ListViewItem item in tasksListView.Items)
					{
						if (item.Tag == notification.Storage)
						{
							item.SubItems[2].Text = LocalsHelper.ToString(notification.State);
							if (notification.State != ProcessingState.NotStarted)
							{
								item.BackColor =  GetResultColor(notification.State);
							}
						}
					}
				}
				else if (e is ImagePackingNotificationEventArgs)
				{
					var notification = (ImagePackingNotificationEventArgs)e;
					
					foreach(ListViewItem item in tasksListView.Items)
					{
						if (item.Tag is string)
						if ((string)item.Tag == ImagePacking)
						{
							item.SubItems[2].Text = LocalsHelper.ToString(notification.State);
							if (notification.State != ProcessingState.NotStarted)
							{
								item.BackColor =  GetResultColor(notification.State);
							}
						}
					}
				}
				else if (e is RunProgramBeforeOrAfterBackupEventArgs)
				{
					var notification = (RunProgramBeforeOrAfterBackupEventArgs)e;
					
					foreach(ListViewItem item in tasksListView.Items)
					{
						if (item.Tag is BackupEventTaskInfo)
						if (item.Tag == notification.TaskInfo)
						{
							item.SubItems[2].Text = LocalsHelper.ToString(notification.State);
							if (notification.State != ProcessingState.NotStarted)
							{
								item.BackColor =  GetResultColor(notification.State);
							}
						}
					}
				}
			}
		}
		
		void CloseButtonClick(object sender, EventArgs e)
		{
		    Close();
		}
		
		void StartButtonClick(object sender, EventArgs e)
		{
			// here some logival verification
			_task.FilesFoldersList.Clear();
			_task.Storages.Clear();
			_task.BeforeBackupTasksChain.Clear();
			_task.AfterBackupTasksChain.Clear();
			
			foreach (ListViewItem item in tasksListView.CheckedItems)
			{
				if (item.Tag is CompressionItem)
				{
					_task.FilesFoldersList.Add((CompressionItem) item.Tag);
				}
				else if (item.Tag is StorageBase)
				{
					_task.Storages.Add((StorageBase) item.Tag);
				}
				else if (item.Tag is BackupEventTaskInfo)
				{
					int groupIndex = tasksListView.Groups.IndexOf(item.Group);
					if (groupIndex == (int)GroupEnum.BeforeBackupChain)
					{
						_task.BeforeBackupTasksChain.Add((BackupEventTaskInfo) item.Tag);
					}
					else if (groupIndex == (int)GroupEnum.AfterBackupChain)
					{
						_task.AfterBackupTasksChain.Add((BackupEventTaskInfo) item.Tag);
					}
					else
					{
						throw new NotImplementedException();
					}
				}
			}
			
			if (_task.FilesFoldersList.Count < 1)
			{
				Messages.ShowInformationBox(Translation.Current[569]);
				return;
			}
			
			if (_task.Storages.Count < 1)
			{
				Messages.ShowInformationBox(Translation.Current[570]);
				return;
			}

			// applying settings
			tasksListView.CheckBoxes = false;
			
			// adding internal tasks
			var listItem = new ListViewItem(Translation.Current[577]);
			listItem.ImageIndex = (int)ImagesEnum.CompressIntoAnImage;
           	listItem.SubItems.Add("-");
           	listItem.SubItems.Add(string.Empty);
           	listItem.Group = tasksListView.Groups[(int)GroupEnum.OtherTasks];
           	listItem.Tag = ImagePacking;
           	listItem.Checked = true;
           	tasksListView.Items.Add(listItem);
			
			foreach (ListViewItem item in tasksListView.Items)
			{
				if (!item.Checked)
				{
					item.BackColor = Color.Gray;
					item.SubItems[2].Text = Translation.Current[573];
				}
			}

			processingStateInformationColumnHeader.Width = 154;
			CompressionItemsListViewResize(null, null);

			PowerTask task;
			bool beepWhenCompleted;
			settingsUserControl.GetSettingsFromUi(out task, out beepWhenCompleted);
			_controller.PowerTask = task;
			_controller.HearSoundWhenBackupCompleted = beepWhenCompleted;
			
			// performing changes in UI
			_backupProgressUserControl = new BackupProgressUserControl();
			Controls.Add(_backupProgressUserControl);

			_backupProgressUserControl.ApplyLocalization();
			_backupProgressUserControl.Left = settingsUserControl.Left;
			_backupProgressUserControl.Width = settingsUserControl.Width;
			_backupProgressUserControl.Top = settingsUserControl.Bottom - _backupProgressUserControl.MinimumSize.Height;
			_backupProgressUserControl.Height = _backupProgressUserControl.MinimumSize.Height;
			_backupProgressUserControl.Anchor = settingsUserControl.Anchor;
			tasksListView.Height += settingsUserControl.Height - _backupProgressUserControl.Height;
			settingsUserControl.Hide();
			startButton.Enabled = false;
			cancelButton.Enabled = true;
			
			_trayModeActivated = true;
			SwapToTray(true);
			
			// starting machinery
			_backupInProgress = true;
			_controller.PrepareBackup();
			_controller.BackupClass.NotificationEventHandler += OnNotificationReceived;
			backupBackgroundWorker.RunWorkerAsync();
		}
		
		void ApplyLocalization(Translation translation)
		{
			settingsUserControl.ApplyLocalization();
            toolTip.SetToolTip(startButton, translation[160]);
            closeButton.Text = translation[161];

            taskNameColumnHeader.Text = translation[174];
            informationAboutTaskColumnHeader.Text = translation[542];
            processingStateInformationColumnHeader.Text = translation[547];

            Text = translation[179];
            toolTip.SetToolTip(cancelButton, translation[186]);
            tasksListView.Groups[0].Header = translation[516];
            tasksListView.Groups[1].Header = translation[517];
            tasksListView.Groups[2].Header = translation[568];
            tasksListView.Groups[3].Header = translation[576];
            tasksListView.Groups[(int)GroupEnum.BeforeBackupChain].Header = translation[601]; // before backup event chain
            tasksListView.Groups[5].Header = translation[602];

            notifyIcon.Text = translation[510];
		}
		
		void LoadForm(object sender, EventArgs e)
		{
			// displaying the current settings

            settingsUserControl.SetSettingsToUi(_controller.Options, PowerTask.None, _task, false, ThreadPriority.BelowNormal);

			tasksListView.BeginUpdate();
            ReadOnlyCollection<CompressionItem> items = _controller.ListOfFiles;
            foreach(CompressionItem item in items)
            {
            	var listItem = new ListViewItem(item.Target, item.IsFolder ? (int)ImagesEnum.Folder : (int)ImagesEnum.File);
            	listItem.SubItems.Add(LocalsHelper.ToString(item.CompressionDegree));
            	listItem.SubItems.Add(string.Empty);
            	listItem.Group = item.IsFolder ? tasksListView.Groups[(int)GroupEnum.CompressionOfFolders] : tasksListView.Groups[(int)GroupEnum.CompressionOfFiles];
            	listItem.Tag = item;
            	listItem.Checked = true;
            	tasksListView.Items.Add(listItem);
            }
            
            foreach(StorageBase item in _task.Storages)
            {
            	var listItem = new ListViewItem(item.StorageName);

            	if (item is FtpStorage)
            	{
            		listItem.ImageIndex = (int)ImagesEnum.Ftp;
            	}
            	else if (item is HddStorage)
            	{
            		listItem.ImageIndex = (int)ImagesEnum.Hdd;
            	}
            	else if (item is NetworkStorage)
            	{
            		listItem.ImageIndex = (int)ImagesEnum.Network;
            	}
            	else
            	{
            		throw new NotImplementedException(item.GetType().ToString());
            	}

            	listItem.SubItems.Add(item.Hint);
            	listItem.SubItems.Add(string.Empty);
            	listItem.Group = tasksListView.Groups[(int)GroupEnum.Storages];
            	listItem.Tag = item;
            	listItem.Checked = true;
            	tasksListView.Items.Add(listItem);
            }

            foreach (BackupEventTaskInfo taskInfo in _task.BeforeBackupTasksChain)
            {
            	var listItem = new ListViewItem(taskInfo.Program);
            	listItem.ImageIndex = (int)ImagesEnum.ProgramInRunBeforeAfterBackupChain;

            	listItem.SubItems.Add(taskInfo.Arguments);
            	listItem.SubItems.Add(string.Empty);
            	listItem.Group = tasksListView.Groups[(int) GroupEnum.BeforeBackupChain];
            	listItem.Tag = taskInfo;
            	listItem.Checked = true;
            	tasksListView.Items.Add(listItem);
            }
            
            foreach (BackupEventTaskInfo taskInfo in _task.AfterBackupTasksChain)
            {
            	var listItem = new ListViewItem(taskInfo.Program);
            	listItem.ImageIndex = (int)ImagesEnum.ProgramInRunBeforeAfterBackupChain;

            	listItem.SubItems.Add(taskInfo.Arguments);
            	listItem.SubItems.Add(string.Empty);
            	listItem.Group = tasksListView.Groups[(int) GroupEnum.AfterBackupChain];
            	listItem.Tag = taskInfo;
            	listItem.Checked = true;
            	tasksListView.Items.Add(listItem);
            }
            
            tasksListView.EndUpdate();

            // logical verifying of settings
            // If they are not correct, just closing the Master
            if (items.Count == 0)
            {
            	Messages.ShowInformationBox(Translation.Current[522]);
            	Close();
            }
            
            if (_task.Storages.Count < 1)
            {
            	Messages.ShowInformationBox(Translation.Current[523]);
            	Close();
            }
		}
	
		void CompressionItemsListViewResize(object sender, EventArgs e)
		{
			taskNameColumnHeader.Width = 180;
			
			int newWidth = tasksListView.Width - 35 - informationAboutTaskColumnHeader.Width - processingStateInformationColumnHeader.Width;
			taskNameColumnHeader.Width = newWidth < 35 ? 35 : newWidth;
		}
		
		#region Tray Interaction

		FormWindowState _previousFormState = FormWindowState.Maximized;
		void SwapToTray(bool changeFormState)
		{
			if (_trayModeActivated)
			{
				if (!notifyIcon.Visible)
					notifyIcon.Visible = true;

                if (!ShowInTaskbar)
                {
                    ShowInTaskbar = false;
                }

			    if (changeFormState)
				{
	    	        WindowState = FormWindowState.Minimized;
	    	        
				}
        	    if (_firstTimeApplicationInTray)
            	{
	            	notifyIcon.ShowBalloonTip(5000, Translation.Current[504], Translation.Current[475], ToolTipIcon.Info);
    	        	_firstTimeApplicationInTray = false;
        	    }
				Hide();        	    
			}
		}
		
		void ReturnFromTray()
		{
			if (_trayModeActivated)
			{
				Show();

				notifyIcon.Visible = false;
				ShowInTaskbar = true;
        	    WindowState = _previousFormState; 
			}
		}
		
		void NotifyIconClick(object sender, EventArgs e)
		{
			ReturnFromTray();
		}
		
		void ResizeForm(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
   	        {
				SwapToTray(false);
   	        } 
			else
			{
				_previousFormState = WindowState;
			}
		}
		#endregion
		
		void BackupBackgroundWorkerDoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			_controller.BackupClass.Run();
		}
		
		void BackupBackgroundWorkerRunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
		{
			_backupInProgress = false;
            if (_controller.Options.LoggingLevel == LogLevel.Support)
            {
				_controller.OpenLogFileInBrowser();
            }

            if (_controller.PowerTask == PowerTask.None && _controller.ErrorsOrWarningsRegistered)
            {
            	cancelButton.Enabled = false;
            	return;
            }
            
            Close();
		}
		
		void CancelButtonClick(object sender, EventArgs e)
		{
			_strongIntentionToCancelBackup = true;
			Close();
		}
		
		void ClosingForm(object sender, FormClosingEventArgs e)
		{
			if (abortBackupBackgroundWorker.IsBusy)
			{
				e.Cancel = true;
				return;
			}
			
            if (_backupInProgress)
			{
				if (e.CloseReason == CloseReason.UserClosing)
				{
					if (_strongIntentionToCancelBackup)
					{
						cancelButton.Enabled = false;
						abortBackupBackgroundWorker.RunWorkerAsync();
					}
					// questioning of user if he is sure he knows what happened if he closes this significant form
					else if (MessageBox.Show(Translation.Current[182], Translation.Current[183], MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0 ) == DialogResult.OK)
					{
						cancelButton.Enabled = false;
						abortBackupBackgroundWorker.RunWorkerAsync();
					}
					
					e.Cancel = true;
				}
				else
				{
					if (_backupInProgress)
					{
						e.Cancel = true;
					}
				}
			}
		}
		
		#region Aborting backup operation
		
		void AbortBackupBackgroundWorkerDoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			_controller.Abort();
		}
		
		void AbortBackupBackgroundWorkerRunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
		{
			_backupInProgress = false;
			DialogResult = DialogResult.Cancel;
			Close();
		}
		
		#endregion
		
		void HelpButtonClick(object sender, EventArgs e)
		{
			Help.ShowHelp(this, Files.HelpFile, HelpNavigator.Topic, "backupMaster/index.htm");
		}
	}
}
