using WeatherPoc2.Core.ViewModels;

namespace WeatherPoc2.App.Views;

public partial class CurrentConditionsPage : ContentPage
{
    private readonly WeatherViewModel _viewModel;

    public CurrentConditionsPage(WeatherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Fetch-on-load — the only refresh trigger in this Feature (focus/manual are Feature 9).
        if (_viewModel.LoadCommand.CanExecute(null))
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
