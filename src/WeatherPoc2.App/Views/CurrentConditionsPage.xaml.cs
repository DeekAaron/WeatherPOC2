using WeatherPoc2.Core.ViewModels;

namespace WeatherPoc2.App.Views;

public partial class CurrentConditionsPage : ContentPage
{
    private readonly CurrentConditionsViewModel _viewModel;

    public CurrentConditionsPage(CurrentConditionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Fetch-on-load — the only refresh trigger in Feature 1 (focus/manual are Feature 9).
        if (_viewModel.LoadCommand.CanExecute(null))
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
