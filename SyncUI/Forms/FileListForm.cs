using SyncUI.Models;

namespace SyncUI.Forms;

/// <summary>
/// Form for viewing files in a sync job
/// </summary>
public partial class FileListForm : Form
{
    private readonly SyncJob _job;

    public FileListForm(SyncJob job)
    {
        _job = job;
        
        InitializeComponent();
        InitializeForm();
    }

    private void InitializeComponent()
    {
        // Form properties
        Text = $"Files - {_job.Name}";
        Size = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 400);
    }

    private void InitializeForm()
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(10)
        };

        // Header panel
        var headerPanel = CreateHeaderPanel();
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        mainPanel.Controls.Add(headerPanel, 0, 0);

        // Files tree view
        var filesPanel = CreateFilesPanel();
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.Controls.Add(filesPanel, 0, 1);

        Controls.Add(mainPanel);
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
            Text = _job.Name,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 15),
            AutoSize = true
        };

        var pathLabel = new Label
        {
            Text = $"{_job.SourcePath} → {_job.DestinationPath}",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.LightGray,
            Location = new Point(20, 40),
            AutoSize = true
        };

        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(80, 30),
            Location = new Point(panel.Width - 100, 15),
            BackColor = Color.FromArgb(100, 100, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (sender, e) => Close();

        panel.Controls.AddRange(new Control[] { titleLabel, pathLabel, closeButton });

        return panel;
    }

    private Panel CreateFilesPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill
        };

        var titleLabel = new Label
        {
            Text = "Files",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(0, 0),
            AutoSize = true
        };

        var filesTreeView = new TreeView
        {
            Name = "FilesTreeView",
            Dock = DockStyle.Fill,
            Location = new Point(0, 30),
            Size = new Size(panel.Width - 10, panel.Height - 40),
            BorderStyle = BorderStyle.FixedSingle,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            FullRowSelect = true,
            ImageList = CreateImageList()
        };

        // Add columns for details view
        filesTreeView.AfterSelect += OnFileSelected;

        panel.Controls.AddRange(new Control[] { titleLabel, filesTreeView });

        // Load sample data (TODO: Replace with actual data from sync engine)
        LoadSampleFiles(filesTreeView);

        return panel;
    }

    private ImageList CreateImageList()
    {
        var imageList = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };

        // Add folder and file icons
        using var folderIcon = SystemIcons.Application.ToBitmap();
        using var fileIcon = SystemIcons.Information.ToBitmap();
        
        imageList.Images.Add("folder", folderIcon);
        imageList.Images.Add("file", fileIcon);

        return imageList;
    }

    private void LoadSampleFiles(TreeView treeView)
    {
        // This is a placeholder - in a real implementation, you would
        // load the actual file tree from the sync engine
        var rootNode = new TreeNode("Root")
        {
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };

        // Add some sample nodes
        var documentsNode = new TreeNode("Documents")
        {
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        documentsNode.Nodes.Add(new TreeNode("Report.docx") { ImageKey = "file", SelectedImageKey = "file" });
        documentsNode.Nodes.Add(new TreeNode("Notes.txt") { ImageKey = "file", SelectedImageKey = "file" });

        var imagesNode = new TreeNode("Images")
        {
            ImageKey = "folder",
            SelectedImageKey = "folder"
        };
        imagesNode.Nodes.Add(new TreeNode("Photo1.jpg") { ImageKey = "file", SelectedImageKey = "file" });
        imagesNode.Nodes.Add(new TreeNode("Photo2.png") { ImageKey = "file", SelectedImageKey = "file" });

        rootNode.Nodes.Add(documentsNode);
        rootNode.Nodes.Add(imagesNode);

        treeView.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private void OnFileSelected(object? sender, TreeViewEventArgs e)
    {
        // Handle file selection - could show file details in a status bar or panel
        // TODO: Implement file details display
    }
}
