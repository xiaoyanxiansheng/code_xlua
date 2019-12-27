namespace JYClient
{
    using System;
    using System.Collections.Generic;
    using XLua;
    using XLua.LuaDLL;

    static class LuaTableDataMgr
    {
        #region 数据
        // luaTableName对应luaTableData
        private static Dictionary<string, LuaTableData> luaTableMap = new Dictionary<string, LuaTableData>();
        #endregion

        #region 注册相关
        /// <summary>
        /// 游戏开始后注册一次即可
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

            string keyname = "lua_safe_bind_key";
            Lua.lua_pushstdcallcfunction(L, BindLuaTableKey);
            if (0 != Lua.xlua_setglobal(L, keyname))
            {
                throw new Exception("call xlua_setglobal fail!");
            }
        }
        /// <summary>
        /// lua调用 lua_safe_bind(name,table) 所有要访问的数据都要绑定
        /// </summary>
        public static int BindLuaTable(IntPtr L)
        {
            ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            string name = Lua.lua_tostring(L, 1);
            IntPtr ptr = Lua.lua_topointer(L, 2);
            if (ptr == IntPtr.Zero)
            {
                throw new Exception("lua_safe_bind error " + name);
            }
            luaTableMap[name] = new LuaTableData(name,ptr);
            return 0;
        }

        public static int BindLuaTableKey(IntPtr L)
        {
            string name = Lua.lua_tostring(L, 1);
            string key = Lua.lua_tostring(L, 2);
            int index = (int)Lua.lua_tonumber(L, 3);
            LuaTableData.AddKey(name, key, index);
            return 0;
        }
        #endregion
    }
}