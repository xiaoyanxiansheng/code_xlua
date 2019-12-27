namespace JYClient
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    #region 对应C中结构
    
    // lua中的table结构
    [StructLayout(LayoutKind.Sequential)]
    public struct LuaTableRawDef
    {
        public IntPtr next;

        // lu_byte tt; lu_byte marked; lu_byte flags; lu_byte lsizenode;
        public uint bytes;

        // unsigned int sizearray
        public uint sizearray;

        // TValue* array
        public IntPtr array;

        // Node* node
        public IntPtr node;

        // Node* lastfree
        public IntPtr lastfree;

        // Table* metatable
        public IntPtr metatable;

        // GCObejct* gclist
        public IntPtr gclist;
    }

    // lua字符串
    [StructLayout(LayoutKind.Sequential)]
    public struct TString
    {
        public IntPtr next;

        public byte tt;

        public byte marked;

        public byte extra;/* reserved words for short strings; "has hash" for longs */

        public byte shrlen;/* length for short strings */

        public uint hash;

        public TStringU u;
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public unsafe struct TStringU
    {
        [FieldOffset(0)]
        public int lnglen;
        [FieldOffset(0)]
        public TString* hnext;
    }
    // lua中字符串存储位置的偏移
    [StructLayout(LayoutKind.Sequential,Size = 12)]
    public struct UTString
    {

    }

    /// <summary>
    /// 明确内存的分配方式 前8个字节是数据 后4个字节是类型 占用16是因为字节对齐
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct LuaTValue
    {
        // integer value
        [FieldOffset(0)]
        public long i;
        // number
        [FieldOffset(0)]
        public double n;
        // table
        [FieldOffset(0)]
        public LuaTableRawDef* h;
        // string
        [FieldOffset(0)]
        public TString* ts;
        // int tt_
        [FieldOffset(8)]
        public int tt_;
    }

    /// <summary>
    /// lua中的类型定义
    /// </summary>
    public static class LuaEnvValues
    {
        public const int BIT_ISCOLLECTABLE = (1 << 6);

        public const int LUA_TNIL       = 0;                            // nil
        public const int LUA_TBOOLEAN   = 1;                            // bool
        public const int LUA_TNUMBER    = 3;                            // number(double)
        public const int LUA_TNUMFLT    = (LUA_TNUMBER | (0 << 4));     // number(double)
        public const int LUA_TNUMINT    = (LUA_TNUMBER | (1 << 4));     // integer
        public const int LUA_TSTRING    = 4 | BIT_ISCOLLECTABLE;        // string
        public const int LUA_TSHRSTR    = (LUA_TSTRING | (0 << 4));     // string(short)
        public const int LUA_TLNGSTR    = (LUA_TSTRING | (1 << 4));     // string(long)
        public const int LUA_TTABLE     = 5 | BIT_ISCOLLECTABLE;        // table
    }

#endregion

    /// <summary>
    /// 对应lua表指针 并且后续操作都是基于这个指针
    /// </summary>
    public unsafe class LuaTableRawData
    {
        // lua结构表指针
        protected LuaTableRawDef* TableRawPtr;
        // lua内存分配大小(注意是array部分)
        private int sizeArray = 0;
        // lua数据部分大小(和sizeArray是不一样的),避免
        private int length = -1;
        // 是否是静态luatable 静态只会计算一次length
        private bool isStaticTable = true;

        public LuaTableRawData(IntPtr intPtr)
        {
            TableRawPtr = (LuaTableRawDef*)intPtr;
            sizeArray = (int)TableRawPtr->sizearray;
        }

#region 基础功能
        public Object this[int key]
        {
            get { return GetValue(key); }
        }
        /// <summary>
        /// 返回 通用 可能会有GC
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Object GetValue(int key)
        {
            if (IsVaild(key))
            {
                int valueType = GetValueType(key);
                if (valueType == LuaEnvValues.LUA_TNIL)
                    return null;
                else if (valueType == LuaEnvValues.LUA_TNUMFLT)
                    return GetDouble(key);
                else if (valueType == LuaEnvValues.LUA_TNUMINT)
                    return GetInt(key);
                else if (valueType == LuaEnvValues.LUA_TBOOLEAN) { }
                // return GetBool(key); 可以直接使用int接收1为true
                else if (valueType == LuaEnvValues.LUA_TTABLE)
                    return GetTablePtr(key);
                else if (valueType == LuaEnvValues.LUA_TSTRING)
                    return GetString(key);
                UnityEngine.Debug.LogError("not surport type " + valueType);
            }
            return null;
        }
        /// <summary>
        /// 返回 int
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetInt(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                return (int)tv->i;
            }
            return 0;
        }
        /// <summary>
        /// 返回 double
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public double GetDouble(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                var i = tv->n;
                return tv->n;
            }
            return 0;
        }
        /// <summary>
        /// 返回 float
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public float GetFloat(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                var i = tv->n;
                return (float)tv->n;
            }
            return 0;
        }
        /// <summary>
        /// 返回 lua table
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IntPtr GetTablePtr(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                return (IntPtr)tv->h;
            }
            return IntPtr.Zero;
        }
        /// <summary>
        /// 返回 字符串
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetString(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                TString* ts = tv->ts;
                int len = tv->tt_ == LuaEnvValues.LUA_TSHRSTR ? ts->shrlen : ts->u.lnglen;
                IntPtr p = (IntPtr)((char*)ts + sizeof(UTString));
                string ret = Marshal.PtrToStringAnsi(p, len);

                if (ret == null)
                {
                    byte[] buffer = new byte[len];
                    Marshal.Copy(p, buffer, 0, len);
                    return Encoding.UTF8.GetString(buffer);
                }

                return ret;
            }
            return null;
        }
        /// <summary>
        /// 返回 bool
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool GetBool(int key)
        {
            return GetInt(key) == 1;
        }

        /// <summary>
        /// 返回 数据类型 和lua中对应
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetValueType(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                return tv->tt_;
            }
            return LuaEnvValues.LUA_TNIL;
        }

