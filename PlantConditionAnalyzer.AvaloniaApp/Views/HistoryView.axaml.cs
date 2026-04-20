using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PlantConditionAnalyzer.AvaloniaApp.ViewModels;
using System;

namespace PlantConditionAnalyzer.AvaloniaApp.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

    }
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is HistoryViewModel vm)
        {
            vm.ChartDataChanged += (snapshots, selected) =>
            {
                LineChart.SetData(snapshots, selected);
            };
        }
    }
}