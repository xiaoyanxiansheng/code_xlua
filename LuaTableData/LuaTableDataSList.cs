namespace JYClient
{
    using JYClient.LuaInterface;

    /// <summary>
    /// List中存放的对应string的id所以读取出来的时候需要转成字符串
    /// </summary>
    /// <typeparam name="Int"></typeparam>
    public class LuaTableDataSList<Int> : LuaTableDataList<Int>
    {
        public LuaTableDataSList(LuaTableData luaTableData):base(luaTableData){}

        public new string this[int index]
        {
            get { return StringLocal.GetString(luaTableData.Get<int>(++index)); } // lua中索引从1开始
        }
    }
}
