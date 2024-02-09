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
        LuaTable? LuaTable { get; set; }
        Dictionary<object, object>? LuaTableDict { get; set; }
        Lua Lua { get; set; }

        Dictionary<object, object>? RowInfo { get; set; }

        public CustomLuaTable(string filename)
        {
            this._filename = filename;
            Lua = new Lua();
            Lua.DoFile(filename);

            // Access Lua variables
            object[] result = Lua.DoString("return Table_Boss");
            object luaTableObject = result[0];
            LuaTable = (LuaTable)luaTableObject;
            LuaTableDict = Lua.GetTableDict(LuaTable);

            

        }

        private void SetRowInfo(LuaTable? value)
        {
            var _value = Lua.GetTableDict(value);

            if (_value != null)
            {
                foreach (KeyValuePair<object, object> kvp in _value)
                {
                    if (kvp.Value is string s)
                    {
                        RowInfo?.Add(kvp.Key, s);
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
                            RowInfo?.Add(kvp.Key, string.Join(", ", values));

                        }
                        else if (CheckSequential(dict.Keys.ToList()))
                        {
                            RowInfo?.Add(kvp.Key, string.Join(", ", dict.Values));
                        }
                        else
                        {
                            // add key and empty value to leader. 
                            RowInfo?.Add(kvp.Key, "");
                            SetRowInfo(tableValue);
                        }
                    }
                    else RowInfo?.Add(kvp.Key, kvp.Value);


                }

            }



        }
        public DataView? GetDataViewPlus()
        {
            var list = new List<string>();
            DataTable dt = new();
            if (LuaTableDict is { })
            {
                RowInfo = new();
                SetRowInfo(LuaTableDict.First().Value as LuaTable);
                var columnNames = RowInfo.Keys;
                
                dt.Columns.AddRange(columnNames.Select(columnName => new DataColumn(columnName.ToString())).ToArray());


                foreach (KeyValuePair<object, object> row in LuaTableDict)
                {
                    RowInfo = new();
                    SetRowInfo(row.Value as LuaTable);
                    //check if theres a non existing column
                    var columns = RowInfo.Keys;
                    foreach (var key in columns)
                    {
                        bool columnExists = dt.Columns.Cast<DataColumn>().Any(column => column.ColumnName.Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (!columnExists)
                        {
                            dt.Columns.Add(new DataColumn(key.ToString()));
                        }

                    }
                    DataRow newRow = dt.NewRow();


                    foreach (var kvp in RowInfo)
                    {
                        newRow[kvp.Key.ToString() ?? ""] = kvp.Value;
                    }

                    dt.Rows.Add(newRow);



                }
            }


            return dt.DefaultView;

        }


        public DataView? GetDataView()
        {
            var columnNames = Lua.GetTableDict(LuaTableDict?.First().Value as LuaTable).Keys;
            DataTable dt = new();
            dt.Columns.AddRange(columnNames.Select(columnName => new DataColumn(columnName.ToString())).ToArray());

            var list = new List<string>();
            if (LuaTableDict is { })
            {
                foreach (KeyValuePair<object, object> row in LuaTableDict)
                {
                    var lineInfo = ConvertLineToString(row.Value as LuaTable);

                    //check if theres a non existing column
                    var columns = lineInfo.Parameters.Keys;
                    foreach (var key in columns)
                    {
                        bool columnExists = dt.Columns.Cast<DataColumn>().Any(column => column.ColumnName.Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (!columnExists)
                        {
                            dt.Columns.Add(new DataColumn(key.ToString()));
                        }

                    }
                    DataRow newRow = dt.NewRow();


                    foreach (var kvp in lineInfo.Parameters)
                    {

                        newRow[kvp.Key] = kvp.Value;
                    }

                    dt.Rows.Add(newRow);



                }
            }


            return dt.DefaultView;

        }

        private List<string> ConverLuaToStringList(List<Dictionary<string, object>> info)
        {
            var list = new List<string>();
            if (LuaTableDict is { })
            {
                foreach (var row in LuaTableDict)
                {
                    string id = row.Key.ToString() ?? "";
                    var changedInfo = info.FirstOrDefault(dict => dict.ContainsKey("id") && dict["id"].ToString() == id);
                    var lineInfo = ConvertLineToString(row.Value as LuaTable, changedInfo);
                    string? line = $"\t[{id}] = {{{lineInfo.Text}}}";
                    list.Add(line);

                }
            }


            return list;
        }

        private LuaTableLine ConvertLineToString(LuaTable? luaTable, Dictionary<string, object>? changedInfo)
        {
            var _value = Lua.GetTableDict(luaTable);
            Dictionary<string, object> parameters = new();
            if (_value != null)
            {
                foreach (KeyValuePair<object, object> kvp in _value)
                {
                    object obj;

                    if (kvp.Value is string s)
                    {
                        s = changedInfo?[kvp.Key.ToString() ?? ""].ToString() ?? "";
                        obj = $"'{s}'";
                    }
                    else if (kvp.Value is LuaTable tableValue)
                    {

                        var dict = Lua.GetTableDict(tableValue);
                        bool isNewTable = CheckNumericalID(dict.Keys.ToList());
                        if (isNewTable || CheckSequential(dict.Keys.ToList())) //ReliveTime = {[9]=15} 
                        {
                            obj = "{" + (changedInfo?[kvp.Key.ToString() ?? ""].ToString() ?? "") + "}";

                        }
                        else
                        {
                            if (tableValue.Keys.Count > 0)
                            {
                                obj = $"{{{ConvertLineToString(tableValue, changedInfo).Text}}}";
                            }
                            else obj = "{" + (changedInfo?[kvp.Key.ToString() ?? ""].ToString() ?? "") + "}";


                        }
                    }
                    else obj = changedInfo?[kvp.Key.ToString() ?? ""].ToString() ?? "";

                    parameters.Add(kvp.Key.ToString() ?? "", obj);
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



        private List<string> ConverLuaToStringList()
        {
            var list = new List<string>();
            if (LuaTableDict is { })
            {
                foreach (var row in LuaTableDict)
                {
                    var lineInfo = ConvertLineToString(row.Value as LuaTable);
                    string? line = $"\t[{row.Key.ToString()}] = {{{lineInfo.Text}}}";
                    list.Add(line);

                }
            }


            return list;
        }

        private LuaTableLine ConvertLineToString(LuaTable? luaTable)
        {
            var _value = Lua.GetTableDict(luaTable);
            Dictionary<string, object> parameters = new();
            if (_value != null)
            {
                foreach (KeyValuePair<object, object> kvp in _value)
                {
                    object obj;

                    if (kvp.Value is string s)
                    {
                        obj = $"'{s}'";
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
                            obj = "{" + string.Join(", ", values) + "}";

                        }
                        else if (CheckSequential(dict.Keys.ToList()))
                        {
                            obj = "{" + string.Join(", ", dict.Values) + "}";

                        }
                        else
                        {
                            obj = $"{{{ConvertLineToString(tableValue).Text}}}";
                        }
                    }
                    else obj = kvp.Value;

                    parameters.Add(kvp.Key.ToString() ?? "", obj);
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
        
        public void SaveLuaTable()
        {
            var lines = ConverLuaToStringList();

            var intialLines = File.ReadAllLines(_filename);
            string someEncodedBs = intialLines[0];
            string TableNameline = intialLines[1];
            string ending = $"}}\nreturn {TableNameline.Split(" = ").First()}";

            string output = someEncodedBs + "\n" + TableNameline + "\n"
                + string.Join(",\n", lines) + "\n" + ending;

            File.WriteAllText("output.txt", output);

        }


        public void SaveLuaTable(DataView dataView)
        {
            List<Dictionary<string, object>> info = new();

            foreach(DataRow dataRow in dataView.ToTable().Rows)
            {
                var dataRowdict = dataRow.Table.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => dataRow[col]);
                info.Add(dataRowdict);
            }

            var lines = ConverLuaToStringList(info);

            var intialLines = File.ReadAllLines(_filename);
            string someEncodedBs = intialLines[0];
            string TableNameline = intialLines[1];
            string ending = $"}}\nreturn {TableNameline.Split(" = ").First()}";

            string output = someEncodedBs + "\n" + TableNameline + "\n"
                + string.Join(",\n", lines) + "\n" + ending;

            File.WriteAllText("output.txt", output);

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

        internal bool RowContainsColumn(string rowID, string columnName)
        {
            var row = LuaTableDict?.Where(kvp => kvp.Key.ToString() == rowID).First().Value;           
            var dict = Lua.GetTableDict(row as LuaTable);
            return dict.Keys.Contains(columnName);
        }

        internal void AddColumnToRow(string rowID, string columnName)
        {
            if(LuaTableDict is { })
            {
                var row = LuaTableDict.Where(kvp => kvp.Key.ToString() == rowID).First().Value as LuaTable;               
                
                foreach (var kvp in LuaTableDict)
                {
                    
                    string rowid = kvp.Key.ToString() ?? "";
                    if (RowContainsColumn(rowid, columnName))
                    {
                        var inner_dict = Lua.GetTableDict(kvp.Value as LuaTable);
                        if(row is { }) row[columnName] = inner_dict[columnName];                        
                        return;
                    }                


                }
            }
           
            
        }
    }
}
