using SyncUI.ViewModels;
using SyncUI.Models;

namespace SyncUI.Views;

public partial class FileListView : ContentPage
{
    private FileMonitorViewModel _viewModel;

    public FileListView(FileMonitorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Get the job from navigation parameters
        if (Shell.Current?.Items != null)
        {
            var job = new SyncJob { Name = "Sample Job" };
            _viewModel.Initialize(job);
        }
    }
}
