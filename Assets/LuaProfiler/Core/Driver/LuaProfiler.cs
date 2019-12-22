/*
               #########                       
              ############                     
              #############                    
             ##  ###########                   
            ###  ###### #####                  
            ### #######   ####                 
           ###  ########## ####                
          ####  ########### ####               
         ####   ###########  #####             
        #####   ### ########   #####           
       #####   ###   ########   ######         
      ######   ###  ###########   ######       
     ######   #### ##############  ######      
    #######  #####################  ######     
    #######  ######################  ######    
   #######  ###### #################  ######   
   #######  ###### ###### #########   ######   
   #######    ##  ######   ######     ######   
   #######        ######    #####     #####    
    ######        #####     #####     ####     
     #####        ####      #####     ###      
      #####       ###        ###      #        
        ###       ###        ###               
         ##       ###        ###               
__________#_______####_______####______________
                我们的未来没有BUG                
* ==============================================================================
* Filename: LuaProfiler
* Created:  2018/7/13 14:29:22
* Author:   エル・プサイ・コングリィ
* Purpose:  
* ==============================================================================
*/

#if UNITY_EDITOR  || USE_LUA_PROFILER
using System;
using System.Collections.Generic;
using System.Reflection;
using RefDict = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace MikuLuaProfiler
{
    public static class LuaProfiler
    {
        #region member
        private static IntPtr _mainL = IntPtr.Zero;
        private static readonly Stack<Sample> beginSampleMemoryStack = new Stack<Sample>();
        private static int m_currentFrame = 0;
        public static int mainThreadId = -100;
        const long MaxB = 1024;
        const long MaxK = MaxB * 1024;
        const long MaxM = MaxK * 1024;
        const long MaxG = MaxM * 1024;

        private static Action<Sample> m_onReceiveSample;
        private static Action<LuaRefInfo> m_onReceiveRef;
        private static Action<LuaDiffInfo> m_onReceiveDiff;
        public static void RegisterOnReceiveSample(Action<Sample> onReceive)
        {
            m_onReceiveSample = onReceive;
        }
        public static void RegisterOnReceiveRefInfo(Action<LuaRefInfo> onReceive)
        {
            m_onReceiveRef = onReceive;
        }
        public static void RegisterOnReceiveDiffInfo(Action<LuaDiffInfo> onReceive)
        {
            m_onReceiveDiff = onReceive;
        }

        public static void UnRegistReceive()
        {
            m_onReceiveSample = null;
            m_onReceiveRef = null;
            m_onReceiveDiff = null;
        }
        #endregion

        #region property
        public static bool m_hasL = false;
        public static IntPtr mainL
        {
            get
            {
                return _mainL;
            }
            set
            {
                if (value != IntPtr.Zero)
                {
                    m_hasL = true;
                    LuaDLL.luaL_initlibs(value);
                }
                else
                {
                    m_hasL = false;
                }
                _mainL = value;
            }
        }
        public static bool IsMainThread
        {
            get
            {
                return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
            }
        }
        #endregion

        #region sample
        public static void BeginSampleCSharp(string name)
        {
            BeginSample(_mainL, name);
        }
        public static void EndSampleCSharp()
        {
            EndSample(_mainL);
        }

        public static long getcurrentTime
        {
            get
            {
                return System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        public static void BeginSample(IntPtr luaState, string name, bool needShow = false)
        {
            if (!IsMainThread)
            {
                return;
            }
            try
            {
                int frameCount = SampleData.frameCount;

                if (m_currentFrame != frameCount)
                {
                    PopAllSampleWhenLateUpdate(luaState);
                    m_currentFrame = frameCount;
                }
                long memoryCount = LuaLib.GetLuaMemory(luaState);
                Sample sample = Sample.Create(getcurrentTime, (int)memoryCount, name);
                sample.needShow = needShow;
                beginSampleMemoryStack.Push(sample);
                Profiler.BeginSample(name);
            }
            catch
            {
            }
        }
        private static List<Sample> popChilds = new List<Sample>();
        public static void PopAllSampleWhenLateUpdate(IntPtr luaState)
        {
            while(beginSampleMemoryStack.Count > 0)
            {
                var item = beginSampleMemoryStack.Pop();
                if (item.fahter == null)
                {
                    if (beginSampleMemoryStack.Count > 0)
                    {
                        long mono_gc = 0;
                        long lua_gc = 0;
                        long cost_time = 0;
                        for (int i = 0, imax = item.childs.Count; i < imax; i++)
                        {
                            Sample c = item.childs[i];
                            lua_gc += c.costLuaGC;
                            mono_gc += c.costMonoGC;
                            cost_time += c.costTime;
                        }
                        item.costLuaGC = (int)Math.Max(lua_gc, 0);
                        item.costMonoGC = (int)Math.Max(mono_gc, 0);
                        item.costTime = (int)cost_time;

                        popChilds.Add(item);
                    }
                    else
                    {
                        item.costLuaGC = (int)LuaLib.GetLuaMemory(luaState) - item.currentLuaMemory;
                        item.costTime = (int)(getcurrentTime - item.currentTime);
                        item.costMonoGC = (int)(GC.GetTotalMemory(false) - item.currentMonoMemory);
                        item.currentLuaMemory = (int)LuaLib.GetLuaMemory(luaState);
                        for (int i = 0, imax = popChilds.Count; i < imax; i++)
                        {
                            popChilds[i].fahter = item;
                        }
                        popChilds.Clear();
                        var setting = LuaDeepProfilerSetting.Instance;
                        if (!setting.isLocal)
                        {
                            NetWorkClient.SendMessage(item);
                        }
                        else if (m_onReceiveSample != null)
                        {
                            m_onReceiveSample(item);
                        }
                    }
                    //item.Restore();
                }
            }
            beginSampleMemoryStack.Clear();
        }
        public static void EndSample(IntPtr luaState)
        {
            if (!IsMainThread)
            {
                return;
            }

            if (beginSampleMemoryStack.Count <= 0)
            {
                return;
            }
            long nowMemoryCount = LuaLib.GetLuaMemory(luaState);
            long nowMonoCount = GC.GetTotalMemory(false);
            Sample sample = beginSampleMemoryStack.Pop();

            sample.costTime = (int)(getcurrentTime - sample.currentTime);
            var monoGC = nowMonoCount - sample.currentMonoMemory;
            var luaGC = nowMemoryCount - sample.currentLuaMemory;
            sample.currentLuaMemory = (int)nowMemoryCount;
            sample.currentMonoMemory = (int)nowMonoCount;
            sample.costLuaGC = (int)luaGC;
            sample.costMonoGC = (int)monoGC;

            if (sample.childs.Count > 0)
            {
                long mono_gc = 0;
                long lua_gc = 0;
                for (int i = 0, imax = sample.childs.Count; i < imax; i++)
                {
                    Sample c = sample.childs[i];
                    lua_gc += c.costLuaGC;
                    mono_gc += c.costMonoGC;
                }
                sample.costLuaGC = (int)Math.Max(lua_gc, luaGC);
                sample.costMonoGC = (int)Math.Max(mono_gc, monoGC);
            }
            long selfLuaGC = sample.selfLuaGC;
            if (selfLuaGC > 0)
            {
#pragma warning disable 0219
                byte[] luagc = new byte[Math.Max(0, selfLuaGC - 32)];
#pragma warning restore 0219
            }
            Profiler.EndSample();

            if (!sample.CheckSampleValid())
            {
                sample.Restore();
                return;
            }
            sample.fahter = beginSampleMemoryStack.Count > 0 ? beginSampleMemoryStack.Peek() : null;
            //UnityEngine.Debug.Log(sample.name);
            if (beginSampleMemoryStack.Count == 0)
            {
                var setting = LuaDeepProfilerSetting.Instance;
                if (setting == null) return;
                if (setting != null && setting.isNeedCapture)
                {
                    //迟钝了
                    if (sample.costTime >= (1 / (float)(setting.captureFrameRate)) * 10000000)
                    {
                        sample.captureUrl = Sample.Capture();
                    }
                    else if (sample.costLuaGC > setting.captureLuaGC)
                    {
                        sample.captureUrl = Sample.Capture();
                    }
                    else if (sample.costMonoGC > setting.captureMonoGC)
                    {
                        sample.captureUrl = Sample.Capture();
                    }
                    else
                    {
                        sample.captureUrl = null;
                    }
                }
                if (!setting.isLocal)
                {
                    NetWorkClient.SendMessage(sample);
                }
                else if(m_onReceiveSample != null)
                {
                    m_onReceiveSample(sample);
                }
            }
            //释放掉被累加的Sample
            if (beginSampleMemoryStack.Count != 0 && sample.fahter == null)
            {
                sample.Restore();
            }
        }

        public static void SendFrameSample()
        {
            var setting = LuaDeepProfilerSetting.Instance;
            long memoryCount = LuaLib.GetLuaMemory(_mainL);
            Sample sample = Sample.Create(getcurrentTime, (int)memoryCount, "");
            if (!setting.isLocal)
            {
                NetWorkClient.SendMessage(sample);
            }
            else if (m_onReceiveSample != null)
            {
                m_onReceiveSample(sample);
            }
        }

        #endregion

        #region check
        public static int historyRef = -100;
        public static void Record()
        {
            IntPtr L = LuaProfiler.mainL;
            if (L == IntPtr.Zero)
            {
                return;
            }
            LuaDLL.isHook = false;

            ClearRecord();
            UnityEngine.Resources.UnloadUnusedAssets();
            // 调用C# LuaTable LuaFunction WeakTable的析构 来清理掉lua的 ref
            GC.Collect();
            // 清理掉C#强ref后，顺便清理掉很多弱引用
            LuaDLL.lua_gc(L, LuaGCOptions.LUA_GCCOLLECT, 0);

            int oldTop = LuaDLL.lua_gettop(L);
            LuaDLL.lua_getglobal(L, "miku_handle_error");

            LuaDLL.lua_getglobal(L, "miku_do_record");
            LuaDLL.lua_getglobal(L, "_G");
            LuaDLL.lua_pushstring(L, "");
            LuaDLL.lua_pushstring(L, "_G");
            //recrod
            LuaDLL.lua_newtable(L);
            historyRef = LuaDLL.luaL_ref(L, LuaIndexes.LUA_REGISTRYINDEX);
            LuaDLL.lua_getref(L, historyRef);
            //history
            LuaDLL.lua_pushnil(L);
            //null_list
            LuaDLL.lua_newtable(L);

            if (LuaDLL.lua_pcall(L, 6, 0, oldTop + 1) == 0)
            {
                LuaDLL.lua_remove(L, oldTop + 1);
            }
            LuaDLL.lua_settop(L, oldTop);

            oldTop = LuaDLL.lua_gettop(L);
            LuaDLL.lua_getglobal(L, "miku_handle_error");

            LuaDLL.lua_getglobal(L, "miku_do_record");
            LuaDLL.lua_pushvalue(L, LuaIndexes.LUA_REGISTRYINDEX);
            LuaDLL.lua_pushstring(L, "");
            LuaDLL.lua_pushstring(L, "_R");
            LuaDLL.lua_getref(L, historyRef);
            //history
            LuaDLL.lua_pushnil(L);
            //null_list
            LuaDLL.lua_newtable(L);

            if (LuaDLL.lua_pcall(L, 6, 0, oldTop + 1) == 0)
            {
                LuaDLL.lua_remove(L, oldTop + 1);
            }
            LuaDLL.lua_settop(L, oldTop);

            LuaDLL.isHook = true;
        }
        private static void ClearRecord()
        {
            IntPtr L = LuaProfiler.mainL;
            if (L == IntPtr.Zero)
            {
                return;
            }
            if (historyRef != -100)
            {
                LuaDLL.lua_unref(L, historyRef);
                historyRef = -100;
            }
        }
        private static void SetTable(int refIndex, Dictionary<string, LuaTypes> dict, Dictionary<string, List<string>> detailDict)
        {
            IntPtr L = LuaProfiler.mainL;
            if (L == IntPtr.Zero)
            {
                return;
            }
            dict.Clear();
            int oldTop = LuaDLL.lua_gettop(L);

            LuaDLL.lua_getref(L, refIndex);
            if (LuaDLL.lua_type(L, -1) != LuaTypes.LUA_TTABLE)
            {
                LuaDLL.lua_pop(L, 1);
                return;
            }
            int t = oldTop + 1;
            LuaDLL.lua_pushnil(L);  /* 第一个 key */
            while (LuaDLL.lua_next(L, t) != 0)
            {
                /* 用一下 'key' （在索引 -2 处） 和 'value' （在索引 -1 处） */
                int key_t = LuaDLL.lua_gettop(L);
                LuaDLL.lua_pushnil(L);  /* 第一个 key */
                string firstKey = null;
                List<string> detailList = new List<string>();
                while (LuaDLL.lua_next(L, key_t) != 0)
                {
                    string key = LuaHook.GetRefString(L, -1);
                    if (string.IsNullOrEmpty(firstKey))
                    {
                        firstKey = key;
                    }
                    detailList.Add(key);
                    LuaDLL.lua_pop(L, 1);
                }
                LuaDLL.lua_settop(L, key_t);
                if (!string.IsNullOrEmpty(firstKey))
                {
                    dict[firstKey] = (LuaTypes)LuaDLL.lua_type(L, -2);
                    detailDict[firstKey] = detailList;
                }

                /* 移除 'value' ；保留 'key' 做下一次迭代 */
                LuaDLL.lua_pop(L, 1);
            }
            LuaDLL.lua_settop(L, oldTop);
        }

        public static void DiffServer()
        {
            NetWorkClient.SendMessage(Diff());
        }

        public static LuaDiffInfo Diff()
        {
            IntPtr L = LuaProfiler.mainL;
            if (L == IntPtr.Zero)
            {
                return null;
            }
            LuaDLL.isHook = false;
            UnityEngine.Resources.UnloadUnusedAssets();
            // 调用C# LuaTable LuaFunction WeakTable的析构 来清理掉lua的 ref
            GC.Collect();
            // 清理掉C#强ref后，顺便清理掉很多弱引用
            LuaDLL.lua_gc(L, LuaGCOptions.LUA_GCCOLLECT, 0);
            if (historyRef == -100)
            {
                return null;
            }

            int oldTop = LuaDLL.lua_gettop(L);
            LuaDLL.lua_getglobal(L, "miku_handle_error");

            LuaDLL.lua_getglobal(L, "miku_diff");
            LuaDLL.lua_getref(L, historyRef);
            if (LuaDLL.lua_type(L, -1) != LuaTypes.LUA_TTABLE)
            {
                LuaDLL.lua_settop(L, oldTop);
                historyRef = -100;
                return null;
            }

            if (LuaDLL.lua_pcall(L, 1, 3, oldTop + 1) == 0)
            {
                LuaDLL.lua_remove(L, oldTop + 1);
            }
            int nullObjectRef = LuaDLL.luaL_ref(L, LuaIndexes.LUA_REGISTRYINDEX);
            int rmRef = LuaDLL.luaL_ref(L, LuaIndexes.LUA_REGISTRYINDEX);
            int addRef = LuaDLL.luaL_ref(L, LuaIndexes.LUA_REGISTRYINDEX);
            LuaDiffInfo ld = LuaDiffInfo.Create();
            SetTable(nullObjectRef, ld.nullRef, ld.nullDetail);
            SetTable(rmRef, ld.rmRef, ld.rmDetail);
            SetTable(addRef, ld.addRef, ld.addDetail);

            LuaDLL.lua_unref(L, nullObjectRef);
            LuaDLL.lua_unref(L, rmRef);
            LuaDLL.lua_unref(L, addRef);
            LuaDLL.lua_settop(L, oldTop);

            LuaDLL.isHook = true;

            return ld;
        }
        #endregion

        #region ref
        private static Dictionary<byte, RefDict> m_refDict = new Dictionary<byte, RefDict>(4);

        public static void AddRef(string refName, string refAddr, byte type)
        {
            RefDict refDict;
            if (!m_refDict.TryGetValue(type, out refDict))
            {
                refDict = new RefDict(2048);
                m_refDict.Add(type, refDict);
            }

            HashSet<string> addrList;
            if (!refDict.TryGetValue(refName, out addrList))
            {
                addrList = new HashSet<string>();
                refDict.Add(refName, addrList);
            }
            if (!addrList.Contains(refAddr))
            {
                addrList.Add(refAddr);
            }
            SendAddRef(refName, refAddr, type);
        }
        public static void SendAddRef(string funName, string funAddr, byte type)
        {
            LuaRefInfo refInfo = LuaRefInfo.Create(1, funName, funAddr, type);
            var setting = LuaDeepProfilerSetting.Instance;
            if (!setting.isLocal)
            {
                NetWorkClient.SendMessage(refInfo);
            }
            else if (m_onReceiveRef != null)
            {
                m_onReceiveRef(refInfo);
            }
        }
        public static void RemoveRef(string refName, string refAddr, byte type)
        {
            if (string.IsNullOrEmpty(refName)) return;
            RefDict refDict;

            if (!m_refDict.TryGetValue(type, out refDict))
            {
                return;
            }

            HashSet<string> addrList;
            if (!refDict.TryGetValue(refName, out addrList))
            {
                return;
            }
            if (!addrList.Contains(refAddr))
            {
                return;
            }
            addrList.Remove(refAddr);
            if (addrList.Count == 0)
            {
                refDict.Remove(refName);
            }
            SendRemoveRef(refName, refAddr, type);
        }
        public static void SendRemoveRef(string funName, string funAddr, byte type)
        {
            LuaRefInfo refInfo = LuaRefInfo.Create(0, funName, funAddr, type);
            var setting = LuaDeepProfilerSetting.Instance;
            if (!setting.isLocal)
            {
                NetWorkClient.SendMessage(refInfo);
            }
            else if (m_onReceiveRef != null)
            {
                m_onReceiveRef(refInfo);
            }
        }
        public static void SendAllRef()
        {
            foreach (var dictItem in m_refDict)
            {
                foreach (var hashList in dictItem.Value)
                {
                    foreach (var item in hashList.Value)
                    {
                        SendAddRef(hashList.Key, item, dictItem.Key);
                    }
                }
            }
        }
        #endregion

    }
}
#endif