#region 暂时不用
        public void SetDouble(int key, double value)
        {
            if (IsVaild(key))
            {
                int size = sizeof(LuaTValue);
                key = key - 1;
                LuaTValue* v = ((LuaTValue*)(TableRawPtr->array)) + key;
                v->n = value;
                v->tt_ = LuaEnvValues.LUA_TNUMFLT;
            }
        }
        public void SetInt(int key, int value)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* v = ((LuaTValue*)(TableRawPtr->array)) + key;
                v->i = value;
                v->tt_ = LuaEnvValues.LUA_TNUMINT;
            }
        }
#endregion
#endregion

#region 帮助函数
        /// <summary>
        /// 是否合法
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsVaild(int key)
        {
            if (TableRawPtr != null && key > 0 && key <= TableRawPtr->sizearray)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否为空 本方案只要出现空就代表长度的计算截止 所以只支持array数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsNil(int key)
        {
            if (IsVaild(key))
            {
                key = key - 1;
                LuaTValue* tv = (LuaTValue*)(TableRawPtr->array) + key;
                if (tv->tt_ == LuaEnvValues.LUA_TNIL)
                    return true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// luatable的数据长度
        /// </summary>
        public int Count
        {
            get
            {
                if (length == -1 || !isStaticTable)
                    GetLen(out length);
                return length;
            }
        }

        /// <summary>
        /// 采用二分法求lua表格的使用数据大小(注意并不是分配的大小)
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        private void GetLen(out int length, int startIndex = 0, int endIndex = 0)
        {
            length = 0;
            if (endIndex == 0) endIndex = sizeArray;
            if (endIndex == 0) return;
            if (startIndex == 0) startIndex = 1;

            int midIndex = (int)((startIndex + endIndex) * 0.5);
            if (IsNil(midIndex))
            {
                GetLen(out length, startIndex, midIndex);
            }
            else
            {
                int nextIndex = midIndex + 1;
                if (IsNil(nextIndex))
                {
                    length = midIndex;
                }
                else
                {
                    GetLen(out length, nextIndex, endIndex);
                }
            }
        }

        /// <summary>
        /// 获取对象内存地址
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public string getMemory(object o)
        {
            GCHandle h = GCHandle.Alloc(o, GCHandleType.WeakTrackResurrection);

            IntPtr addr = GCHandle.ToIntPtr(h);

            return "0x" + addr.ToString("X");
        }
#endregion
    }
}