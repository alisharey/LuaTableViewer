using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;

using NLua;
using System.Data;
using System.Windows.Controls;
using LuaTableViewer.Models;

namespace LuaTableViewer;

public partial class MainWindowViewModel : ObservableObject
{

    [ObservableProperty]
    DataView? _dataView;

    [ObservableProperty]
    private bool _isDarkTheme;

    private readonly CustomLuaTable? _luaTable;

    partial void OnIsDarkThemeChanged(bool value)
    {
        var helper = new PaletteHelper();
        var theme = helper.GetTheme();

        theme.SetBaseTheme(value ? Theme.Dark : Theme.Light);
        helper.SetTheme(theme);
        
        _luaTable?.SaveLuaTable(DataView ?? new());
    }

    [RelayCommand]
    public void CellChanged(DataGridCellEditEndingEventArgs e)
    {
        string value = ((TextBox)e.EditingElement).Text;
        string columnName = e.Column.SortMemberPath.ToString() ?? "";
        string rowID = ((DataRowView)e.Row.Item).Row["id"].ToString() ?? "";

        bool columnExists = _luaTable?.RowContainsColumn(rowID, columnName) ?? true;
        if(!columnExists)
        {
            _luaTable?.AddColumnToRow(rowID, columnName);
        }



    }

   


    public MainWindowViewModel()
    {
        PaletteHelper palette = new PaletteHelper();
        ITheme theme = palette.GetTheme();
        var currentTheme = theme.GetBaseTheme();
        IsDarkTheme = currentTheme == BaseTheme.Dark;

        _luaTable = new CustomLuaTable(@"Resources\Table_Boss.txt");
        DataView = _luaTable.GetDataViewPlus();

       
    }


}
