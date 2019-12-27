
namespace JYClient
{
    using System.Collections;
    using System.Collections.Generic;

    public class LuaTableDataList<T> : IEnumerable
    {
        public LuaTableDataList(LuaTableData luaTableData)
        {
            this.luaTableData = luaTableData;
        }

        protected LuaTableData luaTableData;

        public int Count
        {
            get { return luaTableData.Count; }
        }

        public virtual T this[int index]
        {
            get { return luaTableData.Get<T>(++index); } // lua中索引从1开始
        }

        int tempCurrent = -1;
        public IEnumerator GetEnumerator()
        {
            while (tempCurrent < Count - 1)
            {
                tempCurrent++;
                yield return this[tempCurrent];
            }
            tempCurrent = -1;
        }

        public bool Contains(int index)
        {
            return !luaTableData.IsNil(index);
        }

        /// <summary>
        /// 转化成List数据 有GC的接口 不要频繁的使用
        /// </summary>
        /// <returns></returns>
        public List<T> ToList()
        {
            List<T> ls = new List<T>();
            for(int i = 1; i <= luaTableData.Count; i++)
            {
                ls.Add(luaTableData.Get<T>(i));
            }
            return ls;
        }

        // TODO 待删除
        public void Add(object o)
        {

        }
    }
}
