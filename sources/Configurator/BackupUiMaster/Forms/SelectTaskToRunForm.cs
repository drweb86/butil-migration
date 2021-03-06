﻿using System.Collections.Generic;
using System.Windows.Forms;
using BULocalization;
using BUtil.Core.Options;

namespace BUtil.Configurator.BackupUiMaster.Forms
{
    /// <summary>
    /// Selects the tasks to run in backup ui application
    /// </summary>
    public partial class SelectTaskToRunForm : Form
    {
        #region Constructors

        public SelectTaskToRunForm(Dictionary<string, BackupTask> tasks)
        {
            InitializeComponent();

            _tasks = tasks;

            var titles = new List<string>();
            foreach (var backupTask in tasks)
            {
                titles.Add(backupTask.Key);
            }
            _tasksComboBox.DataSource = titles;

            ApplyLocalization(Translation.Current);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Tasks to execute
        /// </summary>
        public List<BackupTask> TasksToRun
        {
            get { return new List<BackupTask> { _tasks[(string)_tasksComboBox.SelectedItem ]}; }
        }

        #endregion

        #region Private Methods

        void OnOkButtonClick(object sender, System.EventArgs e)
        {
            if (_tasksComboBox.SelectedIndex!=-1)
            {
                DialogResult = DialogResult.OK;
            }
        }

        void ApplyLocalization(Translation translation)
        {
            Text = translation[179];
            _cancelButton.Text = translation[359];
            _chooseTaskLabel.Text = translation[641];
        }

        #endregion

        #region Fields

        readonly Dictionary<string, BackupTask> _tasks;

        #endregion
    }
}
