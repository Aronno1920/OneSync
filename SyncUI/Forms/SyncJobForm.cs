using SyncUI.Models;
using SyncUI.ViewModels;

namespace SyncUI.Forms;

/// <summary>
/// Form for creating and editing sync jobs
/// </summary>
public partial class SyncJobForm : Form
{
    private readonly SyncJobViewModel _viewModel;

    public SyncJobForm(SyncJobViewModel viewModel)
    {
        _viewModel = viewModel;
        
        InitializeComponent();
        InitializeForm();
        InitializeDataBinding();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        // 
        // SyncJobForm
        // 
        ClientSize = new Size(889, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SyncJobForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Create Sync Job";
        ResumeLayout(false);
    }

    private void InitializeForm()
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 10,
            ColumnCount = 2,
            Padding = new Padding(20)
        };

        // Job Name
        mainPanel.Controls.Add(CreateLabel("Job Name:"), 0, 0);
        var nameTextBox = new TextBox
        {
            Name = "NameTextBox",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 0, 5)
        };
        mainPanel.Controls.Add(nameTextBox, 1, 0);

        // Source Path
        mainPanel.Controls.Add(CreateLabel("Source Path:"), 0, 1);
        var sourcePanel = new Panel { Dock = DockStyle.Fill, Height = 30 };
        var sourceTextBox = new TextBox
        {
            Name = "SourceTextBox",
            Dock = DockStyle.Fill,
            Location = new Point(0, 0),
            Width = sourcePanel.Width - 100
        };
        var sourceBrowseButton = new Button
        {
            Text = "Browse...",
            Size = new Size(80, 25),
            Location = new Point(sourcePanel.Width - 85, 2),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        sourceBrowseButton.Click += (sender, e) => OnBrowseSourceClickAsync();
        sourcePanel.Controls.AddRange(new Control[] { sourceTextBox, sourceBrowseButton });
        mainPanel.Controls.Add(sourcePanel, 1, 1);

        // Destination Path
        mainPanel.Controls.Add(CreateLabel("Destination Path:"), 0, 2);
        var destPanel = new Panel { Dock = DockStyle.Fill, Height = 30 };
        var destTextBox = new TextBox
        {
            Name = "DestTextBox",
            Dock = DockStyle.Fill,
            Location = new Point(0, 0),
            Width = destPanel.Width - 100
        };
        var destBrowseButton = new Button
        {
            Text = "Browse...",
            Size = new Size(80, 25),
            Location = new Point(destPanel.Width - 85, 2),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        destBrowseButton.Click += (sender, e) => OnBrowseDestClickAsync();
        destPanel.Controls.AddRange(new Control[] { destTextBox, destBrowseButton });
        mainPanel.Controls.Add(destPanel, 1, 2);

        // Sync Direction
        mainPanel.Controls.Add(CreateLabel("Sync Direction:"), 0, 3);
        var directionComboBox = new ComboBox
        {
            Name = "DirectionComboBox",
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 5, 0, 5)
        };
        directionComboBox.Items.AddRange(_viewModel.AvailableDirections.ToArray());
        mainPanel.Controls.Add(directionComboBox, 1, 3);

        // Sync Mode
        mainPanel.Controls.Add(CreateLabel("Sync Mode:"), 0, 4);
        var modeComboBox = new ComboBox
        {
            Name = "ModeComboBox",
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 5, 0, 5)
        };
        modeComboBox.Items.AddRange(_viewModel.AvailableModes.ToArray());
        mainPanel.Controls.Add(modeComboBox, 1, 4);

        // Test Connection Button
        var testButton = new Button
        {
            Text = "Test Paths",
            Size = new Size(120, 30),
            Margin = new Padding(0, 10, 0, 10)
        };
        testButton.Click += async (sender, e) => await OnTestConnectionClickAsync();
        mainPanel.Controls.Add(testButton, 1, 5);

        // Validation Message
        var validationLabel = new Label
        {
            Name = "ValidationLabel",
            Text = "Please fill in all required fields",
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 10)
        };
        mainPanel.Controls.Add(validationLabel, 0, 6);
        mainPanel.SetColumnSpan(validationLabel, 2);

        // Button Panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };

        var saveButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 30),
            Margin = new Padding(5, 0, 5, 0),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += async (sender, e) => await OnSaveClickAsync();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 30),
            Margin = new Padding(5, 0, 5, 0),
            FlatStyle = FlatStyle.Flat
        };
        cancelButton.Click += (sender, e) => Close();

        buttonPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });
        mainPanel.Controls.Add(buttonPanel, 0, 7);
        mainPanel.SetColumnSpan(buttonPanel, 2);

        // Set row styles
        for (int i = 0; i < 7; i++)
        {
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        }
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Set column styles
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Controls.Add(mainPanel);
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 5),
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
    }

    private void InitializeDataBinding()
    {
        // Bind to job properties
        var nameTextBox = Controls.Find("NameTextBox", true).FirstOrDefault() as TextBox;
        var sourceTextBox = Controls.Find("SourceTextBox", true).FirstOrDefault() as TextBox;
        var destTextBox = Controls.Find("DestTextBox", true).FirstOrDefault() as TextBox;
        var directionComboBox = Controls.Find("DirectionComboBox", true).FirstOrDefault() as ComboBox;
        var modeComboBox = Controls.Find("ModeComboBox", true).FirstOrDefault() as ComboBox;
        var validationLabel = Controls.Find("ValidationLabel", true).FirstOrDefault() as Label;

        // Subscribe to property changes
        _viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SyncJobViewModel.ValidationMessage) && validationLabel != null)
            {
                validationLabel.Text = _viewModel.ValidationMessage;
                validationLabel.ForeColor = _viewModel.IsValid ? Color.Green : Color.Red;
            }
        };

        // Handle text changes
        if (nameTextBox != null)
        {
            nameTextBox.TextChanged += (sender, e) =>
            {
                _viewModel.Job.Name = nameTextBox.Text;
                _viewModel.Validate();
            };
        }

        if (sourceTextBox != null)
        {
            sourceTextBox.TextChanged += (sender, e) =>
            {
                _viewModel.Job.SourcePath = sourceTextBox.Text;
                _viewModel.Validate();
            };
        }

        if (destTextBox != null)
        {
            destTextBox.TextChanged += (sender, e) =>
            {
                _viewModel.Job.DestinationPath = destTextBox.Text;
                _viewModel.Validate();
            };
        }

        if (directionComboBox != null)
        {
            directionComboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (Enum.TryParse<SyncDirection>(directionComboBox.SelectedItem?.ToString(), out var direction))
                {
                    _viewModel.Job.Direction = direction;
                }
            };
        }

        if (modeComboBox != null)
        {
            modeComboBox.SelectedIndexChanged += (sender, e) =>
            {
                if (Enum.TryParse<SyncMode>(modeComboBox.SelectedItem?.ToString(), out var mode))
                {
                    _viewModel.Job.Mode = mode;
                }
            };
        }
    }

    public void Initialize(SyncJob job, bool isNew)
    {
        _viewModel.Initialize(job, isNew);
        Text = isNew ? "Create Sync Job" : "Edit Sync Job";

        // Populate form fields
        var nameTextBox = Controls.Find("NameTextBox", true).FirstOrDefault() as TextBox;
        var sourceTextBox = Controls.Find("SourceTextBox", true).FirstOrDefault() as TextBox;
        var destTextBox = Controls.Find("DestTextBox", true).FirstOrDefault() as TextBox;
        var directionComboBox = Controls.Find("DirectionComboBox", true).FirstOrDefault() as ComboBox;
        var modeComboBox = Controls.Find("ModeComboBox", true).FirstOrDefault() as ComboBox;

        if (nameTextBox != null) nameTextBox.Text = job.Name;
        if (sourceTextBox != null) sourceTextBox.Text = job.SourcePath;
        if (destTextBox != null) destTextBox.Text = job.DestinationPath;
        if (directionComboBox != null) directionComboBox.SelectedItem = job.Direction.ToString();
        if (modeComboBox != null) modeComboBox.SelectedItem = job.Mode.ToString();
    }

    private void OnBrowseSourceClickAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Source Folder",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var sourceTextBox = Controls.Find("SourceTextBox", true).FirstOrDefault() as TextBox;
            if (sourceTextBox != null)
            {
                sourceTextBox.Text = dialog.SelectedPath;
                _viewModel.SetSourcePath(dialog.SelectedPath);
            }
        }
    }

    private void OnBrowseDestClickAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Destination Folder",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var destTextBox = Controls.Find("DestTextBox", true).FirstOrDefault() as TextBox;
            if (destTextBox != null)
            {
                destTextBox.Text = dialog.SelectedPath;
                _viewModel.SetDestinationPath(dialog.SelectedPath);
            }
        }
    }

    private async Task OnTestConnectionClickAsync()
    {
        await _viewModel.TestConnectionAsync();
    }

    private async Task OnSaveClickAsync()
    {
        _viewModel.Validate();
        if (!_viewModel.IsValid)
        {
            MessageBox.Show("Please fix validation errors before saving.", "Validation Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _viewModel.SaveAsync();
        if (result != null)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
