using SyncUI.ViewModels;
using SyncUI.Models;

namespace SyncUI.Views;

public partial class SyncJobPage : ContentPage
{
    private SyncJobViewModel _viewModel;

    public SyncJobPage(SyncJobViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Check if we have navigation parameters
        if (Shell.Current?.Items != null)
        {
            var isNew = true;
            _viewModel.Initialize(null, isNew);
        }
        else
        {
            _viewModel.Initialize(null, true);
        }
    }
}
