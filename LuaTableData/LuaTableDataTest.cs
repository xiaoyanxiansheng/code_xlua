using UnityEngine;
using XLua;
using System;

namespace JYClient
{
    [LuaCallCSharp]
    public class LuaTableDataTest : MonoBehaviour
    {
        public TextAsset luaScript;

        internal static LuaEnv luaEnv;

        private Action luaStart;
        private Action luaOnDestroy;

        private LuaTable scriptEnv;

        public void PrintTable()
        {
            /*LuaTableData table = LuaConfigMgr.GetTable("one");
            Debug.Log(table.Length);
            Debug.Log(table.GetInt(1));
            Debug.Log(table.GetTable(2).GetInt(1));
            Debug.Log(table.GetTable(2).GetInt(2));
            Debug.Log(table.GetTable(2).GetString(3));
            Debug.Log(table.GetTable(2).GetTable(4).GetString(1));
            Debug.Log(table.GetTable(2).GetTable(4).GetInt(2));
            Debug.Log(table.GetInt(3));
            Debug.Log("=====================");
            Debug.Log(table[1]);
            Debug.Log(((LuaTableData)table[2])[1]);
            Debug.Log(((LuaTableData)table[2])[2]);
            Debug.Log(((LuaTableData)table[2])[3]);
            Debug.Log(((LuaTableData)(((LuaTableData)table[2])[4]))[1]);
            Debug.Log(((LuaTableData)(((LuaTableData)table[2])[4]))[2]);
            Debug.Log(table[3]);*/
        }

        void Awake()
        {
            luaEnv = new LuaEnv(); //all lua behaviour shared one luaenv only!

            scriptEnv = luaEnv.NewTable();

            // 为每个脚本设置一个独立的环境，可一定程度上防止脚本间全局变量、函数冲突
            LuaTable meta = luaEnv.NewTable();
            meta.Set("__index", luaEnv.Global);
            scriptEnv.SetMetaTable(meta);
            meta.Dispose();

            scriptEnv.Set("self", this);

            LuaConfigMgr.Register(luaEnv.L);

            luaEnv.DoString(luaScript.text, "LuaBehaviour", scriptEnv);
            Action luaAwake = scriptEnv.Get<Action>("awake");
            scriptEnv.Get("start", out luaStart);
            scriptEnv.Get("ondestroy", out luaOnDestroy);

            if (luaAwake != null)
            {
                luaAwake();
            }
        }

        // Use this for initialization
        void Start()
        {
            if (luaStart != null)
            {
                luaStart();
            }
        }

        void OnDestroy()
        {
            if (luaOnDestroy != null)
            {
                luaOnDestroy();
            }
            luaOnDestroy = null;
            luaStart = null;
            scriptEnv.Dispose();
        }
    }

}