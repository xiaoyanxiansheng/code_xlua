namespace JYClient
{
    using System;
    using System.Collections.Generic;
    using XLua;
    using XLua.LuaDLL;
    using MC.Framework;
    using System.Reflection;
    using Table;

    static class LuaConfigMgr
    {
        // tableName对应table表
        private static Dictionary<string, LuaConfig> luaConfigMap = new Dictionary<string, LuaConfig>();

        #region 注册
        /// <summary>
        /// 在lua虚拟机中注册绑定函数
        /// </summary>
        /// <param name="L"></param>
        public static void Register(System.IntPtr L)
        {
            string name = "lua_safe_bind";
            Lua.lua_pushstdcallcfunction(L, BindLuaTable);
            if (0 != Lua.xlua_setglobal(L, name))
            {
                throw new Exception("call xlua_setglobal fail!");
            }

            name = "lua_safe_bind_key";
            Lua.lua_pushstdcallcfunction(L, BindLuaTableKey);
            if (0 != Lua.xlua_setglobal(L, name))
            {
                throw new Exception("call xlua_setglobal fail!");
            }
        }
        /// <summary>
        /// 绑定数据
        /// </summary>
        public static int BindLuaTable(IntPtr L)
        {
            ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            string tableName = Lua.lua_tostring(L, 1);
            IntPtr ptr = Lua.lua_topointer(L, 2);
            if (ptr == IntPtr.Zero)
            {
                throw new Exception("lua_safe_bind error " + tableName);
            }
            LuaConfig luaConfig;
            if (!luaConfigMap.TryGetValue(tableName,out luaConfig))
            {
                luaConfigMap[tableName] = new LuaConfig(tableName);
            }
            luaConfigMap[tableName].AddConfig(ptr);
            return 0;
        }
        /// <summary>
        /// 绑定key值 为了方便访问 因为底层只是支持int索引
        /// </summary>
        /// <param name="L"></param>
        /// <returns></returns>
        public static int BindLuaTableKey(IntPtr L)
        {
            ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            string tableName = Lua.lua_tostring(L, 1);
            string keyName  = Lua.lua_tostring(L, 2);
            int keyIndex = (int)Lua.lua_tonumber(L, 3);
            LuaTableData.AddKey(tableName, keyName, keyIndex);
            return 0;
        }
        /// <summary>
        /// 数据绑定 将lua中数据绑定到C#中
        /// </summary>
        /// <param name="card"></param>
        public static void BindData(Card card)
        {
            card.DeserializeData(luaConfigMap);
        }
        #endregion

        /// <summary>
        /// 获取某个table表
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static LuaConfig Get(string name)
        {
            LuaConfig luaConfig;
            if (luaConfigMap.TryGetValue(name, out luaConfig))
            {
                return luaConfig;
            }
            else
            {
                GameLogger.Error("[LuaTableData] not tableName " + name);
            }
            return null;
        }
    }
}