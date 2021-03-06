using System;
using System.Collections.Generic;
using BULocalization;
using System.Windows.Forms;
using BUtil.Configurator.Configurator;
using BUtil.Configurator.Configurator.Forms;
using BUtil.Core.Misc;
using BUtil.Core.PL;
using BUtil.Core.FileSystem;
using System.IO;
using System.Collections.ObjectModel;

namespace BUtil.Configurator
{
	public static class Program
	{
		#region Fields
		
		static bool _packageIsBroken;
		static bool _7ZipIsBroken;
		static bool _schedulerInstalled;
		static LanguagesManager _localsManager;
		
		#endregion
		
		#region Properties
		
		public static bool PackageIsBroken
		{
			get { return _packageIsBroken; }
		}
		
		public static bool SevenZipIsBroken
		{
			get { return _7ZipIsBroken; }
		}
		
		public static bool SchedulerInstalled
		{
			get { return _schedulerInstalled; }
		}
		
		#endregion
		
		#region Private methods
		
		/// <summary>
		/// Loading the localization
		/// </summary>
		static void LoadLanguage()
		{
			var settings = new ManagerBehaviorSettings
			                   {
			                       RequestLanguageIfNotSpecified = true,
			                       UseToolGeneratedConfigFile = true
			                   };

		    _localsManager = new LanguagesManager(new ReadOnlyCollection<string>(new [] { "Configurator Program", "Core Library",	}), Directories.LocalsFolder, "BUtil", settings);
			_localsManager.Init();
			_localsManager.Apply();
		}
		
		/// <summary>
		/// Checking the integrity: Checking if the all components present and Checking 7-zip intergrity
		/// </summary>
		static void CheckIntergrity()
		{
			try
            {
                Directories.CriticalFoldersCheck();
                Files.CriticalFilesCheck();
            }
            catch (DirectoryNotFoundException e)
            {
                _packageIsBroken = true;
                Messages.ShowErrorBox(string.Format(Translation.Current[580], e.Message));
            }
            catch (FileNotFoundException e)
            {
                _packageIsBroken = true;
                Messages.ShowErrorBox(string.Format(Translation.Current[580], e.Message));
            }
            
            // Checking 7-zip intergrity
            try
           	{
	            MD5Class.Verify7ZipBinaries();
            }
            catch (InvalidSignException ee)
            {
            	_7ZipIsBroken = true;
            	Messages.ShowErrorBox(string.Format(Translation.Current[541], ee.Message));
            }
            
            _schedulerInstalled = File.Exists(Files.Scheduler);
		}
		
		/// <summary>
		/// Processing command line argument
		/// </summary>
		/// <param name="args">Arguments</param>
		static void ProcessArguments(string[] args)
		{
			var controller = new ConfiguratorController(_localsManager);
		    args = args ?? new string[] {};
            if (args.Length == 0)
            {
                Application.Run(new MainForm(controller));
                return;
            }

		    if (args.Length == 1)
			{
			    string firstArgumentUpper = args[0].ToUpperInvariant();
                
                if (firstArgumentUpper == Arguments.RemoveLocalSettings)
				{
					controller.RemoveLocalUserSettings();
				}
                else if (firstArgumentUpper == Arguments.RunRestorationMaster)
				{
					controller.OpenRestorationMaster(string.Empty, true);
				}
                else if (firstArgumentUpper.EndsWith(Files.ImageFilesExtension.ToUpperInvariant()))
				{
					controller.OpenRestorationMaster(args[0], true);
				}
                else if (firstArgumentUpper == Arguments.RunJournals)
				{
					controller.OpenJournals(true);
				}
                else if (firstArgumentUpper == Arguments.RunBackupMaster)
                {
                    controller.OpenBackupUiMaster(new string[] {}, true);
                }
				else
				{
					Messages.ShowErrorBox(Translation.Current[581]);
				}
			}
            else if (args.Length > 1 && args[0].ToUpperInvariant() == Arguments.RunBackupMaster)
			{
				var listOfTasksToExecute = new List<string>();
                foreach (var argument in args)
                {
                    if (argument.StartsWith(Arguments.RunTask) && argument.Length > Arguments.RunTask.Length)
                    {
                        listOfTasksToExecute.Add(argument.Substring(Arguments.RunTask.Length + 1));
                    }
                }
				controller.OpenBackupUiMaster(listOfTasksToExecute.ToArray(), true);
			}
			else if (args.Length > 1)
    		{
					Messages.ShowErrorBox(Translation.Current[581]);
				}
				else
				{
					Application.Run(new MainForm(controller));
				}

				
		}
		
		#endregion
		
		[STAThread]
		public static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			ImproveIt.InitInfrastructure(true);
	
			LoadLanguage();
			CheckIntergrity();
			ProcessArguments(args);
		}
	}
}
