using Microsoft.Extensions.DependencyInjection;
using SyncUI.Models;
using SyncUI.ViewModels;
using System.ComponentModel;

namespace SyncUI.Forms;

/// <summary>
/// Main form for the OneSync application
/// </summary>
public partial class MainForm : Form
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private System.Windows.Forms.Timer? _refreshTimer;

    public MainForm(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;

        InitializeForm();
        InitializeDataBinding();

        Load += async (sender, e) => await OnLoadAsync();
        FormClosing += OnFormClosing;
    }

    private void InitializeForm()
    {
        // Form properties
        Text = "OneSync - File Synchronization";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        // Create menu strip
        var menuStrip = CreateMenuStrip();
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        // Create main controls
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10)
        };

        // Header panel with status
        var headerPanel = CreateHeaderPanel();
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        mainPanel.Controls.Add(headerPanel, 0, 0);

        // Jobs list panel
        var jobsPanel = CreateJobsPanel();
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.Controls.Add(jobsPanel, 0, 1);

        // Footer panel with actions
        var footerPanel = CreateFooterPanel();
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        mainPanel.Controls.Add(footerPanel, 0, 2);

        Controls.Add(mainPanel);

        // Initialize refresh timer
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += async (sender, e) => await RefreshJobsAsync();
        _refreshTimer.Start();
    }

    private MenuStrip CreateMenuStrip()
    {
        var menuStrip = new MenuStrip();

        // File Menu
        var fileMenu = new ToolStripMenuItem("&File");

        var newJobMenuItem = new ToolStripMenuItem("New Sync Job", null, (s, e) => OnCreateJobClickAsync());
        newJobMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        fileMenu.DropDownItems.Add(newJobMenuItem);

        fileMenu.DropDownItems.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => Application.Exit());
        exitMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
        fileMenu.DropDownItems.Add(exitMenuItem);

        menuStrip.Items.Add(fileMenu);

        // Edit Menu
        var editMenu = new ToolStripMenuItem("&Edit");

        var preferencesMenuItem = new ToolStripMenuItem("Preferences...", null, (s, e) => OnPreferencesClick());
        preferencesMenuItem.ShortcutKeys = Keys.Control | Keys.P;
        editMenu.DropDownItems.Add(preferencesMenuItem);

        menuStrip.Items.Add(editMenu);

        // View Menu
        var viewMenu = new ToolStripMenuItem("&View");

        var refreshMenuItem = new ToolStripMenuItem("Refresh", null, async (s, e) => await RefreshJobsAsync());
        refreshMenuItem.ShortcutKeys = Keys.F5;
        viewMenu.DropDownItems.Add(refreshMenuItem);

        viewMenu.DropDownItems.Add(new ToolStripSeparator());

        var statusMenuItem = new ToolStripMenuItem("Connection Status", null, (s, e) => OnStatusClick());
        viewMenu.DropDownItems.Add(statusMenuItem);

        menuStrip.Items.Add(viewMenu);

        // Help Menu
        var helpMenu = new ToolStripMenuItem("&Help");

        var documentationMenuItem = new ToolStripMenuItem("Documentation", null, (s, e) => OnDocumentationClick());
        helpMenu.DropDownItems.Add(documentationMenuItem);

        var aboutMenuItem = new ToolStripMenuItem("About OneSync", null, (s, e) => OnAboutClick());
        helpMenu.DropDownItems.Add(aboutMenuItem);

        menuStrip.Items.Add(helpMenu);

        return menuStrip;
    }

    private Panel CreateHeaderPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        var titleLabel = new Label
        {
            Text = "OneSync",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var statusLabel = new Label
        {
            Name = "StatusLabel",
            Text = "Connecting...",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.LightGray,
            Location = new Point(20, 45),
            AutoSize = true
        };

        var connectionIndicator = new Label
        {
            Name = "ConnectionIndicator",
            Text = "●",
            Font = new Font("Segoe UI", 12),
            ForeColor = Color.Orange,
            Location = new Point(panel.Width - 50, 30),
            AutoSize = true
        };

        panel.Controls.AddRange(new Control[] { titleLabel, statusLabel, connectionIndicator });

        return panel;
    }

    private Panel CreateJobsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 10)
        };

        var titleLabel = new Label
        {
            Text = "Sync Jobs",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(0, 0),
            AutoSize = true
        };

        var jobsDataGridView = new DataGridView
        {
            Name = "JobsDataGridView",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.LightGray
        };

        // Add columns
        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Job Name",
            DataPropertyName = "Name",
            Width = 200,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "SourcePath",
            HeaderText = "Source",
            DataPropertyName = "SourcePath",
            Width = 250
        });

        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "DestinationPath",
            HeaderText = "Destination",
            DataPropertyName = "DestinationPath",
            Width = 250
        });

        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Direction",
            HeaderText = "Direction",
            DataPropertyName = "DirectionText",
            Width = 120
        });

        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            DataPropertyName = "StatusText",
            Width = 100
        });

        jobsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Progress",
            HeaderText = "Progress",
            Width = 80
        });

        jobsDataGridView.CellDoubleClick += (sender, e) => OnJobCellDoubleClickAsync(e);

        panel.Controls.AddRange(new Control[] { titleLabel, jobsDataGridView });

        return panel;
    }

    private Panel CreateFooterPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        var statsLabel = new Label
        {
            Name = "StatsLabel",
            Text = "Total Jobs: 0 | Active: 0 | Transferred: 0 B",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.White,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var createButton = new Button
        {
            Text = "Create New Job",
            Size = new Size(140, 35),
            Location = new Point(panel.Width - 160, 12),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10)
        };
        createButton.FlatAppearance.BorderSize = 0;
        createButton.Click += (sender, e) => OnCreateJobClickAsync();

        var refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(100, 35),
            Location = new Point(panel.Width - 320, 12),
            BackColor = Color.FromArgb(100, 100, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10)
        };
        refreshButton.FlatAppearance.BorderSize = 0;
        refreshButton.Click += async (sender, e) => { await RefreshJobsAsync(); };

        panel.Controls.AddRange(new Control[] { statsLabel, createButton, refreshButton });

        return panel;
    }

    private void InitializeDataBinding()
    {
        // Bind Jobs collection to DataGridView
        var jobsGrid = Controls.Find("JobsDataGridView", true).FirstOrDefault() as DataGridView;
        if (jobsGrid != null)
        {
            jobsGrid.DataSource = _viewModel.Jobs;
        }

        // Bind status label
        var statusLabel = Controls.Find("StatusLabel", true).FirstOrDefault() as Label;
        if (statusLabel != null)
        {
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.StatusMessage))
                {
                    statusLabel.Text = _viewModel.StatusMessage;
                }
            };
        }

        // Bind connection indicator
        var connectionIndicator = Controls.Find("ConnectionIndicator", true).FirstOrDefault() as Label;
        if (connectionIndicator != null)
        {
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsConnected))
                {
                    connectionIndicator.ForeColor = _viewModel.IsConnected ? Color.Green : Color.Red;
                }
            };
        }

        // Bind statistics label
        var statsLabel = Controls.Find("StatsLabel", true).FirstOrDefault() as Label;
        if (statsLabel != null)
        {
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ActiveJobsCount) ||
                    e.PropertyName == nameof(MainViewModel.TotalJobsCount) ||
                    e.PropertyName == nameof(MainViewModel.TotalTransferredSize))
                {
                    statsLabel.Text = $"Total Jobs: {_viewModel.TotalJobsCount} | Active: {_viewModel.ActiveJobsCount} | Transferred: {_viewModel.TotalTransferredSize}";
                }
            };
        }
    }

    private async Task OnLoadAsync()
    {
        await _viewModel.InitializeAsync();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }

    private async Task RefreshJobsAsync()
    {
        await _viewModel.RefreshJobsAsync();
    }

    private async void OnCreateJobClickAsync()
    {
        var syncJobForm = _serviceProvider.GetRequiredService<SyncJobForm>();
        var newJob = new SyncJob();
        syncJobForm.Initialize(newJob, true);
        
        if (syncJobForm.ShowDialog() == DialogResult.OK)
        {
            // Refresh the jobs list to show the newly created job
            await RefreshJobsAsync();
        }
    }

    private async void OnJobCellDoubleClickAsync(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var jobsGrid = Controls.Find("JobsDataGridView", true).FirstOrDefault() as DataGridView;
        if (jobsGrid?.SelectedRows.Count > 0)
        {
            var job = jobsGrid.SelectedRows[0].DataBoundItem as SyncJob;
            if (job != null)
            {
                // Show job details or edit dialog
                var syncJobForm = _serviceProvider.GetRequiredService<SyncJobForm>();
                syncJobForm.Initialize(job, false);
                
                if (syncJobForm.ShowDialog() == DialogResult.OK)
                {
                    // Refresh the jobs list to show any changes
                    await RefreshJobsAsync();
                }
            }
        }
    }

    private void OnPreferencesClick()
    {
        MessageBox.Show(
            "Preferences dialog would open here.\n\n" +
            "This would allow users to configure:\n" +
            "- Default sync settings\n" +
            "- Network preferences\n" +
            "- Notification options\n" +
            "- Theme selection",
            "Preferences",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnStatusClick()
    {
        var status = _viewModel.IsConnected ? "Connected" : "Disconnected";
        var message = $"Connection Status: {status}\n\n" +
                     $"Total Jobs: {_viewModel.TotalJobsCount}\n" +
                     $"Active Jobs: {_viewModel.ActiveJobsCount}\n" +
                     $"Transferred: {_viewModel.TotalTransferredSize}";

        MessageBox.Show(
            message,
            "Connection Status",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnDocumentationClick()
    {
        MessageBox.Show(
            "Documentation would open here.\n\n" +
            "For more information about OneSync:\n" +
            "- Read the README.md file\n" +
            "- Visit the project repository\n" +
            "- Check the docs/ folder for detailed documentation",
            "Documentation",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        // 
        // MainForm
        // 
        ClientSize = new Size(970, 490);
        Name = "MainForm";
        ResumeLayout(false);

    }

    private void OnAboutClick()
    {
        MessageBox.Show(
            "OneSync - File Synchronization Tool\n\n" +
            "Version: 1.0.0\n" +
            "A powerful file synchronization application\n" +
            "built with Rust sync engine and .NET Windows Forms.\n\n" +
            "© 2024 OneSync Project",
            "About OneSync",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
