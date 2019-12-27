namespace JYClient
{
    using System;
    using System.Collections.Generic;
    using MC.Framework;

    public class LuaConfig
    {

        public string tableName;
        public LuaConfig(string tableName)
        {
            this.tableName = tableName;
        }

        // 数据集合
        private List<LuaTableData> mValue = new List<LuaTableData>();

        public List<LuaTableData> Values
        {
            get
            {
                return mValue;
            }
        }

        public void AddConfig(IntPtr ptr)
        {
            LuaTableData luaTableData = new LuaTableData(tableName, ptr);
            mValue.Add(new LuaTableData(tableName, ptr));
        }
    }
}