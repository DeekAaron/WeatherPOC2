using WeatherPoc2.Core.ViewModels;

namespace WeatherPoc2.App.Views;

public partial class LocationSearchPage : ContentPage
{
    public LocationSearchPage(LocationSearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
