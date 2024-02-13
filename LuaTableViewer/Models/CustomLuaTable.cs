using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;




using NLua;

namespace LuaTableViewer.Models
{
    public class CustomLuaTable
    {
        private readonly string _filename = "";
        private readonly string _tablename = "";
        LuaTable? LuaTable { get; set; }
        Dictionary<object, object>? LuaTableDict { get; set; }
        Lua Lua { get; set; }

        private Dictionary<object, object>? _rowInfo;

        public CustomLuaTable(string filename)
        {
            this._filename = filename;
            Lua = new Lua();
            Lua.DoFile(filename);

            var tableNames = Lua.DoString(@"
                local tables = {}
                for k, v in pairs(_G) do
                    if type(v) == 'table' and k ~= ""table"" and k ~= ""luanet"" and k ~= ""_G"" and k ~= ""package"" and k ~= ""io"" and k ~= ""os"" and k ~= ""string"" and k ~= ""coroutine"" and k ~= ""utf8"" and k ~= ""debug"" and k ~= ""math"" then
                        tables[#tables + 1] = k
                    end
                end
                return tables
            
            ");

            _tablename = (tableNames?[0] as LuaTable)?.Values.Cast<string>().FirstOrDefault() ?? "";

            LuaTable = Lua.GetTable(_tablename);
            LuaTableDict = Lua.GetTableDict(LuaTable);
            LuaTableDict = LuaTableDict.OrderBy(kvp => int.Parse(kvp.Key.ToString() ?? "0")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            

            
        }
        private void SetRowInfo(LuaTable? value)
        {
            var _value = Lua.GetTableDict(value);
            _value = _value.OrderBy(kvp => kvp.Key.ToString()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (_value != null)
            {
                foreach (KeyValuePair<object, object> kvp in _value)
                {
                    if (kvp.Value is string s)
                    {
                        _rowInfo?.Add(kvp.Key, s);
                    }
                    else if (kvp.Value is LuaTable tableValue)
                    {
                        var dict = Lua.GetTableDict(tableValue);
                        bool isNewTable = CheckNumericalID(dict.Keys.ToList());
                        if (isNewTable) //ReliveTime = {[9]=15} 
                        {
                            List<string> values = new();
                            foreach (var _kvp in dict)
                            {
                                values.Add($"[{_kvp.Key}]={_kvp.Value}");
                            }
                            //add value= string.Join(", ", values)
                            _rowInfo?.Add(kvp.Key, string.Join(", ", values));

                        }
                        else if (CheckSequential(dict.Keys.ToList()))
                        {
                            _rowInfo?.Add(kvp.Key, string.Join(", ", dict.Values));
                        }
                        else
                        {
                            // add key and empty value to leader. 
                            _rowInfo?.Add(kvp.Key, "");
                            SetRowInfo(tableValue);
                        }
                    }
                    else _rowInfo?.Add(kvp.Key, kvp.Value);


                }

            }



        }

        public DataView? GetDataView()
        {
            var list = new List<string>();
            DataTable dt = new();
            if (LuaTableDict is { })
            {
                _rowInfo = new();
                SetRowInfo(LuaTableDict.First().Value as LuaTable);
                var columnNames = _rowInfo.Keys;

                var keycolumn = dt.Columns.Add("Key");
                keycolumn.ReadOnly = true;
                dt.Columns.AddRange(columnNames.Select(columnName => new DataColumn(columnName.ToString())).ToArray());


                foreach (KeyValuePair<object, object> row in LuaTableDict)
                {
                    _rowInfo = new();
                    SetRowInfo(row.Value as LuaTable);
                    //check if theres a non existing column
                    var columns = _rowInfo.Keys;
                    foreach (var key in columns)
                    {
                        bool columnExists = dt.Columns.Cast<DataColumn>().Any(column => column.ColumnName.Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (!columnExists)
                        {
                            dt.Columns.Add(new DataColumn(key.ToString()));
                        }

                    }
                    DataRow newRow = dt.NewRow();
                    newRow["key"] = row.Key.ToString();

                    foreach (var kvp in _rowInfo)
                    {
                        newRow[kvp.Key.ToString() ?? ""] = kvp.Value;
                    }

                    dt.Rows.Add(newRow);



                }
            }

            // sort the columns for the second time -- missses up order for nested dicts
            //var sortedColumns = dt.Columns.Cast<DataColumn>()
            //                    .OrderBy(column => column.ColumnName)
            //                    .ToList();
            //foreach (var column in sortedColumns)
            //{
            //    dt.Columns[column.ColumnName]?.SetOrdinal(sortedColumns.IndexOf(column));

            //}
            return dt.DefaultView;

        }



        private List<string> ConvertToStringList(List<Dictionary<string, object>> info)
        {
            var list = new List<string>();
            if (LuaTableDict is { })
            {

                foreach (var row in LuaTableDict)
                {
                    string key = row.Key.ToString() ?? "";
                    var changedInfo = info.FirstOrDefault(dict => dict.ContainsKey("Key") && dict["Key"].ToString() == key);
                    var lineInfo = ConvertLuaTableRowToString(row.Value as LuaTable, changedInfo);
                    string? line = $"\t[{key}] = {{{lineInfo.Text}}}";
                    list.Add(line);

                }
            }


            return list;
        }

        private LuaTableLine ConvertLuaTableRowToString(LuaTable? luaTableRow, Dictionary<string, object>? changedInfo)
        {
            var rowDict = Lua.GetTableDict(luaTableRow);
            rowDict = rowDict.OrderBy(kvp => kvp.Key.ToString()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Dictionary<string, object> parameters = new();
            if (rowDict != null)
            {
                foreach (KeyValuePair<object, object> kvp in rowDict)
                {
                    object formattedValue;
                    string value = changedInfo?[kvp.Key.ToString() ?? ""].ToString() ?? "";

                    if (kvp.Value is string)
                    {
                        formattedValue = $"'{value}'";
                    }
                    else if (kvp.Value is LuaTable tableValue)
                    {

                        var dict = Lua.GetTableDict(tableValue);
                        var keys = dict.Keys.ToList();

                        if (CheckNumericalID(keys) || CheckSequential(keys))
                        {
                            formattedValue = "{" + value + "}";

                        }
                        else
                        {
                            if (tableValue.Keys.Count > 0)
                            {
                                formattedValue = $"{{{ConvertLuaTableRowToString(tableValue, changedInfo).Text}}}";
                            }
                            else formattedValue = "{" + value + "}";


                        }
                    }
                    else formattedValue = value;

                    parameters.Add(kvp.Key.ToString() ?? "", formattedValue);
                    // try here 
                }

            }
            var text = string.Join(", ", parameters.Select(kvp => kvp.Key + " = " + kvp.Value));

            return new LuaTableLine()
            {
                Text = text,
                Parameters = parameters
            };
        }



        private static bool CheckNumericalID(List<object> list)
        {
            if (list.Count > 0 && list[0] is long x)
            {
                return x != 1;
            }
            else return false;
        }
        private static bool CheckSequential(List<object> list)
        {
            if (list.Count == 1 && list[0] is long x)
            {
                return x == 1;
            }
            else if (list.Count < 2 || list[0] is string)
            {
                return false;
            }


            var intList = list.Select(x => int.Parse(x.ToString() ?? "0")).ToList();

            for (int i = 1; i < intList.Count; i++)
            {
                if (intList[i] != intList[i - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }



        public void SaveLuaTable(DataView dataView)
        {
            List<Dictionary<string, object>> dataTableDict = new();

            foreach (DataRow dataRow in dataView.ToTable().Rows)
            {
                var dataRowdict = dataRow.Table.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => dataRow[col]);
                dataTableDict.Add(dataRowdict);
            }

            var lines = ConvertToStringList(dataTableDict);

            var intialLines = File.ReadAllLines(_filename);
            //string someEncodedBs = intialLines[0] + "\n";
            string TableNameline = $"{_tablename} = {{\n";
            string ending = $"}}\nreturn {_tablename}";

            string output = TableNameline + string.Join(",\n", lines) + "\n" + ending;

            File.WriteAllText(_filename, output);

        }





        internal bool RowContainsColumn(string key, string columnName)
        {
            var row = LuaTableDict?.Where(kvp => (kvp.Key.ToString() ?? "") == key).First().Value;
            var dict = Lua.GetTableDict(row as LuaTable);
            return dict.Keys.Contains(columnName);
        }

        internal void AddColumnToRow(string key, string columnName)
        {
            if (LuaTableDict is { })
            {
                var row = LuaTableDict.Where(kvp => kvp.Key.ToString() == key).First().Value as LuaTable;

                foreach (var kvp in LuaTableDict)
                {

                    string rowkey = kvp.Key.ToString() ?? "";
                    if (RowContainsColumn(rowkey, columnName))
                    {
                        var inner_dict = Lua.GetTableDict(kvp.Value as LuaTable);
                        if (row is { }) row[columnName] = inner_dict[columnName];
                        return;
                    }


                }
            }


        }
    }
}
