
namespace JYClient
{
    using System;
    using System.Collections.Generic;
    using MC.Framework;

    /// <summary>
    /// 对应array数据部分
    /// </summary>
    public class LuaTableData : LuaTableRawData
    {
        #region 私有
        private string tableName;
        // 对key值访问的支持
        private class KeyT
        {
            public KeyT(string keyName,int keyIndex)
            {
                this.keyName = keyName;
                this.keyIndex = keyIndex;
            }
            public string keyName;
            public int keyIndex;
        }
        private static Dictionary<string, Dictionary<string, KeyT>> keys = new Dictionary<string, Dictionary<string, KeyT>>();

        // 对泛型访问的支持
        private Dictionary<Type, Delegate> getVFuncMap = null;
        private bool TryGetVFunc<T>(Type type, out T func) where T : class
        {
            if (getVFuncMap == null)
            {
                getVFuncMap = new Dictionary<Type, Delegate>()
                {
                    { typeof(int),new Func<int,int>(GetInt)},
                    { typeof(double),new Func<int,double>(GetDouble)},
                    { typeof(string),new Func<int,string>(GetString)},
                    { typeof(bool),new Func<int,bool>(GetBool)},
                    { typeof(float),new Func<int,float>(GetFloat)},
                    { typeof(LuaTableData),new Func<int,LuaTableData>(GetTable)},
                };
            }

            Delegate obj;
            if(getVFuncMap.TryGetValue(type,out obj))
            {
                func = obj as T;
                return true;
            }
            func = null;
            return false;
        }
        
        private LuaTableData GetTable(int key)
        {
            string tName = GetKeyName(key);
            return new LuaTableData(tName, GetTablePtr(key));
        }
        #endregion

        #region 对方开放
        public LuaTableData(string tableName, IntPtr intPtr) : base(intPtr)
        {
            this.tableName = tableName;
        }

        /// <summary>
        /// 通过字段访问数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            var ret = default(T);

            Dictionary<string,KeyT> keyts;
            if(keys.TryGetValue(tableName, out keyts))
            {
                KeyT keyt;
                if(keyts.TryGetValue(key,out keyt))
                {
                    ret = Get<T>(keyt.keyIndex);
                }
                else
                {
                    GameLogger.Error("[LuaTableData] not key " + key + " in table " + tableName);
                }
            }
            else
            {
                GameLogger.Error("[LuaTableData] not tableName " + tableName);
            }

            return ret;
        }
        /// <summary>
        /// 通过索引访问
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(int key)
        {
            var ret = default(T);

            Func<int, T> func;
            if (TryGetVFunc(typeof(T), out func))
            {
                ret = func(key);
            }
            else
            {
                GameLogger.Error("[LuaTableData] not type " + typeof(T));
            }

            return ret;
        }
        /// <summary>
        /// 建立id和name的对应关系 方便后期通过keyName去访问
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <param name="index"></param>
        public static void AddKey(string name,string key,int index)
        {
            Dictionary<string, KeyT> keyt;
            if (!keys.TryGetValue(name,out keyt))
            {
                keys[name] = new Dictionary<string, KeyT>();
            }
            keys[name].Add(key, new KeyT(key, index));
        }
        /// <summary>
        /// 获取索引对应的keyName
        /// </summary>
        /// <param name="keyIndex"></param>
        /// <returns></returns>
        public string GetKeyName(int keyIndex)
        {
            Dictionary<string, KeyT> keyts;
            if (keys.TryGetValue(tableName, out keyts))
            {
                foreach( KeyT t in keyts.Values)
                {
                    if (t.keyIndex == keyIndex)
                    {
                        return t.keyName;
                    }
                }
            }
            return null;
        }
        #endregion
    }
}