﻿using Microsoft.Win32;

namespace BUtil.BackupUiMaster
{
    internal static class NativeMethods
    {
        public static void ScheduleOpeningFileAfterLoginOfUserIntoTheSystem(string filename)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion", true);
            RegistryKey writeKey = key.OpenSubKey("RunOnce", true);
            if (writeKey == null)
            {
            	key.CreateSubKey("RunOnce");
            	writeKey = key.OpenSubKey("RunOnce", true);
            }

            writeKey.SetValue("BUtil Backup Report", "\"" + filename + "\"");
        }
    }
}
